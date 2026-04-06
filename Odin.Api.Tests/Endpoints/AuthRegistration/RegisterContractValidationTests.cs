using System.ComponentModel.DataAnnotations;
using Odin.Api.Endpoints.AuthRegistration;

namespace Odin.Api.Tests.Endpoints.AuthRegistration;

public class RegisterContractValidationTests
{
    [Fact]
    public void Password_WithoutDigit_IsInvalid()
    {
        var req = ValidBaseRequest();
        req.Password = "Onlyletters";
        Assert.Contains(nameof(RegisterContract.Request.Password), MemberNamesWithErrors(req));
    }

    [Fact]
    public void Password_WithoutLetter_IsInvalid()
    {
        var req = ValidBaseRequest();
        req.Password = "12345678";
        Assert.Contains(nameof(RegisterContract.Request.Password), MemberNamesWithErrors(req));
    }

    [Fact]
    public void Password_TooShort_IsInvalid()
    {
        var req = ValidBaseRequest();
        req.Password = "Ab1";
        Assert.Contains(nameof(RegisterContract.Request.Password), MemberNamesWithErrors(req));
    }

    [Fact]
    public void Password_Valid_IsAccepted()
    {
        var req = ValidBaseRequest();
        req.Password = "Password1";
        Assert.Empty(MemberNamesWithErrors(req));
    }

    private static RegisterContract.Request ValidBaseRequest() =>
        new()
        {
            Username = "testuser",
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@test.local",
            Password = "Password1",
        };

    private static HashSet<string> MemberNamesWithErrors(RegisterContract.Request req)
    {
        var results = req.Validate(new ValidationContext(req)).ToList();
        return results.SelectMany(r => r.MemberNames).ToHashSet(StringComparer.Ordinal);
    }
}
