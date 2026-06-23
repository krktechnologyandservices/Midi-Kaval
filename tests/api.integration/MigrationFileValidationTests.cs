using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class MigrationFileValidationTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public MigrationFileValidationTests(AuthWebApplicationFactory factory)
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
        ws.Cell(2, 1).Value = "CR-FV-001";
        ws.Cell(2, 2).Value = "ST-FV-001";
        ws.Cell(2, 3).Value = "Test";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildRenamedExe() => [0x4D, 0x5A, 0x90, 0x00]; // MZ\x90\x00 — PE executable header

    private static byte[] BuildShortFile() => [0x50, 0x4B, 0x03]; // 3 bytes — too short for magic check

    private async Task<string> LoginAsDirectorAsync()
    {
        var director = await AuthTestHelpers.LoginAndVerifyAsync(
            _client, _factory.EmailSender, AuthTestData.Email, AuthTestData.Password);
        return director.AccessToken;
    }

    private async Task<HttpResponseMessage> SendImportAsync(
        string accessToken,
        byte[] fileBytes,
        string fileName = "test.xlsx",
        string? contentType = null)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            contentType ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/migration/import?dryRun=true")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendValidateAsync(
        string accessToken,
        byte[] fileBytes,
        string fileName = "test.xlsx",
        string? contentType = null)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            contentType ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/migration/validate")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    // ── Tests ──

    [Fact]
    public async Task Import_RenamedExe_Returns400()
    {
        var token = await LoginAsDirectorAsync();
        var exeBytes = BuildRenamedExe();

        var response = await SendImportAsync(token, exeBytes, "data.xlsx");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(body);
        Assert.Contains("Invalid file format", body.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Import_ValidExcel_Returns200()
    {
        var token = await LoginAsDirectorAsync();
        var xlsxBytes = BuildValidExcel();

        var response = await SendImportAsync(token, xlsxBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Validate_RenamedExe_Returns400()
    {
        var token = await LoginAsDirectorAsync();
        var exeBytes = BuildRenamedExe();

        var response = await SendValidateAsync(token, exeBytes, "data.xlsx");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(body);
        Assert.Contains("Invalid file format", body.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Import_ShortFile_Returns400()
    {
        var token = await LoginAsDirectorAsync();
        var shortBytes = BuildShortFile();

        var response = await SendImportAsync(token, shortBytes, "data.xlsx");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_ValidExcelWithWrongContentType_Returns200()
    {
        var token = await LoginAsDirectorAsync();
        var xlsxBytes = BuildValidExcel();

        var response = await SendImportAsync(token, xlsxBytes,
            contentType: "application/octet-stream");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
