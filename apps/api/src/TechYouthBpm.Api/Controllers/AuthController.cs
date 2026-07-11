using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TechYouthBpm.Api;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ApiControllerBase(authService)
{
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await AuthService.RegisterAsync(request, cancellationToken);
        return result.IsSuccess ? Created($"/api/users/{result.Value!.Id}", result.Value) : ValidationProblem(result.Errors);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await AuthService.LoginAsync(
            request,
            ResolveClientIpAddress(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        if (result.IsSuccess)
        {
            AppendAuthCookies(result.Value!);
        }

        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies.TryGetValue(AuthCookieNames.RefreshToken, out var cookieRefreshToken)
            ? cookieRefreshToken
            : string.Empty;
        var result = await AuthService.RefreshSessionAsync(
            refreshToken,
            ResolveClientIpAddress(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        if (result.IsSuccess)
        {
            AppendAuthCookies(result.Value!);
        }
        else
        {
            ClearAuthCookies();
        }

        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        return user is null ? UnauthorizedProblem() : Ok(user);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await AuthService.LogoutAsync(CurrentToken(), cancellationToken);
        ClearAuthCookies();
        return result.IsSuccess ? NoContent() : ValidationProblem(result.Errors);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await AuthService.ForgotPasswordAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await AuthService.ResetPasswordAsync(request, cancellationToken);
        return result.IsSuccess ? NoContent() : ValidationProblem(result.Errors);
    }

    [HttpPatch("me/profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await AuthService.UpdateProfileAsync(request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await AuthService.ChangePasswordAsync(request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await AuthService.ListSessionsAsync(user, CurrentToken(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await AuthService.RevokeSessionAsync(sessionId, user, CurrentToken(), cancellationToken);
        return result.IsSuccess ? NoContent() : ValidationProblem(result.Errors);
    }

    [HttpPost("me/email-verification")]
    public async Task<IActionResult> StartEmailVerification(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await AuthService.StartEmailVerificationAsync(user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost("me/email-verification/confirm")]
    public async Task<IActionResult> ConfirmEmailVerification(
        EmailVerificationConfirmRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await AuthService.ConfirmEmailVerificationAsync(request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("public-email-verification/start")]
    public async Task<IActionResult> StartPublicEmailVerification(
        PublicEmailVerificationStartRequest request,
        CancellationToken cancellationToken)
    {
        var result = await AuthService.StartPublicEmailVerificationAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("public-email-verification/confirm")]
    public async Task<IActionResult> ConfirmPublicEmailVerification(
        PublicEmailVerificationConfirmRequest request,
        CancellationToken cancellationToken)
    {
        var result = await AuthService.ConfirmPublicEmailVerificationAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    private void AppendAuthCookies(LoginResponse session)
    {
        var accessOptions = BuildCookieOptions(session.ExpiresAt, httpOnly: true);
        Response.Cookies.Append(AuthCookieNames.AccessToken, session.Token, accessOptions);

        if (!string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            Response.Cookies.Append(
                AuthCookieNames.RefreshToken,
                session.RefreshToken,
                BuildCookieOptions(session.RefreshTokenExpiresAt ?? session.ExpiresAt, httpOnly: true));
        }

        if (!string.IsNullOrWhiteSpace(session.CsrfToken))
        {
            Response.Cookies.Append(
                AuthCookieNames.CsrfToken,
                session.CsrfToken,
                BuildCookieOptions(session.ExpiresAt, httpOnly: false));
        }
    }

    private void ClearAuthCookies()
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps
        };
        Response.Cookies.Delete(AuthCookieNames.AccessToken, options);
        Response.Cookies.Delete(AuthCookieNames.RefreshToken, options);
        Response.Cookies.Delete(
            AuthCookieNames.CsrfToken,
            new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            });
    }

    private CookieOptions BuildCookieOptions(DateTime expiresAt, bool httpOnly)
    {
        return new CookieOptions
        {
            HttpOnly = httpOnly,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc))
        };
    }

    private string? ResolveClientIpAddress()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = Request.Headers["X-Real-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp.Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
