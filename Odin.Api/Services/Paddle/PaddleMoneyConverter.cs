using System.Globalization;

namespace Odin.Api.Services.Paddle;

/// <summary>
/// Converts Paddle money strings (smallest currency unit, e.g. <c>"4999"</c>) to <c>decimal</c>
/// in the major unit. Paddle's amount precision depends on the currency: most use 2 minor units
/// (USD, EUR, GBP, …), some use 0 (JPY, KRW). We map only the well-known zero-decimal currencies
/// and default everything else to 2.
/// </summary>
public static class PaddleMoneyConverter
{
    public static decimal ToDecimalMajorUnit(string? amount, string? currencyCode)
    {
        if (string.IsNullOrEmpty(amount))
            return 0m;

        if (!decimal.TryParse(amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var raw))
            return 0m;

        var minor = MinorUnits(currencyCode);
        return minor == 0 ? raw : raw / (decimal)Math.Pow(10, minor);
    }

    private static int MinorUnits(string? currencyCode) => currencyCode?.ToUpperInvariant() switch
    {
        // ISO-4217 currencies with 0 minor units.
        "JPY" or "KRW" or "VND" or "ISK" or "HUF" or "CLP" or "PYG" or "RWF" or "UGX" or "XAF" or "XOF" or "XPF" => 0,
        _ => 2,
    };
}
