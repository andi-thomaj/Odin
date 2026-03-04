using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.UserManagement.Models
{
    public class UpdateUserRoleContract
    {
        public class Request : IValidatableObject
        {
            public required string Role { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                var validRoles = new[] { "User", "Scientist", "Admin" };

                if (string.IsNullOrWhiteSpace(Role))
                {
                    yield return new ValidationResult("Role is required.", [nameof(Role)]);
                }
                else if (!validRoles.Contains(Role, StringComparer.OrdinalIgnoreCase))
                {
                    yield return new ValidationResult(
                        $"Invalid role. Must be one of: {string.Join(", ", validRoles)}.",
                        [nameof(Role)]);
                }
            }
        }

        public class Response
        {
            public int Id { get; set; }
            public required string IdentityId { get; set; }
            public required string Email { get; set; }
            public string Role { get; set; } = string.Empty;
        }
    }
}
