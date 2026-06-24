using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using MidiKaval.Api.Infrastructure.RoleManagement;

namespace MidiKaval.Api.UnitTests.Infrastructure.RoleManagement;

public class TokenServiceTests
{
    private const string SigningKey = "this-is-a-test-signing-key-that-is-32-chars!";

    private static TokenService CreateService(string? key = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ACTIVATION_LINK_SIGNING_KEY"] = key ?? SigningKey,
            })
            .Build();

        return new TokenService(config);
    }

    public class GenerateActivationToken
    {
        [Fact]
        public void ReturnsRawToken_TokenHash_AndSignature()
        {
            var service = CreateService();
            var (rawToken, tokenHash, signature) = service.GenerateActivationToken();

            Assert.NotNull(rawToken);
            Assert.NotNull(tokenHash);
            Assert.NotNull(signature);

            // Raw token should be 64 hex characters (32 bytes)
            Assert.Equal(64, rawToken.Length);
            Assert.Matches("^[0-9a-f]{64}$", rawToken);

            // Token hash should be 64 hex characters (SHA-256 = 32 bytes)
            Assert.Equal(64, tokenHash.Length);
            Assert.Matches("^[0-9a-f]{64}$", tokenHash);
        }

        [Fact]
        public void TokenHashMatchesSha256OfRawToken()
        {
            var service = CreateService();
            var (rawToken, tokenHash, _) = service.GenerateActivationToken();

            var expectedHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

            Assert.Equal(expectedHash, tokenHash);
        }

        [Fact]
        public void GeneratesUniqueTokensEachCall()
        {
            var service = CreateService();
            var (raw1, _, _) = service.GenerateActivationToken();
            var (raw2, _, _) = service.GenerateActivationToken();

            Assert.NotEqual(raw1, raw2);
        }
    }

    public class ValidateSignature
    {
        [Fact]
        public void ValidSignature_ReturnsTrue()
        {
            var service = CreateService();
            var (rawToken, _, signature) = service.GenerateActivationToken();

            Assert.True(service.ValidateSignature(rawToken, signature));
        }

        [Fact]
        public void TamperedToken_ReturnsFalse()
        {
            var service = CreateService();
            var (_, _, signature) = service.GenerateActivationToken();

            Assert.False(service.ValidateSignature("tampered-token-value-1234567890abcdef12345678", signature));
        }

        [Fact]
        public void WrongSignature_ReturnsFalse()
        {
            var service = CreateService();
            var (rawToken, _, _) = service.GenerateActivationToken();

            Assert.False(service.ValidateSignature(rawToken, "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef"));
        }

        [Fact]
        public void DifferentKey_InvalidatesSignature()
        {
            var serviceA = CreateService();
            var serviceB = CreateService("a-different-signing-key-that-is-also-32-chars!");

            var (rawToken, _, signature) = serviceA.GenerateActivationToken();

            Assert.False(serviceB.ValidateSignature(rawToken, signature));
        }
    }

    public class BuildActivationUrl
    {
        [Fact]
        public void BuildsCorrectUrl()
        {
            var service = CreateService();
            var url = service.BuildActivationUrl("http://localhost:4200", "rawtoken123", "sig456");

            Assert.Equal("http://localhost:4200/activate?token=rawtoken123&sig=sig456", url);
        }

        [Fact]
        public void TrimsTrailingSlashFromBaseUrl()
        {
            var service = CreateService();
            var url = service.BuildActivationUrl("http://localhost:4200/", "token", "sig");

            Assert.StartsWith("http://localhost:4200/activate", url);
            Assert.DoesNotContain("//activate", url);
        }
    }

    public class Constructor
    {
        [Fact]
        public void Throws_WhenSigningKeyNotConfigured()
        {
            var config = new ConfigurationBuilder().Build();

            var ex = Assert.Throws<InvalidOperationException>(() => new TokenService(config));
            Assert.Contains("ACTIVATION_LINK_SIGNING_KEY", ex.Message);
        }

        [Fact]
        public void Throws_WhenSigningKeyIsTooShort()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ACTIVATION_LINK_SIGNING_KEY"] = "short",
                })
                .Build();

            var ex = Assert.Throws<InvalidOperationException>(() => new TokenService(config));
            Assert.Contains("at least 32 characters", ex.Message);
        }
    }
}
