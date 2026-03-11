using System.Net;
using System.Text.Json;

namespace Odin.Api.Services
{
    public record GeoLocationResult(string Country, string CountryCode);

    public interface IGeoLocationService
    {
        Task<GeoLocationResult?> GetCountryFromIpAsync(string? ipAddress);
    }

    public class GeoLocationService(HttpClient httpClient, ILogger<GeoLocationService> logger) : IGeoLocationService
    {
        public async Task<GeoLocationResult?> GetCountryFromIpAsync(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return null;

            var isLoopback = IPAddress.TryParse(ipAddress, out var ip) &&
                             (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.IPv6Loopback));

            var url = isLoopback
                ? "https://ipwho.is/"
                : $"https://ipwho.is/{ipAddress}";

            try
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                if (json.TryGetProperty("success", out var success) && success.GetBoolean() &&
                    json.TryGetProperty("country", out var country) &&
                    json.TryGetProperty("country_code", out var countryCode))
                {
                    return new GeoLocationResult(
                        country.GetString() ?? string.Empty,
                        countryCode.GetString() ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resolve country for IP {IpAddress}", ipAddress);
            }

            return null;
        }
    }
}
