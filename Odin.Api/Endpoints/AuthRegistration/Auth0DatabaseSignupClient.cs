using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Odin.Api.Endpoints.AuthRegistration;

public sealed class Auth0DatabaseSignupClient(
    HttpClient httpClient,
    IOptions<Auth0SignupOptions> options) : IAuth0DatabaseSignupClient
{
    private readonly Auth0SignupOptions _options = options.Value;

    public async Task<Auth0SignupOutcome> SignupAsync(Auth0SignupPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SpaClientId))
            return Auth0SignupOutcome.Failed("configuration_error", "Auth0 SpaClientId is not configured.");

        var trimmedMiddle = string.IsNullOrWhiteSpace(payload.MiddleName) ? null : payload.MiddleName.Trim();

        var metadata = new Dictionary<string, string>
        {
            ["first_name"] = payload.FirstName.Trim(),
            ["last_name"] = payload.LastName.Trim(),
        };
        if (trimmedMiddle is not null)
            metadata["middle_name"] = trimmedMiddle;

        var body = new
        {
            client_id = _options.SpaClientId,
            email = payload.Email.Trim(),
            password = payload.Password,
            connection = _options.DatabaseConnection,
            username = payload.Username.Trim(),
            name = $"{payload.FirstName.Trim()} {payload.LastName.Trim()}".Trim(),
            user_metadata = metadata,
        };

        using var response = await httpClient.PostAsJsonAsync("dbconnections/signup", body, cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var identityId = Auth0SignupResponseParser.ParseIdentityIdFromSuccessBody(json);
            if (string.IsNullOrEmpty(identityId))
                return Auth0SignupOutcome.Failed("invalid_response", "Auth0 signup response missing user id.");

            return Auth0SignupOutcome.Ok(identityId);
        }

        return ParseAuth0Error(json);
    }

    private static Auth0SignupOutcome ParseAuth0Error(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("code", out var codeEl))
            {
                var code = codeEl.GetString();
                var desc = root.TryGetProperty("description", out var d) ? d.GetString() : null;
                return Auth0SignupOutcome.Failed(code, desc ?? json);
            }

            if (root.TryGetProperty("error", out var errEl))
            {
                var err = errEl.GetString();
                var desc = root.TryGetProperty("error_description", out var ed) ? ed.GetString() : null;
                return Auth0SignupOutcome.Failed(err, desc ?? json);
            }

            if (root.TryGetProperty("message", out var msgEl))
                return Auth0SignupOutcome.Failed("error", msgEl.GetString() ?? json);
        }
        catch (JsonException)
        {
            // fall through
        }

        return Auth0SignupOutcome.Failed("signup_failed", json.Length > 500 ? json[..500] : json);
    }
}
