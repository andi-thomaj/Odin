using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;

namespace Odin.Api.Services.LemonSqueezy;

public sealed class LemonSqueezyService(HttpClient httpClient, IOptions<LemonSqueezyOptions> options) : ILemonSqueezyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public async Task<string> CreateCheckoutAsync(
        string userId,
        string? userEmail,
        string successUrl,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            throw new InvalidOperationException("LemonSqueezy:ApiKey is not configured.");

        var body = BuildCheckoutPayload(cfg, userId, userEmail, successUrl);
        var json = JsonSerializer.Serialize(body, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.lemonsqueezy.com/v1/checkouts");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg.ApiKey);
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/vnd.api+json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var url = doc.RootElement
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("url")
            .GetString();

        return url ?? throw new InvalidOperationException("Lemon Squeezy returned a checkout without a URL.");
    }

    private static object BuildCheckoutPayload(LemonSqueezyOptions cfg, string userId, string? email, string successUrl)
    {
        return new
        {
            data = new
            {
                type = "checkouts",
                attributes = new
                {
                    checkout_data = new
                    {
                        email = email ?? "",
                        custom = new { user_id = userId }
                    },
                    product_options = new
                    {
                        redirect_url = successUrl,
                        enabled_variants = new[] { int.Parse(cfg.VariantId) }
                    }
                },
                relationships = new
                {
                    store = new { data = new { type = "stores", id = cfg.StoreId } },
                    variant = new { data = new { type = "variants", id = cfg.VariantId } }
                }
            }
        };
    }
}
