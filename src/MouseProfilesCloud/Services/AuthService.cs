using Microsoft.Extensions.Options;
using MouseProfilesCloud.Models;
using MouseProfilesCloud.Options;
using MouseProfilesCloud.Repositories;

namespace MouseProfilesCloud.Services;

public class AuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IEmailService _email;
    private readonly AppOptions _app;
    private readonly TimeProvider _time;

    public AuthService(
        IUserRepository users,
        IPasswordHasher hasher,
        ITokenService tokens,
        IEmailService email,
        IOptions<AppOptions> app,
        TimeProvider time)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
        _email = email;
        _app = app.Value;
        _time = time;
    }

    public async Task<ServiceResult> RegisterAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();

        await _users.SweepUnverifiedAsync();

        var existing = await _users.FindByEmailAsync(email);
        if (existing != null)
        {
            return existing.EmailVerified
                ? ServiceResult.Fail("Email already registered.", 409)
                : ServiceResult.Fail("An account with that email is pending verification. Check your inbox or request a new verification email.", 409);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var verifyToken = Guid.NewGuid().ToString("N");
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = _hasher.Hash(password),
            EmailVerified = false,
            VerifyToken = verifyToken,
            VerifyTokenExpiry = now.AddHours(24),
            CreatedAt = now,
        };

        await _users.CreateAsync(user);
        await SendVerificationEmailAsync(user.Email, verifyToken);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> VerifyEmailAsync(string token)
    {
        await _users.SweepUnverifiedAsync();
        var user = await _users.FindByVerifyTokenAsync(token);
        if (user == null)
            return ServiceResult.Fail("Invalid or expired verification token.", 400);

        await _users.MarkEmailVerifiedAsync(user.Id);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<string>> LoginAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        await _users.SweepUnverifiedAsync();

        var user = await _users.FindByEmailAsync(email);

        // Always run Verify — even for unknown emails — to prevent timing-based enumeration.
        // Not a structurally valid BCrypt hash, but BCrypt.Net processes it without throwing and
        // returns false at the same work factor (11) as real password hashes.
        const string DummyHash = "$2a$11$dummyhashfortimingnobodywouldeverguessthisXXXXXXXXXXXXX";
        var hashToCheck = user?.PasswordHash ?? DummyHash;
        var passwordOk = _hasher.Verify(password, hashToCheck) && user != null;

        if (!passwordOk)
            return ServiceResult.Fail<string>("Invalid email or password.", 401);

        if (!user!.EmailVerified)
            return ServiceResult.Fail<string>("Please verify your email before logging in.", 401);

        return ServiceResult.Ok(_tokens.CreateToken(user));
    }

    public async Task<ServiceResult> ResendVerificationAsync(string email)
    {
        email = email.Trim().ToLowerInvariant();

        var user = await _users.FindByEmailAsync(email);

        // Don't reveal whether the email exists
        if (user == null || user.EmailVerified)
            return ServiceResult.Ok();

        var token = Guid.NewGuid().ToString("N");
        var expiry = _time.GetUtcNow().UtcDateTime.AddHours(24);
        user.VerifyToken = token;
        user.VerifyTokenExpiry = expiry;

        await _users.UpdateVerifyTokenAsync(user.Id, token, expiry);
        await SendVerificationEmailAsync(user.Email, token);

        return ServiceResult.Ok();
    }

    private Task SendVerificationEmailAsync(string email, string token)
    {
        var url = $"{_app.BaseUrl.TrimEnd('/')}/auth/verify-email?token={token}";
        return _email.SendVerificationEmailAsync(email, url);
    }
}
