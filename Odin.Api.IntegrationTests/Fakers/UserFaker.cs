using Bogus;
using Odin.Api.Endpoints.UserManagement.Models;

namespace Odin.Api.IntegrationTests.Fakers;

public static class UserFaker
{
    private static int _identityCounter;

    private static readonly Faker<CreateUserContract.Request> RequestFaker = new Faker<CreateUserContract.Request>()
        .RuleFor(r => r.IdentityId, _ => $"auth0|test-{Interlocked.Increment(ref _identityCounter):D6}")
        .RuleFor(r => r.FirstName, f => f.Name.FirstName())
        .RuleFor(r => r.LastName, f => f.Name.LastName())
        .RuleFor(r => r.Email, (f, r) => f.Internet.Email(r.FirstName, r.LastName))
        .RuleFor(r => r.Username, (f, r) => f.Internet.UserName(r.FirstName, r.LastName));

    public static CreateUserContract.Request GenerateCreateRequest() => RequestFaker.Generate();

    public static CreateUserContract.Request GenerateCreateRequest(Action<CreateUserContract.Request> customize)
    {
        var request = RequestFaker.Generate();
        customize(request);
        return request;
    }

    public static UpdateUserContract.Request GenerateUpdateRequest(Faker? faker = null)
    {
        var f = faker ?? new Faker();
        return new UpdateUserContract.Request
        {
            FirstName = f.Name.FirstName(),
            LastName = f.Name.LastName(),
            Username = f.Internet.UserName()
        };
    }
}
