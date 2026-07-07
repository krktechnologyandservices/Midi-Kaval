using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.Reports;
using MidiKaval.Api.Infrastructure.Storage;

namespace MidiKaval.Api.Jobs;

public sealed class ReportExportJobRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<ReportExportOptions> options,
    ILogger<ReportExportJobRunner> logger)
{
    private static readonly HttpClient UploadHttpClient = new();
    public async Task RunAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reportService = scope.ServiceProvider.GetRequiredService<ReportGenerationService>();
        var blobService = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var emailService = scope.ServiceProvider.GetRequiredService<EmailDeliveryService>();

        var pending = await db.Set<ReportExportJob>()
            .Where(j => j.Status == ReportExportJobStatus.Pending)
            .OrderBy(j => j.CreatedAtUtc)
            .ToListAsync(ct);

        foreach (var job in pending)
        {
            try
            {
                job.Status = ReportExportJobStatus.Processing;
                await db.SaveChangesAsync(ct);

                var reportType = Domain.Enums.ReportTypeExtensions.FromApiString(job.ReportType);
                if (reportType is null)
                {
                    job.Status = ReportExportJobStatus.Failed;
                    job.ErrorMessage = $"Unknown report type: {job.ReportType}";
                    job.CompletedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                var rows = await reportService.BuildReportAsync(
                    reportType.Value,
                    job.OrganisationId,
                    job.FromDate,
                    job.ToDate,
                    job.Year,
                    ct);

                var title = reportService.ReportTypeToDisplayName(reportType.Value);
                var fileBytes = await reportService.GenerateFileAsync(rows, job.Format, title);

                var extension = job.Format == "pdf" ? ".pdf" : ".xlsx";
                var contentType = job.Format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                var blobName = $"{options.Value.BlobContainerPrefix}/{job.OrganisationId}/{job.Id:D}{extension}";

                // Upload blob via SAS URI. Azure Blob/Azurite reject a PUT Blob request that's
                // missing x-ms-blob-type, so it must be set explicitly here — every other
                // uploader in this codebase (web/mobile attachment uploads) already sends it.
                var (uploadUrl, _) = blobService.GenerateUploadSasUri(blobName, contentType);
                using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
                {
                    Content = new ByteArrayContent(fileBytes),
                };
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                request.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
                var uploadResponse = await UploadHttpClient.SendAsync(request, ct);
                uploadResponse.EnsureSuccessStatusCode();

                job.Status = ReportExportJobStatus.Completed;
                job.BlobPath = blobName;
                job.CompletedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                // Create in-app notification
                var notification = new InAppNotification
                {
                    Id = Guid.NewGuid(),
                    OrganisationId = job.OrganisationId,
                    UserId = job.CreatedByUserId,
                    EventType = NotificationEventTypes.ReportExportReady,
                    Title = "Report ready",
                    Body = $"{title} export is ready for download.",
                    ResourceType = "ReportExportJob",
                    ResourceId = job.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                db.InAppNotifications.Add(notification);
                await db.SaveChangesAsync(ct);

                // Send email
                await emailService.TrySendReportExportReadyAsync(
                    job.CreatedByUserId,
                    job.OrganisationId,
                    title,
                    DateTime.UtcNow.AddMinutes(options.Value.SasUrlExpiryMinutes),
                    ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Report export job {JobId} failed", job.Id);
                job.Status = ReportExportJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
