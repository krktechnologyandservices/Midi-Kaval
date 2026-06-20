using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Notifications;
using MidiKaval.Api.Models.TravelClaims;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class TravelClaimNotificationApiTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public TravelClaimNotificationApiTests(AuthWebApplicationFactory factory)
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
    public async Task Approve_WithoutComment_UsesStandardBodyWithDestinationAndAmount()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        await CaseTestData.ApproveTravelClaimAsync(_client, director.AccessToken, submitted.Id);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = await db.InAppNotifications.SingleAsync(
            n => n.UserId == submitted.ClaimantUserId
                && n.EventType == NotificationEventTypes.TravelClaimApproved
                && n.ResourceId == submitted.Id);

        Assert.Equal("Travel claim approved", notification.Title);
        Assert.Equal(
            $"Your claim for {submitted.Destination} (₹{submitted.Amount:0.##}) was approved.",
            notification.Body);
        Assert.DoesNotContain("Director note:", notification.Body, StringComparison.Ordinal);

        var fieldSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);
        var list = await CaseTestData.ListNotificationsAsync(_client, fieldSession.AccessToken);
        var dto = Assert.Single(list.Items, n => n.ResourceId == submitted.Id);
        Assert.Equal(NotificationEventTypes.TravelClaimApproved, dto.EventType);
        Assert.Equal("TravelClaim", dto.ResourceType);
    }

    [Fact]
    public async Task Approve_WithComment_IncludesDirectorNoteLine()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        const string comment = "Approved for June visit";
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        await CaseTestData.ApproveTravelClaimAsync(
            _client,
            director.AccessToken,
            submitted.Id,
            new ApproveTravelClaimRequest { Comment = comment });

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = await db.InAppNotifications.SingleAsync(
            n => n.UserId == submitted.ClaimantUserId
                && n.EventType == NotificationEventTypes.TravelClaimApproved
                && n.ResourceId == submitted.Id);

        Assert.Equal("Travel claim approved", notification.Title);
        Assert.Contains(submitted.Destination, notification.Body, StringComparison.Ordinal);
        Assert.Contains($"₹{submitted.Amount:0.##}", notification.Body, StringComparison.Ordinal);
        Assert.Contains($"Director note: {comment}", notification.Body, StringComparison.Ordinal);
        Assert.NotEqual(comment, notification.Body);
    }

    [Fact]
    public async Task Return_IncludesDestinationAmountAndDirectorNote()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        const string comment = "Receipt unclear";
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        await CaseTestData.ReturnTravelClaimAsync(
            _client,
            director.AccessToken,
            submitted.Id,
            new ReturnTravelClaimRequest { Comment = comment });

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = await db.InAppNotifications.SingleAsync(
            n => n.UserId == submitted.ClaimantUserId
                && n.EventType == NotificationEventTypes.TravelClaimReturned
                && n.ResourceId == submitted.Id);

        Assert.Equal("Travel claim returned", notification.Title);
        Assert.Contains(submitted.Destination, notification.Body, StringComparison.Ordinal);
        Assert.Contains($"₹{submitted.Amount:0.##}", notification.Body, StringComparison.Ordinal);
        Assert.Contains($"Director note: {comment}", notification.Body, StringComparison.Ordinal);
        Assert.NotEqual(comment, notification.Body);

        var fieldSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);
        var list = await CaseTestData.ListNotificationsAsync(_client, fieldSession.AccessToken);
        var dto = Assert.Single(list.Items, n => n.ResourceId == submitted.Id);
        Assert.Equal(NotificationEventTypes.TravelClaimReturned, dto.EventType);
        Assert.Equal("TravelClaim", dto.ResourceType);
    }

    [Fact]
    public async Task MonthlyTotals_Approve_IncludesAmount_Return_ExcludesAmount()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail,
            transportMode: "Other");

        var claimDate = submitted.ClaimDate;
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        await CaseTestData.ApproveTravelClaimAsync(_client, director.AccessToken, submitted.Id);

        var afterApprove = await CaseTestData.ListTravelClaimMonthlyTotalsAsync(
            _client,
            coordinator.AccessToken,
            claimDate.Year,
            claimDate.Month);
        var approvedRow = Assert.Single(afterApprove.Items);
        Assert.Equal(submitted.Amount, approvedRow.TotalAmount);
        Assert.Equal(1, approvedRow.ClaimCount);

        var returnedClaim = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail,
            transportMode: "Other");

        await CaseTestData.ReturnTravelClaimAsync(
            _client,
            director.AccessToken,
            returnedClaim.Id,
            new ReturnTravelClaimRequest { Comment = "Resubmit with clearer receipt" });

        var afterReturn = await CaseTestData.ListTravelClaimMonthlyTotalsAsync(
            _client,
            coordinator.AccessToken,
            returnedClaim.ClaimDate.Year,
            returnedClaim.ClaimDate.Month);

        var staffRow = Assert.Single(afterReturn.Items);
        Assert.Equal(submitted.Amount, staffRow.TotalAmount);
        Assert.Equal(1, staffRow.ClaimCount);
    }
}
