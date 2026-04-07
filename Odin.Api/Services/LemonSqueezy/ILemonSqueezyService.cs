namespace Odin.Api.Services.LemonSqueezy;

public interface ILemonSqueezyService
{
    /// <summary>
    /// Creates a Lemon Squeezy checkout and returns the hosted checkout URL.
    /// </summary>
    Task<string> CreateCheckoutAsync(string userId, string? userEmail, string successUrl, CancellationToken cancellationToken = default);
}
