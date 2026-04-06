using Odin.Api.Endpoints.AuthRegistration;

namespace Odin.Api.IntegrationTests.Fakers;

/// <summary>
/// Avoids calling real Auth0 during integration tests.
/// </summary>
public sealed class FakeAuth0DatabaseSignupClient : IAuth0DatabaseSignupClient
{
    private int _counter;

    public Task<Auth0SignupOutcome> SignupAsync(Auth0SignupPayload payload, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _counter);
        return Task.FromResult(Auth0SignupOutcome.Ok($"auth0|test-reg-{id:D6}"));
    }
}
