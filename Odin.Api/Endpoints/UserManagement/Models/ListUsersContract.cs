namespace Odin.Api.Endpoints.UserManagement.Models;

public static class ListUsersContract
{
    public sealed class UserItem
    {
        public int Id { get; init; }
        public required string IdentityId { get; init; }
        public string Username { get; init; } = string.Empty;
        public required string Email { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }

    public sealed class Response
    {
        public List<UserItem> Items { get; init; } = [];
        public int TotalCount { get; init; }
        public int Skip { get; init; }
        public int Take { get; init; }
    }
}
