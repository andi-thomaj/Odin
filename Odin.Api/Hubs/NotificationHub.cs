using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Odin.Api.Hubs
{
    [Authorize(Policy = "EmailVerified")]
    public class NotificationHub : Hub
    {
        /// <summary>
        /// SignalR group every admin connection joins on connect, so admin-only live signals (e.g. the App Store
        /// purchases feed, which carries customer/amount detail) target ONLY admins and never broadcast sensitive
        /// data to every authenticated client. The hub authorizes any EmailVerified user, so <c>Clients.All</c>
        /// would reach non-admins — admin payloads must use this group instead.
        /// </summary>
        public const string AdminGroup = "admins";

        public override async Task OnConnectedAsync()
        {
            // The role enrichment middleware stamps an `app_role` claim onto the principal during the connect
            // request, so admins can be routed into the admin group here. Fail-closed: a missing claim just
            // means no live admin pushes for that connection (it still catches up on the next page load).
            if (string.Equals(Context.User?.FindFirstValue("app_role"), "Admin", StringComparison.OrdinalIgnoreCase))
                await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // SignalR removes a dropped connection from all its groups automatically — no manual cleanup needed.
            await base.OnDisconnectedAsync(exception);
        }
    }
}
