using Odin.Api.Services.Email;

namespace Odin.Api.IntegrationTests.Fakers;

public sealed class NoOpResendAudienceService : IResendAudienceService
{
    public Task AddContactAsync(string email, string? firstName, string? lastName,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
