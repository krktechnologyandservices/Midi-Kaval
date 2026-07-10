using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.HttpOverrides;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Budgets;
using MidiKaval.Api.Infrastructure.Middleware;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.Visits;
using MidiKaval.Api.Infrastructure.Geocoding;
using MidiKaval.Api.Infrastructure.Sync;
using MidiKaval.Api.Infrastructure.Reports;
using MidiKaval.Api.Infrastructure.Storage;
using MidiKaval.Api.Infrastructure.Supervisor;
using MidiKaval.Api.Infrastructure.TravelClaims;
using MidiKaval.Api.Infrastructure.Users;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Migration;
using MidiKaval.Api.Jobs;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.Seed;
using MidiKaval.Api.Models;
using MidiKaval.Api.Infrastructure.RoleManagement;
using MidiKaval.Api.Domain.RoleManagement;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Explicitly wire up user secrets rather than relying on WebApplication.CreateBuilder's
// implicit auto-detection (which depends on Assembly.GetEntryAssembly() resolving correctly
// and can silently no-op depending on how the process was launched).
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

QuestPDF.Settings.License = LicenseType.Community;

builder.Services.Configure<CaseExportOptions>(
    builder.Configuration.GetSection(CaseExportOptions.SectionName));

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ApiEnvelopeFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

if (!builder.Environment.IsTesting())
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
    connectionString = PostgresConnectionStringNormalizer.Normalize(connectionString);

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

    builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
    builder.Services.AddScoped<AdminUserSeeder>();
    builder.Services.AddScoped<FieldWorkerUserSeeder>();
    builder.Services.AddScoped<PocsoCaseSeeder>();
    builder.Services.AddScoped<VendorUserSeeder>();
    builder.Services.AddScoped<AccountMigrationService>();
    builder.Services.AddScoped<CaseService>();
    builder.Services.AddScoped<CaseNoteService>();
    builder.Services.AddScoped<InterventionService>();
    builder.Services.AddScoped<CourtSittingService>();
    builder.Services.AddScoped<CourtMissFlagService>();
    builder.Services.AddScoped<TravelClaimService>();
    builder.Services.Configure<InterventionOverdueJobOptions>(
        builder.Configuration.GetSection(InterventionOverdueJobOptions.SectionName));
    builder.Services.Configure<CourtReminderJobOptions>(
        builder.Configuration.GetSection(CourtReminderJobOptions.SectionName));
    builder.Services.Configure<CourtMissEscalationJobOptions>(
        builder.Configuration.GetSection(CourtMissEscalationJobOptions.SectionName));
    builder.Services.Configure<BudgetThresholdJobOptions>(
        builder.Configuration.GetSection(BudgetThresholdJobOptions.SectionName));
    builder.Services.Configure<AuditDigestJobOptions>(
        builder.Configuration.GetSection(AuditDigestJobOptions.SectionName));
    builder.Services.Configure<PushNotificationsOptions>(
        builder.Configuration.GetSection(PushNotificationsOptions.SectionName));
    builder.Services.Configure<ReportExportOptions>(
        builder.Configuration.GetSection(ReportExportOptions.SectionName));
    builder.Services.AddOptions<CaseAnonymizationJobOptions>()
        .Bind(builder.Configuration.GetSection(CaseAnonymizationJobOptions.SectionName))
        .ValidateOnStart();
    builder.Services.AddSingleton<IValidateOptions<CaseAnonymizationJobOptions>, CaseAnonymizationJobOptionsValidator>();
    builder.Services.AddSingleton<FakePushNotificationSender>();
    var pushOptions = builder.Configuration
        .GetSection(PushNotificationsOptions.SectionName)
        .Get<PushNotificationsOptions>() ?? new PushNotificationsOptions();
    if (pushOptions.IsConfigured())
    {
        builder.Services.AddSingleton<IPushNotificationSender, FirebasePushNotificationSender>();
    }
    else
    {
        builder.Services.AddSingleton<IPushNotificationSender>(sp =>
            sp.GetRequiredService<FakePushNotificationSender>());
    }

    builder.Services.AddScoped<NotificationService>();
    builder.Services.AddScoped<PushDeliveryService>();
    builder.Services.AddScoped<EmailDeliveryService>();
    builder.Services.AddScoped<UserDeviceService>();
    builder.Services.AddScoped<InterventionOverdueJobRunner>();
    builder.Services.AddScoped<CourtReminderJobRunner>();
    builder.Services.AddScoped<CourtMissEscalationJobRunner>();
    builder.Services.AddScoped<BudgetThresholdJobRunner>();
    builder.Services.AddScoped<CrisisQueueService>();
    builder.Services.AddScoped<DashboardService>();
    builder.Services.AddScoped<ReportGenerationService>();
    builder.Services.AddScoped<ReportExportJobRunner>();
    builder.Services.AddScoped<CaseAnonymizationJobRunner>();
    builder.Services.AddScoped<AuditDigestJobRunner>();
    builder.Services.AddScoped<MappingSpecLoader>();
    builder.Services.AddScoped<MigrationImportService>();
    builder.Services.Configure<MappingSpecOptions>(builder.Configuration.GetSection(MappingSpecOptions.SectionName));
    // Report exports and budget-threshold checks are triggered by a specific user action
    // (clicking "Export", crossing a utilization threshold) rather than firing repeatedly on
    // a timer, so — unlike the reminder/escalation jobs below — there's no downside to running
    // them in Development. Gating them out here silently left "Export" stuck on Pending forever
    // for any developer running the API locally via `dotnet run`.
    builder.Services.AddHostedService<ReportExportBackgroundService>();
    builder.Services.AddHostedService<BudgetThresholdMonitorBackgroundService>();
    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddHostedService<InterventionOverdueBackgroundService>();
        builder.Services.AddHostedService<CourtReminderBackgroundService>();
        builder.Services.AddHostedService<CourtMissEscalationBackgroundService>();
        builder.Services.AddHostedService<CaseAnonymizationBackgroundService>();
        builder.Services.AddHostedService<AuditDigestBackgroundService>();
    }
    builder.Services.AddScoped<AttachmentService>();
    builder.Services.AddScoped<VisitService>();
    // Nominatim's usage policy requires a genuine identifying User-Agent on every
    // request — set it here since browsers refuse to let client-side JS set this header,
    // which is why address search is proxied through this API instead of called directly.
    builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(client =>
    {
        client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MidiKaval-CaseManagement/1.0");
    });
    builder.Services.AddScoped<SyncPushService>();
    builder.Services.AddScoped<CaseSearchPresetService>();
    builder.Services.AddScoped<UserQueryService>();
    builder.Services.AddScoped<CaseStage2DataService>();
    builder.Services.AddScoped<CaseStage3DataService>();
    builder.Services.AddScoped<CaseStage4DataService>();
    builder.Services.AddScoped<CaseStage5DataService>();
    builder.Services.AddScoped<CaseStage6DataService>();
    builder.Services.AddScoped<CaseRelatedCasesService>();
    builder.Services.AddScoped<BudgetService>();
    builder.Services.AddScoped<BudgetUtilizationService>();
    builder.Services.AddScoped<IBudgetReportExportService, BudgetReportExcelService>();
    builder.Services.AddScoped<SocioDemographicProfileService>();
    builder.Services.AddScoped<SocioDemographicProfileExcelService>();
    builder.Services.AddBlobStorage(builder.Configuration);
    builder.Services.AddMidiKavalAuth(builder.Configuration);
    builder.Services.AddMidiKavalDataRateLimiting(builder.Configuration);
    builder.Services.AddMidiKavalSecurity(builder.Configuration);
    builder.Services.AddMidiKavalCors(builder.Configuration, builder.Environment);

    // Vendor backstage services
    builder.Services.AddSingleton<MidiKaval.Api.Infrastructure.RoleManagement.TokenService>();
    builder.Services.AddScoped<MidiKaval.Api.Domain.RoleManagement.OrganisationService>();
    builder.Services.AddScoped<MidiKaval.Api.Domain.RoleManagement.RegistrationService>();
    builder.Services.AddScoped<MidiKaval.Api.Domain.RoleManagement.LastDirectorGuard>();
    builder.Services.AddScoped<MidiKaval.Api.Domain.RoleManagement.ZeroDirectorTriggerService>();
    builder.Services.AddScoped<MidiKaval.Api.Domain.RoleManagement.UserManagementService>();
    builder.Services.AddScoped<InvitationService>();
    builder.Services.AddScoped<ActivationEmailDeliveryJob>();
    builder.Services.AddScoped<InvitationEmailDeliveryJob>();
    builder.Services.AddScoped<InvitationCleanupJob>();
    builder.Services.AddScoped<InvitationResendNotificationJob>();
    builder.Services.AddScoped<ZeroDirectorAlertJob>();
    builder.Services.AddScoped<ZeroDirectorMonitorJob>();
    builder.Services.AddScoped<ConfirmationEmailDeliveryJob>();
    builder.Services.AddScoped<Legacy2faMigrationJob>();

    // Hangfire background jobs
    var hangfireConnectionString = builder.Configuration.GetConnectionString("Hangfire");
    builder.Services.AddHangfire(config =>
    {
        config.UseSimpleAssemblyNameTypeSerializer()
              .UseRecommendedSerializerSettings();

        if (!string.IsNullOrWhiteSpace(hangfireConnectionString))
        {
            config.UsePostgreSqlStorage(opts =>
                    opts.UseNpgsqlConnection(PostgresConnectionStringNormalizer.Normalize(hangfireConnectionString)));
        }
        else
        {
            config.UseInMemoryStorage();
        }
    });
    builder.Services.AddHangfireServer();
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Midi Kaval API",
        Version = "v1",
        Description = "Kaval Online case management platform REST API.",
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// EncryptionKeyProvider is only ever consumed via its static GetCurrent() bridge (EF Core
// value converters can't use DI), so nothing else in the app triggers DI to resolve this
// singleton. Force resolution here so SetCurrent() runs before the first request.
app.Services.GetRequiredService<MidiKaval.Api.Infrastructure.Encryption.EncryptionKeyProvider>();

app.UseMiddleware<RequestIdMiddleware>();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    // TODO: Configure KnownNetworks or KnownProxies before production deployment.
    // Without these, any client can spoof X-Forwarded-For and bypass IP-based rate limiting.
    // Example for a Docker/nginx setup on 172.x.x.x:
    //   KnownNetworks = { new IPNetwork(IPAddress.Parse("172.16.0.0"), 12) }
    // Example for a single proxy IP:
    //   KnownProxies = { IPAddress.Parse("10.0.0.100") }
    // For Azure App Service / AWS ELB the platform handles forwarding — leave empty.
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseMiddleware<ApiProblemDetailsMiddleware>();

if (!app.Environment.IsTesting())
{
    app.UseMiddleware<ContentSecurityPolicyMiddleware>();
    app.UseCors(CorsOptions.WebClientPolicy);
    app.UseAuthentication();
    app.UseMiddleware<MidiKaval.Api.Infrastructure.Middleware.TokenVersionMiddleware>();
    app.UseMiddleware<MidiKaval.Api.Infrastructure.Middleware.SuspendedUserMiddleware>();
    app.UseAuthorization();
    app.UseRateLimiter();
    app.UseHangfireDashboard();

    // Register recurring jobs
    RecurringJob.AddOrUpdate<InvitationCleanupJob>(
        "invitation-cleanup",
        j => j.ExecuteAsync(CancellationToken.None),
        "0 2 * * *"); // Daily at 2am
    RecurringJob.AddOrUpdate<ZeroDirectorMonitorJob>(
        "zero-director-monitor",
        j => j.ExecuteAsync(CancellationToken.None),
        "0 * * * *"); // Every hour
    RecurringJob.AddOrUpdate<Legacy2faMigrationJob>(
        "legacy-2fa-migration",
        j => j.ExecuteAsync(CancellationToken.None),
        "0 * * * *"); // Every hour — cursor-based idempotency, max 100/hr
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Midi Kaval API v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();

await app.ApplyMigrationsAndSeedAsync();

// Migration cutover: set RUN_MIGRATION=1 env var to migrate config-file accounts to DB
if (Environment.GetEnvironmentVariable("RUN_MIGRATION") == "1")
{
    try
    {
        await using var migrateScope = app.Services.CreateAsyncScope();
        var migrationService = migrateScope.ServiceProvider.GetRequiredService<AccountMigrationService>();
        await migrationService.RunAsync(app.Lifetime.ApplicationStopping);
    }
    catch (OperationCanceledException)
    {
        app.Logger.LogInformation("Account migration was cancelled during shutdown.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Account migration failed. The app will continue startup — re-run with RUN_MIGRATION=1 to retry.");
    }
}

app.Run();

public partial class Program { }
