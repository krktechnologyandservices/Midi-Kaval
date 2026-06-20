namespace MidiKaval.Api.Infrastructure.Auth;

public interface IUserSessionService
{
    Task InvalidateUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
