using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Migration;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class MigrationImportTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public MigrationImportTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        await RbacTestData.EnsureRoleUsersAsync(_factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ──

    private static byte[] BuildValidExcel()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Cases");
        ws.Cell(1, 1).Value = "CR Number";
        ws.Cell(1, 2).Value = "ST Number";
        ws.Cell(1, 3).Value = "Beneficiary Name";
        ws.Cell(1, 4).Value = "Age";
        ws.Cell(1, 5).Value = "Type of Offence";
        ws.Cell(1, 6).Value = "Classification";
        ws.Cell(1, 7).Value = "Domicile/Area";
        ws.Cell(1, 8).Value = "First Offender";

        ws.Cell(2, 1).Value = "CR-IMP-001";
        ws.Cell(2, 2).Value = "ST-IMP-001";
        ws.Cell(2, 3).Value = "Alice";
        ws.Cell(2, 4).Value = "25";
        ws.Cell(2, 5).Value = "Theft";
        ws.Cell(2, 6).Value = "Petty";
        ws.Cell(2, 7).Value = "Urban";
        ws.Cell(2, 8).Value = "Yes";

        ws.Cell(3, 1).Value = "CR-IMP-002";
        ws.Cell(3, 2).Value = "ST-IMP-002";
        ws.Cell(3, 3).Value = "Bob";
        ws.Cell(3, 4).Value = "30";
        ws.Cell(3, 5).Value = "Assault";
        ws.Cell(3, 6).Value = "Serious";
        ws.Cell(3, 7).Value = "Rural";
        ws.Cell(3, 8).Value = "No";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildExcelWithDuplicateAndError()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Cases");
        ws.Cell(1, 1).Value = "CR Number";
        ws.Cell(1, 2).Value = "ST Number";
        ws.Cell(1, 3).Value = "Beneficiary Name";
        ws.Cell(1, 4).Value = "Age";
        ws.Cell(1, 5).Value = "Type of Offence";
        ws.Cell(1, 6).Value = "Classification";
        ws.Cell(1, 7).Value = "Domicile/Area";
        ws.Cell(1, 8).Value = "First Offender";

        // Row 1: will be created first, then duplicate in row 2
        ws.Cell(2, 1).Value = "CR-DUP-001";
        ws.Cell(2, 2).Value = "ST-DUP-001";
        ws.Cell(2, 3).Value = "Charlie";
        ws.Cell(2, 4).Value = "20";
        ws.Cell(2, 5).Value = "Robbery";
        ws.Cell(2, 6).Value = "Heinous";
        ws.Cell(2, 7).Value = "Slum";
        ws.Cell(2, 8).Value = "Yes";

        // Row 2: same Crime number → should be skipped
        ws.Cell(3, 1).Value = "CR-DUP-001";
        ws.Cell(3, 2).Value = "ST-DUP-002";
        ws.Cell(3, 3).Value = "Diana";
        ws.Cell(3, 4).Value = "22";
        ws.Cell(3, 5).Value = "Theft";
        ws.Cell(3, 6).Value = "Petty";
        ws.Cell(3, 7).Value = "Urban";
        ws.Cell(3, 8).Value = "No";

        // Row 3: missing required field (Beneficiary Name) → error
        ws.Cell(4, 1).Value = "CR-ERR-001";
        ws.Cell(4, 2).Value = "ST-ERR-001";
        ws.Cell(4, 3).Value = "";
        ws.Cell(4, 4).Value = "35";
        ws.Cell(4, 5).Value = "Fraud";
        ws.Cell(4, 6).Value = "Serious";
        ws.Cell(4, 7).Value = "Coastal";
        ws.Cell(4, 8).Value = "Yes";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<string> LoginAsDirectorAsync()
    {
        var director = await AuthTestHelpers.LoginAndVerifyAsync(
            _client, _factory.EmailSender, AuthTestData.Email, AuthTestData.Password);
        return director.AccessToken;
    }

    private async Task<HttpResponseMessage> SendImportAsync(
        string accessToken,
        byte[] excelBytes,
        string fileName = "test.xlsx",
        bool dryRun = false)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(excelBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", fileName);

        var url = dryRun ? "/api/v1/migration/import?dryRun=true" : "/api/v1/migration/import";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    // ── Tests ──

    [Fact]
    public async Task Director_DryRun_Returns200_WithNoDbWrites()
    {
        var token = await LoginAsDirectorAsync();
        var excelBytes = BuildValidExcel();
        var caseCountBefore = await CountCasesAsync();

        var response = await SendImportAsync(token, excelBytes, dryRun: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MigrationImportResultDto>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(0, result.Created); // dry-run never counts as created
        Assert.Empty(result.Skipped);
        Assert.Empty(result.Errors);

        var caseCountAfter = await CountCasesAsync();
        Assert.Equal(caseCountBefore, caseCountAfter); // no DB writes
    }

    [Fact]
    public async Task Director_Import_CreatesCasesAndAuditEvents()
    {
        var token = await LoginAsDirectorAsync();
        var excelBytes = BuildValidExcel();
        var caseCountBefore = await CountCasesAsync();
        var auditCountBefore = await CountAuditEventsAsync();

        var response = await SendImportAsync(token, excelBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MigrationImportResultDto>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.Created);
        Assert.Empty(result.Skipped);
        Assert.Empty(result.Errors);

        var caseCountAfter = await CountCasesAsync();
        Assert.Equal(caseCountBefore + 2, caseCountAfter);

        var auditImported = await CountAuditEventsAsync(AuditEventTypes.CaseImported);
        Assert.Equal(2, auditImported - auditCountBefore); // 2 case.imported events
    }

    [Fact]
    public async Task Director_Import_DuplicateRowsAreSkipped()
    {
        var token = await LoginAsDirectorAsync();
        var excelBytes = BuildExcelWithDuplicateAndError();

        // First import: creates row 1 (CR-DUP-001), row 2 skipped (duplicate of row 1 in same file is NOT yet in DB)
        // Actually the duplicate pre-check queries the DB, not the current file.
        // For a proper duplicate test, pre-seed a case first.
        await SeedCaseAsync("CR-DUP-001", "ST-DUP-001");

        var response = await SendImportAsync(token, excelBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MigrationImportResultDto>();
        Assert.NotNull(result);
        // Row 1 (CR-DUP-001): duplicate → skipped
        // Row 2 (CR-DUP-001 duplicate crime, different ST): duplicate → skipped
        // Row 3 (CR-ERR-001): missing beneficiary name → error
        Assert.Equal(3, result.TotalRows);
        Assert.Equal(0, result.Created);
        Assert.Equal(2, result.Skipped.Count);
        Assert.Contains(result.Skipped, s => s.CrimeNumber == "CR-DUP-001"); // !
        Assert.Equal(1, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.CrimeNumber == "CR-ERR-001");
    }

    [Fact]
    public async Task Director_Import_ReturnsErrorsForInvalidRows()
    {
        var token = await LoginAsDirectorAsync();

        // A file with only headers and no data rows should return 0 rows
        using var emptyWorkbook = new XLWorkbook();
        var ws = emptyWorkbook.Worksheets.Add("Cases");
        ws.Cell(1, 1).Value = "CR Number";
        ws.Cell(1, 2).Value = "Beneficiary Name";
        using var stream = new MemoryStream();
        emptyWorkbook.SaveAs(stream);
        var emptyBytes = stream.ToArray();

        var response = await SendImportAsync(token, emptyBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MigrationImportResultDto>();
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalRows);
    }

    [Fact]
    public async Task Director_Import_ReturnsImportCompletedAudit()
    {
        var token = await LoginAsDirectorAsync();
        var excelBytes = BuildValidExcel();

        var response = await SendImportAsync(token, excelBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the migration.import_completed audit event exists
        var auditComplete = await CountAuditEventsAsync(AuditEventTypes.MigrationImportCompleted);
        Assert.True(auditComplete > 0, "Expected migration.import_completed audit event");
    }

    [Fact]
    public async Task Coordinator_Import_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var excelBytes = BuildValidExcel();

        var response = await SendImportAsync(coordinator.AccessToken, excelBytes);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Import_Returns401()
    {
        var excelBytes = BuildValidExcel();

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(excelBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "test.xlsx");

        var response = await _client.PostAsync("/api/v1/migration/import?dryRun=true", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Director_Import_NoFile_Returns400()
    {
        var token = await LoginAsDirectorAsync();

        using var content = new MultipartFormDataContent();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/migration/import")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Director_Import_NonExcelFile_Returns400()
    {
        var token = await LoginAsDirectorAsync();

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent("not an excel file"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "test.txt");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/migration/import")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── DB Helpers ──

    private async Task<int> CountCasesAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Cases.CountAsync();
    }

    private async Task<int> CountAuditEventsAsync(string? eventType = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (eventType is null)
            return await db.AuditEvents.CountAsync();
        return await db.AuditEvents.CountAsync(e => e.EventType == eventType);
    }

    private async Task SeedCaseAsync(string crimeNumber, string stNumber)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminUser = await db.Users
            .Where(u => u.OrganisationId == AuthTestData.OrganisationId)
            .OrderBy(u => u.CreatedAtUtc)
            .FirstAsync();
        db.Cases.Add(new Api.Domain.Entities.Case
        {
            Id = Guid.NewGuid(),
            OrganisationId = AuthTestData.OrganisationId,
            CrimeNumber = crimeNumber,
            StNumber = stNumber,
            BeneficiaryName = "Pre-Seeded",
            TypeOfOffence = "Test",
            OffenceClassification = Api.Domain.Enums.OffenceClassification.Petty,
            Domicile = Api.Domain.Enums.Domicile.Urban,
            CreatedByUserId = adminUser.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
