using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MouseProfilesCloud.Models;
using MouseProfilesCloud.Options;
using MouseProfilesCloud.Repositories;
using MouseProfilesCloud.Services;

namespace MouseProfilesCloud.Tests.Auth;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly FakeTimeProvider _time = new();

    private AuthService CreateSut() => new(
        _users.Object,
        _hasher.Object,
        _tokens.Object,
        _email.Object,
        Microsoft.Extensions.Options.Options.Create(new AppOptions { BaseUrl = "https://example.com" }),
        _time);

    // ── Register ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_NewEmail_CreatesUserAndSendsEmail()
    {
        _users.Setup(r => r.FindByEmailAsync("user@example.com")).ReturnsAsync((AppUser?)null);
        _hasher.Setup(h => h.Hash("password123")).Returns("hashed");

        var sut = CreateSut();
        var result = await sut.RegisterAsync("user@example.com", "password123");

        Assert.True(result.Succeeded);
        _users.Verify(r => r.CreateAsync(It.Is<AppUser>(u =>
            u.Email == "user@example.com" &&
            u.PasswordHash == "hashed" &&
            !u.EmailVerified &&
            u.VerifyToken != null)), Times.Once);
        _email.Verify(e => e.SendVerificationEmailAsync("user@example.com", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Register_NormalizesEmail_ToLowercase()
    {
        _users.Setup(r => r.FindByEmailAsync("user@example.com")).ReturnsAsync((AppUser?)null);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");

        var sut = CreateSut();
        await sut.RegisterAsync("  USER@Example.COM  ", "password123");

        _users.Verify(r => r.FindByEmailAsync("user@example.com"), Times.Once);
        _users.Verify(r => r.CreateAsync(It.Is<AppUser>(u => u.Email == "user@example.com")), Times.Once);
    }

    [Fact]
    public async Task Register_DuplicateVerifiedEmail_Returns409()
    {
        _users.Setup(r => r.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(new AppUser { Email = "user@example.com", EmailVerified = true });

        var result = await CreateSut().RegisterAsync("user@example.com", "password123");

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
        _users.Verify(r => r.CreateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task Register_DuplicateUnverifiedEmail_Returns409WithResendHint()
    {
        _users.Setup(r => r.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(new AppUser { Email = "user@example.com", EmailVerified = false });

        var result = await CreateSut().RegisterAsync("user@example.com", "password123");

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("pending verification", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_SweepsUnverifiedAccountsFirst()
    {
        _users.Setup(r => r.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((AppUser?)null);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");

        await CreateSut().RegisterAsync("user@example.com", "password123");

        _users.Verify(r => r.SweepUnverifiedAsync(), Times.Once);
    }

    [Fact]
    public async Task Register_VerifyTokenExpiry_Is24HoursFromNow()
    {
        var now = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _time.SetUtcNow(now);
        _users.Setup(r => r.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((AppUser?)null);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");

        await CreateSut().RegisterAsync("user@example.com", "password123");

        _users.Verify(r => r.CreateAsync(It.Is<AppUser>(u =>
            u.VerifyTokenExpiry == now.UtcDateTime.AddHours(24))), Times.Once);
    }

    // ── VerifyEmail ───────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_ValidToken_MarksVerifiedAndReturnsOk()
    {
        var user = new AppUser { Id = Guid.NewGuid(), EmailVerified = false };
        _users.Setup(r => r.FindByVerifyTokenAsync("validtoken")).ReturnsAsync(user);

        var result = await CreateSut().VerifyEmailAsync("validtoken");

        Assert.True(result.Succeeded);
        _users.Verify(r => r.MarkEmailVerifiedAsync(user.Id), Times.Once);
    }

    [Fact]
    public async Task VerifyEmail_InvalidOrExpiredToken_Returns400()
    {
        _users.Setup(r => r.FindByVerifyTokenAsync("badtoken")).ReturnsAsync((AppUser?)null);

        var result = await CreateSut().VerifyEmailAsync("badtoken");

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        _users.Verify(r => r.MarkEmailVerifiedAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ── Login ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var user = new AppUser { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "hashed", EmailVerified = true };
        _users.Setup(r => r.FindByEmailAsync("user@example.com")).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("password123", "hashed")).Returns(true);
        _tokens.Setup(t => t.CreateToken(user)).Returns("jwt.token.here");

        var result = await CreateSut().LoginAsync("user@example.com", "password123");

        Assert.True(result.Succeeded);
        Assert.Equal("jwt.token.here", result.Value);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var user = new AppUser { Email = "user@example.com", PasswordHash = "hashed", EmailVerified = true };
        _users.Setup(r => r.FindByEmailAsync("user@example.com")).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrongpassword", "hashed")).Returns(false);

        var result = await CreateSut().LoginAsync("user@example.com", "wrongpassword");

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401WithSameMessageAndAlwaysCallsVerify()
    {
        _users.Setup(r => r.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((AppUser?)null);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await CreateSut().LoginAsync("ghost@example.com", "password123");

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Invalid email or password.", result.Error);
        // Verify is always called — even for unknown emails — to prevent timing-based enumeration
        _hasher.Verify(h => h.Verify("password123", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Login_UnverifiedEmail_Returns401WithHint()
    {
        var user = new AppUser { Email = "user@example.com", PasswordHash = "hashed", EmailVerified = false };
        _users.Setup(r => r.FindByEmailAsync("user@example.com")).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("password123", "hashed")).Returns(true);

        var result = await CreateSut().LoginAsync("user@example.com", "password123");

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
        Assert.Contains("verify your email", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── ResendVerification ────────────────────────────────────────────────

    [Fact]
    public async Task ResendVerification_UnverifiedUser_UpdatesTokenAndSendsEmail()
    {
        var user = new AppUser { Id = Guid.NewGuid(), Email = "user@example.com", EmailVerified = false };
        _users.Setup(r => r.FindByEmailAsync("user@example.com")).ReturnsAsync(user);

        var result = await CreateSut().ResendVerificationAsync("user@example.com");

        Assert.True(result.Succeeded);
        _users.Verify(r => r.UpdateVerifyTokenAsync(user.Id, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        _email.Verify(e => e.SendVerificationEmailAsync("user@example.com", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ResendVerification_UnknownEmail_ReturnsOkWithoutLeaking()
    {
        _users.Setup(r => r.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((AppUser?)null);

        var result = await CreateSut().ResendVerificationAsync("ghost@example.com");

        Assert.True(result.Succeeded);
        _email.Verify(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResendVerification_AlreadyVerified_ReturnsOkWithoutSending()
    {
        var user = new AppUser { Email = "user@example.com", EmailVerified = true };
        _users.Setup(r => r.FindByEmailAsync("user@example.com")).ReturnsAsync(user);

        var result = await CreateSut().ResendVerificationAsync("user@example.com");

        Assert.True(result.Succeeded);
        _email.Verify(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
