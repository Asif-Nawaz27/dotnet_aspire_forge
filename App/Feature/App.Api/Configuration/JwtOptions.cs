using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace App.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    // HMAC-SHA256 needs at least a 256-bit key.
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;
}

// Fails startup outside Development if the checked-in placeholder key is still in use, so a
// misconfigured deployment can't silently accept tokens signed with a publicly known key.
public sealed class JwtOptionsValidator(IHostEnvironment environment) : IValidateOptions<JwtOptions>
{
    public const string PlaceholderPrefix = "CHANGE_ME";

    public ValidateOptionsResult Validate(string? name, JwtOptions options) =>
        !environment.IsDevelopment() && options.SigningKey.StartsWith(PlaceholderPrefix, StringComparison.Ordinal)
            ? ValidateOptionsResult.Fail("Jwt:SigningKey is still the development placeholder. Supply a real key via configuration.")
            : ValidateOptionsResult.Success;
}
