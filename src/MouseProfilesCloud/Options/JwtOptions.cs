using System.ComponentModel.DataAnnotations;

namespace MouseProfilesCloud.Options;

public class JwtOptions
{
    public const string Section = "Jwt";

    [Required, MinLength(32)]
    public string Secret { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;
}
