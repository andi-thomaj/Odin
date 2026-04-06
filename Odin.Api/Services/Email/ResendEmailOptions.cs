namespace Odin.Api.Services.Email;

public sealed class ResendEmailOptions
{
    public const string SectionName = "Resend";

    /// <summary>Resend API key (Bearer).</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Sender address for transactional/marketing email sends (must be a verified domain in Resend).</summary>
    public string FromEmail { get; set; } = "";

    public string FromName { get; set; } = "Ancestrify";

    /// <summary>Resend segment id for marketing contacts (POST /contacts <c>segments</c>).</summary>
    public string AudienceId { get; set; } = "";
}
