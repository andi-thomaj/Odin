using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Services;
using Odin.Api.Services.Email;

namespace Odin.Api.Endpoints.AuthRegistration;

public sealed class AuthRegistrationService(
    ApplicationDbContext dbContext,
    IAuth0DatabaseSignupClient auth0Signup,
    IGeoLocationService geoLocationService,
    IResendAudienceService resendAudience,
    ILogger<AuthRegistrationService> logger) : IAuthRegistrationService
{
    public async Task<IResult> RegisterAsync(RegisterContract.Request request, string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var emailNorm = request.Email.Trim().ToLowerInvariant();
        var usernameNorm = request.Username.Trim().ToLowerInvariant();

        var emailTaken = await dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == emailNorm, cancellationToken);
        if (emailTaken)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(RegisterContract.Request.Email)] = ["This email is already registered."],
            });
        }

        var usernameTaken = await dbContext.Users
            .AnyAsync(u => u.Username.ToLower() == usernameNorm, cancellationToken);
        if (usernameTaken)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(RegisterContract.Request.Username)] = ["This username is already taken."],
            });
        }

        var signupPayload = new Auth0SignupPayload
        {
            Email = request.Email.Trim(),
            Password = request.Password,
            Username = request.Username.Trim(),
            FirstName = request.FirstName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim(),
            LastName = request.LastName.Trim(),
        };

        var auth0Result = await auth0Signup.SignupAsync(signupPayload, cancellationToken);
        if (!auth0Result.Success || string.IsNullOrEmpty(auth0Result.IdentityId))
        {
            var code = auth0Result.ErrorCode ?? "signup_failed";
            var desc = auth0Result.ErrorDescription ?? "Registration failed.";
            logger.LogWarning("Auth0 signup failed: {Code} {Description}", code, desc);

            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["auth0"] = [MapAuth0Message(code, desc)],
            });
        }

        var identityId = auth0Result.IdentityId;
        var middle = string.IsNullOrWhiteSpace(request.MiddleName) ? string.Empty : request.MiddleName.Trim();

        try
        {
            var geoResult = await geoLocationService.GetCountryFromIpAsync(ipAddress);

            var user = new User
            {
                IdentityId = identityId,
                Username = request.Username.Trim(),
                Email = request.Email.Trim(),
                FirstName = request.FirstName.Trim(),
                MiddleName = middle,
                LastName = request.LastName.Trim(),
                Role = AppRole.User,
                Country = geoResult?.Country,
                CountryCode = geoResult?.CountryCode,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = identityId,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = identityId,
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                await resendAudience.AddContactAsync(user.Email, user.FirstName, user.LastName, cancellationToken);
            }
            catch (Exception marketingEx)
            {
                logger.LogError(marketingEx,
                    "Could not add marketing contact for new user {Email}. Registration succeeded.",
                    user.Email);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Database insert failed after successful Auth0 signup for IdentityId {IdentityId}. User may exist in Auth0 without an application_users row.",
                identityId);
            return Results.Problem(
                detail: "Account was created in identity provider but profile storage failed. Please contact support.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(new RegisterContract.Response());
    }

    private static string MapAuth0Message(string code, string description)
    {
        if (code.Equals("user_exists", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            return "An account with this email already exists.";

        if (code.Equals("invalid_password", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("password", StringComparison.OrdinalIgnoreCase))
            return description;

        return description;
    }
}
