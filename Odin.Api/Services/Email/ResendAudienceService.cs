using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Odin.Api.Services.Email;

public sealed class ResendAudienceService(
    HttpClient httpClient,
    IOptions<ResendEmailOptions> options,
    ILogger<ResendAudienceService> logger) : IResendAudienceService
{
    private readonly ResendEmailOptions _options = options.Value;

    public async Task AddContactAsync(string email, string? firstName, string? lastName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogWarning("Resend:ApiKey is not configured; skipping marketing contact for {To}", email);
            return;
        }

        if (string.IsNullOrWhiteSpace(email))
            return;

        using var request = new HttpRequestMessage(HttpMethod.Post, "contacts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());

        var body = new ResendCreateContactRequest
        {
            Email = email.Trim(),
            FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim(),
            Unsubscribed = false,
        };

        if (!string.IsNullOrWhiteSpace(_options.AudienceId))
            body.Segments = [new ResendSegmentRef { Id = _options.AudienceId.Trim() }];

        request.Content = JsonContent.Create(body);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Resend contacts API error {Status}: {Body}", (int)response.StatusCode, responseBody);
            throw new InvalidOperationException("Failed to add marketing contact.");
        }
    }

    private sealed class ResendCreateContactRequest
    {
        [JsonPropertyName("email")]
        public required string Email { get; init; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; init; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; init; }

        [JsonPropertyName("unsubscribed")]
        public bool Unsubscribed { get; init; }

        [JsonPropertyName("segments")]
        public ResendSegmentRef[]? Segments { get; set; }
    }

    private sealed class ResendSegmentRef
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }
    }
}
