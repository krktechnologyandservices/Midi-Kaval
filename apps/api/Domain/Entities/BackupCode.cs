namespace MidiKaval.Api.Domain.Entities;

public class BackupCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public bool Used { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
