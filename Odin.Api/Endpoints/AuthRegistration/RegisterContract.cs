using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.AuthRegistration;

public static class RegisterContract
{
    public class Request : IValidatableObject
    {
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Username))
                yield return new ValidationResult("Username is required.", [nameof(Username)]);

            if (Username?.Length < 2)
                yield return new ValidationResult("Username must be at least 2 characters.", [nameof(Username)]);

            if (Username?.Length > 100)
                yield return new ValidationResult("Username must not exceed 100 characters.", [nameof(Username)]);

            if (string.IsNullOrWhiteSpace(FirstName))
                yield return new ValidationResult("First name is required.", [nameof(FirstName)]);

            if (FirstName?.Length < 2)
                yield return new ValidationResult("First name must be at least 2 characters.", [nameof(FirstName)]);

            if (FirstName?.Length > 100)
                yield return new ValidationResult("First name must not exceed 100 characters.", [nameof(FirstName)]);

            if (MiddleName is not null && MiddleName.Length > 100)
                yield return new ValidationResult("Middle name must not exceed 100 characters.", [nameof(MiddleName)]);

            if (string.IsNullOrWhiteSpace(LastName))
                yield return new ValidationResult("Last name is required.", [nameof(LastName)]);

            if (LastName?.Length < 2)
                yield return new ValidationResult("Last name must be at least 2 characters.", [nameof(LastName)]);

            if (LastName?.Length > 100)
                yield return new ValidationResult("Last name must not exceed 100 characters.", [nameof(LastName)]);

            if (string.IsNullOrWhiteSpace(Email))
                yield return new ValidationResult("Email is required.", [nameof(Email)]);

            if (!new EmailAddressAttribute().IsValid(Email))
                yield return new ValidationResult("Email format is invalid.", [nameof(Email)]);

            if (string.IsNullOrWhiteSpace(Password))
                yield return new ValidationResult("Password is required.", [nameof(Password)]);

            if (Password is not null)
            {
                if (Password.Length < 8)
                    yield return new ValidationResult("Password must be at least 8 characters.", [nameof(Password)]);

                if (Password.Length > 128)
                    yield return new ValidationResult("Password must not exceed 128 characters.", [nameof(Password)]);

                var hasLetter = Password.Any(char.IsLetter);
                var hasDigit = Password.Any(char.IsDigit);
                if (!hasLetter || !hasDigit)
                    yield return new ValidationResult(
                        "Password must contain at least one letter and one number.",
                        [nameof(Password)]);
            }
        }
    }

    public class Response
    {
        public string Message { get; set; } = "Registration successful.";
    }
}
