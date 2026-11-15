using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MouseProfilesCloud.Services;

namespace MouseProfilesCloud.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    public record RegisterRequest([Required, EmailAddress] string Email, [Required, MinLength(8)] string Password);
    public record LoginRequest([Required] string Email, [Required] string Password);
    public record ResendRequest([Required, EmailAddress] string Email);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var result = await _auth.RegisterAsync(req.Email, req.Password);
        if (!result.Succeeded)
            return Problem(result.Error, statusCode: result.StatusCode);

        return Ok(new { message = "Account created. Check your email to verify your address." });
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery, Required] string token)
    {
        var result = await _auth.VerifyEmailAsync(token);
        if (!result.Succeeded)
            return Problem(result.Error, statusCode: result.StatusCode);

        return Ok(new { message = "Email verified. You can now log in." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var result = await _auth.LoginAsync(req.Email, req.Password);
        if (!result.Succeeded)
            return Problem(result.Error, statusCode: result.StatusCode);

        return Ok(new { token = result.Value });
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(ResendRequest req)
    {
        await _auth.ResendVerificationAsync(req.Email);
        // Always return 200 to avoid leaking whether the email exists
        return Ok(new { message = "If that address is registered and unverified, a new email has been sent." });
    }
}
