namespace Odin.Api.Authentication;

/// <summary>
/// Authentication scheme name for the cookie that gates the Hangfire dashboard at <c>/jobs</c>.
/// Admins mint this cookie by POSTing to <c>/v1/api/admin/hangfire/session</c> with a valid JWT;
/// the cookie is then sent automatically by the browser when the dashboard tab is opened.
/// </summary>
public static class HangfireAuthScheme
{
    public const string Name = "HangfireCookie";
}
