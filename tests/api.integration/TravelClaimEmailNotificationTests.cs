using MidiKaval.Api.Infrastructure.Email.Templates;
using MidiKaval.Api.Models.TravelClaims;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class TravelClaimEmailNotificationTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public TravelClaimEmailNotificationTests(AuthWebApplicationFactory factory)
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
    public async Task SubmitTravelClaim_EmailsCoordinatorWithClaimDetails()
    {
        var (claim, _) = await CaseTestData.CreateAssignedCaseAndTravelClaimAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var fieldSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var content = "receipt-bytes"u8.ToArray();
        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            fieldSession.AccessToken,
            CaseTestData.BuildPresignRequest(
                claim.Id,
                resourceType: "TravelClaim",
                fileSizeBytes: content.Length));

        await CaseTestData.PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, content);
        await CaseTestData.ConfirmAttachmentAsync(_client, fieldSession.AccessToken, presign.AttachmentId);

        _factory.EmailSender.Clear();
        await CaseTestData.SubmitTravelClaimAsync(_client, fieldSession.AccessToken, claim.Id);

        var coordinatorEmail = Assert.Single(
            _factory.EmailSender.Messages,
            m => m.To == RbacTestData.CoordinatorEmail);
        Assert.Contains("Travel claim submitted", coordinatorEmail.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(RbacTestData.SocialWorkerEmail, coordinatorEmail.Body, StringComparison.Ordinal);
        Assert.Contains(claim.Destination, coordinatorEmail.Body, StringComparison.Ordinal);
        Assert.Contains(EmailTemplateFooter.Line, coordinatorEmail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("beneficiary", coordinatorEmail.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveTravelClaim_EmailsCoordinatorSupervisorCopy_NotClaimant()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        _factory.EmailSender.Clear();
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        await CaseTestData.ApproveTravelClaimAsync(
            _client,
            director.AccessToken,
            submitted.Id,
            new ApproveTravelClaimRequest { Comment = "Looks good" });

        var coordinatorEmail = Assert.Single(
            _factory.EmailSender.Messages,
            m => m.To == RbacTestData.CoordinatorEmail);
        Assert.Contains("approved", coordinatorEmail.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"Claim from {RbacTestData.SocialWorkerEmail}", coordinatorEmail.Body, StringComparison.Ordinal);
        Assert.Contains("Director note: Looks good", coordinatorEmail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            _factory.EmailSender.Messages,
            m => m.To == RbacTestData.SocialWorkerEmail);
    }
}
