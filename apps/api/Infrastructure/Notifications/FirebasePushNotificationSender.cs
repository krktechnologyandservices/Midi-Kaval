using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MidiKaval.Api.Infrastructure.Notifications;

public sealed class FirebasePushNotificationSender : IPushNotificationSender
{
    private static readonly object InitLock = new();
    private static bool _initialized;

    public FirebasePushNotificationSender(
        IOptions<PushNotificationsOptions> options,
        ILogger<FirebasePushNotificationSender> logger)
    {
        EnsureInitialized(options.Value, logger);
    }

    public async Task<PushSendResult> SendAsync(
        PushSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = new Message
        {
            Token = request.Device.PushToken,
            Notification = new Notification
            {
                Title = request.Title,
                Body = request.Body,
            },
            Data = request.Data.ToDictionary(static pair => pair.Key, static pair => pair.Value),
            Android = new AndroidConfig { Priority = Priority.High },
            Apns = new ApnsConfig
            {
                Headers = new Dictionary<string, string> { ["apns-priority"] = "10" },
                Aps = new Aps
                {
                    Alert = new ApsAlert
                    {
                        Title = request.Title,
                        Body = request.Body,
                    },
                },
            },
        };

        try
        {
            await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
            return new PushSendResult(true, false, null, null);
        }
        catch (FirebaseMessagingException ex) when (IsStaleTokenError(ex))
        {
            return new PushSendResult(false, true, ex.MessagingErrorCode?.ToString(), ex.Message);
        }
        catch (FirebaseMessagingException ex)
        {
            return new PushSendResult(false, false, ex.MessagingErrorCode?.ToString(), ex.Message);
        }
    }

    private static bool IsStaleTokenError(FirebaseMessagingException ex)
    {
        if (ex.MessagingErrorCode is MessagingErrorCode.Unregistered
            or MessagingErrorCode.SenderIdMismatch)
        {
            return true;
        }

        if (ex.MessagingErrorCode is MessagingErrorCode.InvalidArgument)
        {
            var message = ex.Message;
            return message.Contains("registration token", StringComparison.OrdinalIgnoreCase)
                || message.Contains("not a valid FCM", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static void EnsureInitialized(PushNotificationsOptions options, ILogger logger)
    {
        if (_initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            if (FirebaseApp.DefaultInstance is null)
            {
                var credential = LoadCredential(options);
                FirebaseApp.Create(new AppOptions
                {
                    Credential = credential,
                    ProjectId = options.ProjectId,
                });
                logger.LogInformation("FirebaseApp initialized for push notifications.");
            }

            _initialized = true;
        }
    }

    private static GoogleCredential LoadCredential(PushNotificationsOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CredentialsJson))
        {
            return GoogleCredential.FromJson(options.CredentialsJson)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
        }

        if (!string.IsNullOrWhiteSpace(options.CredentialsPath))
        {
            return GoogleCredential.FromFile(options.CredentialsPath)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
        }

        throw new InvalidOperationException("Push notification credentials are not configured.");
    }
}
