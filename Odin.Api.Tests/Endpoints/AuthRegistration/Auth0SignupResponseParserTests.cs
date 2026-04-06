using Odin.Api.Endpoints.AuthRegistration;

namespace Odin.Api.Tests.Endpoints.AuthRegistration;

public class Auth0SignupResponseParserTests
{
    [Fact]
    public void ParseIdentityId_FromUserId_ReturnsTrimmed()
    {
        const string json = """{"user_id":"auth0|abc123","email":"a@b.co"}""";
        Assert.Equal("auth0|abc123", Auth0SignupResponseParser.ParseIdentityIdFromSuccessBody(json));
    }

    [Fact]
    public void ParseIdentityId_FromUnderscoreId_PrefixesAuth0()
    {
        const string json = """{"_id":"507f1f77bcf86cd799439011","email":"a@b.co"}""";
        Assert.Equal("auth0|507f1f77bcf86cd799439011", Auth0SignupResponseParser.ParseIdentityIdFromSuccessBody(json));
    }

    [Fact]
    public void ParseIdentityId_FromUnderscoreIdAlreadyPrefixed_ReturnsAsIs()
    {
        const string json = """{"_id":"auth0|already"}""";
        Assert.Equal("auth0|already", Auth0SignupResponseParser.ParseIdentityIdFromSuccessBody(json));
    }
}
