namespace TechYouthBpm.Api;

internal static class AuthCookieNames
{
    public const string AccessToken = "techyouth_access";
    public const string RefreshToken = "techyouth_refresh";
    public const string CsrfToken = "techyouth_csrf";
    public const string CsrfHeader = "X-CSRF-Token";
}
