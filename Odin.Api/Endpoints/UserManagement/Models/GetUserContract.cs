using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.UserManagement.Models
{
    public class GetUserContract
    {
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
            public DateTime CreatedAt { get; set; }
        }
    }
}
