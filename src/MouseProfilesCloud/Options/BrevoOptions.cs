using System.ComponentModel.DataAnnotations;

namespace MouseProfilesCloud.Options;

public class BrevoOptions
{
    public const string Section = "Brevo";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string FromEmail { get; set; } = string.Empty;

    [Required]
    public string FromName { get; set; } = string.Empty;
}
