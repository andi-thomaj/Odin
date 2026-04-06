namespace Odin.Api.Services.Email;

/// <summary>Adds contacts to Resend for marketing (e.g. Broadcasts). Uses the Contacts API with an optional segment id.</summary>
public interface IResendAudienceService
{
    /// <summary>Registers an email in Resend and adds it to the configured marketing segment when <see cref="ResendEmailOptions.AudienceId"/> is set.</summary>
    Task AddContactAsync(string email, string? firstName, string? lastName,
        CancellationToken cancellationToken = default);
}
