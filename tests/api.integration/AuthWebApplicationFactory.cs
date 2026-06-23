using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Notifications;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace MidiKaval.Api.IntegrationTests;

public class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private readonly AzuriteContainer _azurite = new AzuriteBuilder()
        .WithImage("mcr.microsoft.com/azure-storage/azurite:3.34.0")
        .Build();

    private bool _containersStarted;

    public FakeEmailSender EmailSender { get; } = new();

    public FakePushNotificationSender PushSender { get; } =
        new(NullLogger<FakePushNotificationSender>.Instance);

    protected override IHost CreateHost(IHostBuilder builder)
    {
        StartContainersAsync().GetAwaiter().GetResult();
        ApplyTestConfiguration();
        return base.CreateHost(builder);
    }

    protected virtual void ApplyTestConfiguration()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("BlobStorage__ConnectionString", _azurite.GetConnectionString());
        Environment.SetEnvironmentVariable("BlobStorage__ContainerName", "attachments");
        Environment.SetEnvironmentVariable("BlobStorage__SasExpiryMinutes", "15");
        Environment.SetEnvironmentVariable("BlobStorage__MaxUploadBytes", "10485760");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "test-issuer");
        Environment.SetEnvironmentVariable("Jwt__Audience", "test-audience");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", new string('k', 32));
        Environment.SetEnvironmentVariable("Auth__RateLimitPermitLimit", "1000");
        Environment.SetEnvironmentVariable("Auth__RateLimitWindowSeconds", "60");
        Environment.SetEnvironmentVariable("DataRateLimiting__ReadPermitLimit", "10000");
        Environment.SetEnvironmentVariable("DataRateLimiting__WritePermitLimit", "10000");
        Environment.SetEnvironmentVariable("DataRateLimiting__WindowSeconds", "60");
        Environment.SetEnvironmentVariable("CaseAnonymizationJob__RetentionYears", "1");
        Environment.SetEnvironmentVariable("CaseAnonymizationJob__BatchSize", "10");
        Environment.SetEnvironmentVariable("CaseAnonymizationJob__IntervalHours", "24");
        Environment.SetEnvironmentVariable("Seed__Admin__Email", AuthTestData.Email);
        Environment.SetEnvironmentVariable("Seed__Admin__Password", AuthTestData.Password);
        Environment.SetEnvironmentVariable("Seed__OrganisationId", AuthTestData.OrganisationId.ToString());
        Environment.SetEnvironmentVariable("CaseExport__MaxRows", "5000");
        Environment.SetEnvironmentVariable("EncryptionKey__Provider", "Environment");

        // Generate a random 32-byte test key at runtime (not a hardcoded literal)
        var testKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Environment.SetEnvironmentVariable("EncryptionKey__Versions__0", testKey);
        Environment.SetEnvironmentVariable("EncryptionKey__ActiveKeyVersion", "0");
    }

    private async Task StartContainersAsync()
    {
        if (_containersStarted)
        {
            return;
        }

        await Task.WhenAll(
            _postgres.StartAsync(),
            _redis.StartAsync(),
            _azurite.StartAsync());
        _containersStarted = true;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            var emailDescriptors = services.Where(d => d.ServiceType == typeof(IEmailSender)).ToList();
            foreach (var descriptor in emailDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IEmailSender>(EmailSender);

            var pushDescriptors = services.Where(d => d.ServiceType == typeof(IPushNotificationSender)).ToList();
            foreach (var descriptor in pushDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IPushNotificationSender>(PushSender);
        });
    }
}

internal static class AuthTestData
{
    public const string Email = "director@pilot.example";
    public const string Password = "TestDirectorPassword123!";
    public static readonly Guid OrganisationId = Guid.Parse("00000000-0000-4000-8000-000000000001");
}
