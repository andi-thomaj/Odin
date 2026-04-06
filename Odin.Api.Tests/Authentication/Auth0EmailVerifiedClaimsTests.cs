using System.Security.Claims;
using Odin.Api.Authentication;

namespace Odin.Api.Tests.Authentication;

public class Auth0EmailVerifiedClaimsTests
{
    [Theory]
    [InlineData("email_verified", "true", "true")]
    [InlineData("email_verified", "True", "true")]
    [InlineData("email_verified", "false", "false")]
    [InlineData("http://schemas.auth0.com/email_verified", "true", "true")]
    public void GetAppEmailVerifiedValue_FromClaims(string type, string value, string expected)
    {
        var p = new ClaimsPrincipal(new ClaimsIdentity([new Claim(type, value)]));
        Assert.Equal(expected, Auth0EmailVerifiedClaims.GetAppEmailVerifiedValue(p));
    }

    [Fact]
    public void GetAppEmailVerifiedValue_MissingClaim_IsFalse()
    {
        var p = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "x")]));
        Assert.Equal("false", Auth0EmailVerifiedClaims.GetAppEmailVerifiedValue(p));
    }
}
