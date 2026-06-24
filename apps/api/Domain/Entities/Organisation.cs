namespace MidiKaval.Api.Domain.Entities;

public class Organisation
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<ActivationToken> ActivationTokens { get; set; } = new List<ActivationToken>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
