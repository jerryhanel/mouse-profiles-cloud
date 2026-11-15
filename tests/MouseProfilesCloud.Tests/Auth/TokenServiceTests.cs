using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MouseProfilesCloud.Models;
using MouseProfilesCloud.Options;
using MouseProfilesCloud.Services;
using Ext = Microsoft.Extensions.Options.Options;

namespace MouseProfilesCloud.Tests.Auth;

public class TokenServiceTests
{
    private static readonly JwtOptions TestJwt = new()
    {
        Secret = "test-secret-that-is-long-enough-for-hmac",
        Issuer = "TestIssuer",
        Audience = "TestAudience",
    };

    private TokenService CreateSut(FakeTimeProvider time) =>
        new(Ext.Create(TestJwt), time);

    [Fact]
    public void CreateToken_ExpiryIsSevenDaysFromNow()
    {
        var now = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider();
        time.SetUtcNow(now);

        var user = new AppUser { Id = Guid.NewGuid(), Email = "user@example.com" };
        var rawToken = CreateSut(time).CreateToken(user);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(rawToken);
        Assert.Equal(now.UtcDateTime.AddDays(7), parsed.ValidTo, TimeSpan.FromSeconds(1));
    }
}
