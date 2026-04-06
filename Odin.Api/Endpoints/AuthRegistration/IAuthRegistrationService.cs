namespace Odin.Api.Endpoints.AuthRegistration;

public interface IAuthRegistrationService
{
    Task<IResult> RegisterAsync(RegisterContract.Request request, string? ipAddress,
        CancellationToken cancellationToken = default);
}
