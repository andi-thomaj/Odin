using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.UserManagement.Models
{
    public class CreateUserContract
    {
        public class Request : IValidatableObject
        {
            public required string IdentityId { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public required string Email { get; set; }
            public string? Username { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (string.IsNullOrWhiteSpace(IdentityId))
                {
                    yield return new ValidationResult("Identity ID is required.", [nameof(IdentityId)]);
                }

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

                if (string.IsNullOrWhiteSpace(Email))
                {
                    yield return new ValidationResult("Email is required.", [nameof(Email)]);
                }

                if (!new EmailAddressAttribute().IsValid(Email))
                {
                    yield return new ValidationResult("Email format is invalid.", [nameof(Email)]);
                }
            }
        }

        public class Response
        {
            public int Id { get; set; }
            public required string IdentityId { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public required string Email { get; set; }
            public string Role { get; set; } = string.Empty;
            public bool IsNewUser { get; set; }
        }
    }
}
