using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.UserManagement.Models
{
    public class UpdateUserContract
    {
        public class Request : IValidatableObject
        {
            public string? FirstName { get; set; }
            public string? MiddleName { get; set; }
            public string? LastName { get; set; }
            public string? Username { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (string.IsNullOrWhiteSpace(FirstName))
                {
                    yield return new ValidationResult("First name is required.", [nameof(FirstName)]);
                }

                if (FirstName?.Length < 2)
                {
                    yield return new ValidationResult("First name must be at least 2 characters.", [nameof(FirstName)]);
                }

                if (FirstName?.Length > 100)
                {
                    yield return new ValidationResult("First name must not exceed 100 characters.",
                        [nameof(FirstName)]);
                }

                if (MiddleName is not null && MiddleName.Length > 100)
                {
                    yield return new ValidationResult("Middle name must not exceed 100 characters.",
                        [nameof(MiddleName)]);
                }

                if (string.IsNullOrWhiteSpace(LastName))
                {
                    yield return new ValidationResult("Last name is required.", [nameof(LastName)]);
                }

                if (LastName?.Length < 2)
                {
                    yield return new ValidationResult("Last name must be at least 2 characters.", [nameof(LastName)]);
                }

                if (LastName?.Length > 100)
                {
                    yield return new ValidationResult("Last name must not exceed 100 characters.", [nameof(LastName)]);
                }

                if (string.IsNullOrWhiteSpace(Username))
                {
                    yield return new ValidationResult("Username is required.", [nameof(Username)]);
                }

                if (Username?.Length < 2)
                {
                    yield return new ValidationResult("Username must be at least 2 characters.", [nameof(Username)]);
                }

                if (Username?.Length > 100)
                {
                    yield return new ValidationResult("Username must not exceed 100 characters.", [nameof(Username)]);
                }
            }
        }

        public class Response
        {
            public int Id { get; set; }
            public required string IdentityId { get; set; }
            public string Username { get; set; } = string.Empty;
            public required string Email { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string MiddleName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
        }
    }
}
