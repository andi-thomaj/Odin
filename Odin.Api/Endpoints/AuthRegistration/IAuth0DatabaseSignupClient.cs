namespace Odin.Api.Endpoints.AuthRegistration;

public interface IAuth0DatabaseSignupClient
{
    Task<Auth0SignupOutcome> SignupAsync(Auth0SignupPayload payload, CancellationToken cancellationToken = default);
}

public sealed class Auth0SignupPayload
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string Username { get; init; }
    public required string FirstName { get; init; }
    public string? MiddleName { get; init; }
    public required string LastName { get; init; }
}

public sealed class Auth0SignupOutcome
{
    public bool Success { get; init; }
    public string? IdentityId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDescription { get; init; }

    public static Auth0SignupOutcome Ok(string identityId) =>
        new() { Success = true, IdentityId = identityId };

    public static Auth0SignupOutcome Failed(string? code, string? description) =>
        new() { Success = false, ErrorCode = code, ErrorDescription = description };
}
