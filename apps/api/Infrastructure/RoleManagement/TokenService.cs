using System.Security.Cryptography;
using System.Text;

namespace MidiKaval.Api.Infrastructure.RoleManagement;

public sealed class TokenService
{
    private readonly byte[] _signingKey;
    private const int TokenByteLength = 32;

    public TokenService(IConfiguration configuration)
    {
        var key = configuration["ACTIVATION_LINK_SIGNING_KEY"]
            ?? throw new InvalidOperationException("ACTIVATION_LINK_SIGNING_KEY is not configured.");
        if (key.Length < 32)
            throw new InvalidOperationException("ACTIVATION_LINK_SIGNING_KEY must be at least 32 characters.");
        _signingKey = Encoding.UTF8.GetBytes(key);
    }

    public (string rawToken, string tokenHash, string signature) GenerateActivationToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        var rawToken = Convert.ToHexString(randomBytes).ToLowerInvariant();
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

        using var hmac = new HMACSHA256(_signingKey);
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
        return (rawToken, tokenHash, sig);
    }

    public bool ValidateSignature(string rawToken, string signature)
    {
        using var hmac = new HMACSHA256(_signingKey);
        var expectedSig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSig),
            Encoding.UTF8.GetBytes(signature));
    }

    public string BuildActivationUrl(string baseUrl, string rawToken, string signature) =>
        $"{baseUrl.TrimEnd('/')}/activate?token={rawToken}&sig={signature}";

    public string BuildInvitationUrl(string baseUrl, string rawToken, string signature) =>
        $"{baseUrl.TrimEnd('/')}/invite?token={rawToken}&sig={signature}";
}
