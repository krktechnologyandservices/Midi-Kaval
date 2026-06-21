using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Migration;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class MigrationValidationTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public MigrationValidationTests(AuthWebApplicationFactory factory)
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
        ws.Cell(1, 1).Value = "Case ID";
        ws.Cell(1, 2).Value = "CR Number";
        ws.Cell(1, 3).Value = "ST Number";
        ws.Cell(1, 4).Value = "Beneficiary Name";
        ws.Cell(1, 5).Value = "Age";
        ws.Cell(1, 6).Value = "Contact No";
        ws.Cell(1, 7).Value = "Address";
        ws.Cell(1, 8).Value = "Type of Offence";
        ws.Cell(1, 9).Value = "Classification";
        ws.Cell(1, 10).Value = "Domicile/Area";
        ws.Cell(1, 11).Value = "First Offender";
        ws.Cell(1, 12).Value = "Stage";
        ws.Cell(1, 13).Value = "Assigned Worker";
        ws.Cell(1, 14).Value = "Date Registered";
        ws.Cell(1, 15).Value = "Remarks";

        ws.Cell(2, 1).Value = "KAV-001";
        ws.Cell(2, 2).Value = "CR-2024-001";
        ws.Cell(2, 3).Value = "ST-2024-001";
        ws.Cell(2, 4).Value = "Test Beneficiary";
        ws.Cell(2, 5).Value = "16";
        ws.Cell(2, 6).Value = "9876543210";
        ws.Cell(2, 7).Value = "Test Address";
        ws.Cell(2, 8).Value = "Theft";
        ws.Cell(2, 9).Value = "Petty";
        ws.Cell(2, 10).Value = "Urban";
        ws.Cell(2, 11).Value = "Yes";
        ws.Cell(2, 12).Value = "Initial Assessment";
        ws.Cell(2, 13).Value = "Worker A";
        ws.Cell(2, 14).Value = "2024-01-15";
        ws.Cell(2, 15).Value = "";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildExcelMissingRequiredColumns()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Cases");
        // Only 3 columns — missing CR Number, ST Number, Beneficiary Name, Type of Offence, Classification, Domicile
        ws.Cell(1, 1).Value = "Case ID";
        ws.Cell(1, 2).Value = "Address";
        ws.Cell(1, 3).Value = "Remarks";
        ws.Cell(2, 1).Value = "KAV-001";
        ws.Cell(2, 2).Value = "Some address";
        ws.Cell(2, 3).Value = "Some remarks";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildExcelWithTypeMismatch()
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

        // Age is text "N/A" — type mismatch
        ws.Cell(2, 1).Value = "CR-001";
        ws.Cell(2, 2).Value = "ST-001";
        ws.Cell(2, 3).Value = "Test";
        ws.Cell(2, 4).Value = "N/A";
        ws.Cell(2, 5).Value = "Theft";
        ws.Cell(2, 6).Value = "Petty";
        ws.Cell(2, 7).Value = "Urban";
        ws.Cell(2, 8).Value = "Yes";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildEmptyExcel()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Empty");
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<HttpResponseMessage> SendValidateAsync(
        string accessToken,
        byte[] excelBytes,
        string fileName = "test.xlsx")
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(excelBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
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
    public async Task Director_ValidExcel_Returns200_WithValidationResult()
    {
        var director = await AuthTestHelpers.LoginAndVerifyAsync(
            _client, _factory.EmailSender, AuthTestData.Email, AuthTestData.Password);
        var excelBytes = BuildValidExcel();

        var response = await SendValidateAsync(director.AccessToken, excelBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MigrationValidationResultDto>();
        Assert.NotNull(result);
        Assert.Equal(15, result.TotalColumns);
        Assert.True(result.MatchedColumns > 0);
        Assert.Equal(0, result.MissingRequiredFields);
    }

    [Fact]
    public async Task Director_MissingRequiredColumns_Returns200_WithMissingFields()
    {
        var director = await AuthTestHelpers.LoginAndVerifyAsync(
            _client, _factory.EmailSender, AuthTestData.Email, AuthTestData.Password);
        var excelBytes = BuildExcelMissingRequiredColumns();

        var response = await SendValidateAsync(director.AccessToken, excelBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MigrationValidationResultDto>();
        Assert.NotNull(result);
        Assert.True(result.MissingRequiredFields > 0, "Expected missing required fields to be reported");
        Assert.Contains(result.MissingFields, m => m.Severity == "error");
    }

    [Fact]
    public async Task Director_TypeMismatch_Returns200_WithWarnings()
    {
        var director = await AuthTestHelpers.LoginAndVerifyAsync(
            _client, _factory.EmailSender, AuthTestData.Email, AuthTestData.Password);
        var excelBytes = BuildExcelWithTypeMismatch();

        var response = await SendValidateAsync(director.AccessToken, excelBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MigrationValidationResultDto>();
        Assert.NotNull(result);
        Assert.True(result.DataTypeWarnings > 0, "Expected data type warnings for Age column");
    }

    [Fact]
    public async Task Director_EmptyFile_Returns200_WithZeroColumns()
    {
        var director = await AuthTestHelpers.LoginAndVerifyAsync(
            _client, _factory.EmailSender, AuthTestData.Email, AuthTestData.Password);
        var excelBytes = BuildEmptyExcel();

        var response = await SendValidateAsync(director.AccessToken, excelBytes);

        // A worksheet with only a header row and no data columns still parses as 0 columns — that's acceptable
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MigrationValidationResultDto>();
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalColumns);
    }

    [Fact]
    public async Task Director_NonExcelFile_Returns400()
    {
        var director = await AuthTestHelpers.LoginAndVerifyAsync(
            _client, _factory.EmailSender, AuthTestData.Email, AuthTestData.Password);

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent("not an excel file"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "test.txt");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/migration/validate")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", director.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
    }

    [Fact]
    public async Task Coordinator_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var excelBytes = BuildValidExcel();

        var response = await SendValidateAsync(coordinator.AccessToken, excelBytes);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var excelBytes = BuildValidExcel();

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(excelBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "test.xlsx");

        var response = await _client.PostAsync("/api/v1/migration/validate", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Director_NoFile_Returns400()
    {
        var director = await AuthTestHelpers.LoginAndVerifyAsync(
            _client, _factory.EmailSender, AuthTestData.Email, AuthTestData.Password);

        using var content = new MultipartFormDataContent();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/migration/validate")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", director.AccessToken);
        var response = await _client.SendAsync(request);

        // Empty multipart will reach controller with null file
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
