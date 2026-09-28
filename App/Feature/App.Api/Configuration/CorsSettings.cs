namespace App.Api.Configuration;

public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    public const string PolicyName = "Frontend";

    // An explicit allow-list; empty means no cross-origin browser access at all.
    public string[] AllowedOrigins { get; set; } = [];
}
