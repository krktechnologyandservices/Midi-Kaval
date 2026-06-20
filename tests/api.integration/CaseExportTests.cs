using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CaseExportTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CaseExportTests(AuthWebApplicationFactory factory)
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

    [Fact]
    public async Task Export_Xlsx_Returns200_WithContentTypeAndBody()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        var marker = $"XLSX-{Guid.NewGuid():N}";
        request.BeneficiaryName = marker;
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        var response = await CaseTestData.ExportCasesAsync(
            _client,
            session.AccessToken,
            "xlsx",
            new Dictionary<string, string?> { ["q"] = marker });

        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition?.FileName);
        Assert.Matches(@"^cases-export-\d{8}-\d{6}Z\.xlsx$", disposition.FileName.Trim('"'));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }

    [Fact]
    public async Task Export_Pdf_Returns200_WithApplicationPdf()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        var marker = $"PDF-{Guid.NewGuid():N}";
        request.BeneficiaryName = marker;
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        var response = await CaseTestData.ExportCasesAsync(
            _client,
            session.AccessToken,
            "pdf",
            new Dictionary<string, string?> { ["q"] = marker });

        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Export_MatchesSearchFilter()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var marker = $"URBAN-{Guid.NewGuid():N}";

        var urban = CaseTestData.BuildValidRequest();
        urban.Domicile = "Urban";
        urban.BeneficiaryName = marker;
        var urbanCrime = urban.CrimeNumber!;
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, urban);

        var rural = CaseTestData.BuildValidRequest();
        rural.Domicile = "Rural";
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, rural);

        var response = await CaseTestData.ExportCasesAsync(
            _client,
            session.AccessToken,
            "xlsx",
            new Dictionary<string, string?> { ["q"] = marker });

        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var workbook = new XLWorkbook(stream);
        var rows = workbook.Worksheet(1).RowsUsed().Skip(1).ToList();
        Assert.Single(rows);
        Assert.Equal(urbanCrime, rows[0].Cell(1).GetString());
    }

    [Fact]
    public async Task Export_IgnoresPagination_ReturnsAllMatches()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var offenceTag = $"ExportPag-{Guid.NewGuid():N}";

        for (var i = 0; i < 30; i++)
        {
            var request = CaseTestData.BuildValidRequest();
            request.TypeOfOffence = offenceTag;
            await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);
        }

        var response = await CaseTestData.ExportCasesAsync(
            _client,
            session.AccessToken,
            "xlsx",
            new Dictionary<string, string?>
            {
                ["typeOfOffence"] = offenceTag,
                ["pageSize"] = "5",
                ["page"] = "1",
            });

        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var workbook = new XLWorkbook(stream);
        var dataRows = workbook.Worksheet(1).RowsUsed().Skip(1).Count();
        Assert.Equal(30, dataRows);
    }

    [Fact]
    public async Task Export_Pdf_IgnoresPagination_ReturnsAllMatches()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var offenceTag = $"PdfPag-{Guid.NewGuid():N}";

        for (var i = 0; i < 30; i++)
        {
            var request = CaseTestData.BuildValidRequest();
            request.TypeOfOffence = offenceTag;
            await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);
        }

        var response = await CaseTestData.ExportCasesAsync(
            _client,
            session.AccessToken,
            "pdf",
            new Dictionary<string, string?>
            {
                ["typeOfOffence"] = offenceTag,
                ["pageSize"] = "5",
                ["page"] = "1",
            });

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 4096, "PDF export of 30 rows should produce a non-trivial file.");
    }

    [Fact]
    public async Task Export_InvalidFormat_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendExportAsync(_client, session.AccessToken, "csv", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("format", problem?.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_MissingFormat_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cases/search/export");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("format", problem?.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_InvalidDomicile_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendExportAsync(
            _client,
            session.AccessToken,
            "xlsx",
            new Dictionary<string, string?> { ["domicile"] = "Invalid" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("domicile", problem?.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Director_Export_Returns200()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            AuthTestData.Email,
            AuthTestData.Password);
        var request = CaseTestData.BuildValidRequest();
        var marker = $"DIR-{Guid.NewGuid():N}";
        request.BeneficiaryName = marker;
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        var response = await CaseTestData.ExportCasesAsync(
            _client,
            session.AccessToken,
            "xlsx",
            new Dictionary<string, string?> { ["q"] = marker });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SocialWorker_Export_Returns403()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendExportAsync(_client, session.AccessToken, "xlsx", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task CaseWorker_Export_Returns403()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendExportAsync(_client, session.AccessToken, "xlsx", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task DeactivatedCoordinator_Export_Returns403()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var probeClient = _factory.CreateClient();
        var response = await CaseTestData.SendExportAsync(probeClient, session.AccessToken, "xlsx", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(AuthService.DeactivatedMessage, problem?.Detail);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Unauthenticated_Export_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/cases/search/export?format=xlsx");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class CaseExportOverCapWebApplicationFactory : AuthWebApplicationFactory
{
    protected override void ApplyTestConfiguration()
    {
        base.ApplyTestConfiguration();
        Environment.SetEnvironmentVariable("CaseExport__MaxRows", "2");
    }
}

[Collection("AuthIntegrationExportOverCap")]
public class CaseExportOverCapTests : IClassFixture<CaseExportOverCapWebApplicationFactory>, IAsyncLifetime
{
    private readonly CaseExportOverCapWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CaseExportOverCapTests(CaseExportOverCapWebApplicationFactory factory)
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

    [Fact]
    public async Task Export_OverMaxRows_Returns422()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var offenceTag = $"OverCap-{Guid.NewGuid():N}";

        for (var i = 0; i < 3; i++)
        {
            var request = CaseTestData.BuildValidRequest();
            request.TypeOfOffence = offenceTag;
            await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);
        }

        var response = await CaseTestData.SendExportAsync(
            _client,
            session.AccessToken,
            "xlsx",
            new Dictionary<string, string?> { ["typeOfOffence"] = offenceTag });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("refine filters", problem?.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
