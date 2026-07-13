using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TechYouthBpm.Application.Auth;

namespace TechYouthBpm.Tests.Integration;

internal static class IntegrationTestHttp
{
    public static async Task<(TestLoginSession Session, IReadOnlyDictionary<string, string> Cookies)> LoginAsync(
        HttpClient client,
        string username = "admin",
        string password = "admin123",
        bool rememberMe = false)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, password, rememberMe));
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Login failed with {(int)response.StatusCode}: {error}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var session = new TestLoginSession(
            payload.GetProperty("token").GetString() ?? string.Empty,
            payload.GetProperty("csrfToken").GetString() ?? string.Empty,
            payload.GetProperty("user").GetProperty("username").GetString() ?? string.Empty);
        return (session, ReadCookies(response));
    }

    public static HttpRequestMessage BearerRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public static HttpRequestMessage CookieRequest(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string> cookies,
        string? csrfToken = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", string.Join("; ", cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));
        if (!string.IsNullOrWhiteSpace(csrfToken))
        {
            request.Headers.Add("X-CSRF-Token", csrfToken);
        }

        return request;
    }

    public static IReadOnlyDictionary<string, string> ReadCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
                .Select(value => value.Split(';', 2)[0].Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal)
            : new Dictionary<string, string>();
}

internal sealed record TestLoginSession(string Token, string CsrfToken, string Username);
