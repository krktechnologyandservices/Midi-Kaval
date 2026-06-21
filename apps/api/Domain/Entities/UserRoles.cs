namespace MidiKaval.Api.Domain.Entities;

public static class UserRoles
{
    public const string Director = "Director";
    public const string Coordinator = "Coordinator";
    public const string SocialWorker = "SocialWorker";
    public const string CaseWorker = "CaseWorker";
    public const string Accountant = "Accountant";

    public static readonly string[] All = [Director, Coordinator, SocialWorker, CaseWorker, Accountant];

    public static bool IsValid(string role) => All.Contains(role);
}
