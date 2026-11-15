namespace MouseProfilesCloud.Models;

public class AppUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string? VerifyToken { get; set; }
    public DateTime? VerifyTokenExpiry { get; set; }
    public DateTime CreatedAt { get; set; }
}
