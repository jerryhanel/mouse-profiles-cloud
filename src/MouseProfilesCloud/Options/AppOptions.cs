using System.ComponentModel.DataAnnotations;

namespace MouseProfilesCloud.Options;

public class AppOptions
{
    public const string Section = "App";

    [Required]
    public string BaseUrl { get; set; } = string.Empty;
}
