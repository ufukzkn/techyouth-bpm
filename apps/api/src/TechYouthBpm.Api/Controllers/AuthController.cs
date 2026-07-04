using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
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
}
