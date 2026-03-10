using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Odin.Api.Hubs
{
    /// <summary>
    /// Maps SignalR user identity to the Auth0 "sub" claim so that
    /// <c>Clients.User(identityId)</c> targets the correct connection.
    /// </summary>
    public class UserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            return connection.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? connection.User?.FindFirstValue("sub");
        }
    }
}
