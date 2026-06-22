using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Budgets;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.Visits;
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
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

    builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
    builder.Services.AddScoped<AdminUserSeeder>();
    builder.Services.AddScoped<FieldWorkerUserSeeder>();
    builder.Services.AddScoped<PocsoCaseSeeder>();
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
    builder.Services.Configure<PushNotificationsOptions>(
        builder.Configuration.GetSection(PushNotificationsOptions.SectionName));
    builder.Services.Configure<ReportExportOptions>(
        builder.Configuration.GetSection(ReportExportOptions.SectionName));
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
    builder.Services.AddScoped<CrisisQueueService>();
    builder.Services.AddScoped<DashboardService>();
    builder.Services.AddScoped<ReportGenerationService>();
    builder.Services.AddScoped<ReportExportJobRunner>();
    builder.Services.AddScoped<MappingSpecLoader>();
    builder.Services.AddScoped<MigrationImportService>();
    builder.Services.Configure<MappingSpecOptions>(builder.Configuration.GetSection(MappingSpecOptions.SectionName));
    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddHostedService<InterventionOverdueBackgroundService>();
        builder.Services.AddHostedService<CourtReminderBackgroundService>();
        builder.Services.AddHostedService<CourtMissEscalationBackgroundService>();
        builder.Services.AddHostedService<ReportExportBackgroundService>();
    }
    builder.Services.AddScoped<AttachmentService>();
    builder.Services.AddScoped<VisitService>();
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
    builder.Services.AddMidiKavalCors(builder.Configuration, builder.Environment);
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

app.UseMiddleware<RequestIdMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseMiddleware<ApiProblemDetailsMiddleware>();

if (!app.Environment.IsTesting())
{
    app.UseCors(CorsOptions.WebClientPolicy);
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Midi Kaval API v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();

await app.ApplyMigrationsAndSeedAsync();

app.Run();

public partial class Program { }
