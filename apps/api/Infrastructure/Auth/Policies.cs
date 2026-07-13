namespace MidiKaval.Api.Infrastructure.Auth;

public static class Policies
{
    public const string DirectorOnly = nameof(DirectorOnly);
    public const string CoordinatorOrAbove = nameof(CoordinatorOrAbove);
    public const string FieldWorker = nameof(FieldWorker);
    public const string Director = nameof(Director);
    public const string Coordinator = nameof(Coordinator);
    public const string SocialWorker = nameof(SocialWorker);
    public const string CaseWorker = nameof(CaseWorker);
    public const string Accountant = nameof(Accountant);
    public const string AccountantOrAbove = nameof(AccountantOrAbove);
    public const string BudgetViewer = nameof(BudgetViewer);
    public const string VendorOnly = nameof(VendorOnly);
    public const string DirectorOrVendor = nameof(DirectorOrVendor);
    public const string FieldWorkerOrCoordinatorOrAbove = nameof(FieldWorkerOrCoordinatorOrAbove);

    public const string ForbiddenByRoleMessage =
        "You do not have permission to perform this action.";
}
