using System.Security.Cryptography;
using System.Text;

namespace MidiKaval.Api.Infrastructure.Auth;

public static class OtpHasher
{
    public static string Hash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(string code, string hash) =>
        string.Equals(Hash(code), hash, StringComparison.OrdinalIgnoreCase);
}
