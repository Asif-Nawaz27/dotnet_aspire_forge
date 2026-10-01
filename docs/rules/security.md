# Security rules

## SEC001 - Authentication not configured

**Default severity:** `warning`

Fires when the project looks like an ASP.NET Core web app (its source contains
`WebApplication.CreateBuilder`) and either of two things is missing:

- No call to `AddAuthentication` - no scheme was ever registered, or
- `AddAuthentication` is present but no call to `app.UseAuthentication()` was found - a scheme is
  registered but the middleware never runs, so requests are never actually authenticated.

**Fix:** `aspireforge add authentication` (see [`add`](../commands/add.md)).

## SEC002 - CORS policy allows unrestricted origins

**Default severity:** `error`

Fires when the source contains a call to `AllowAnyOrigin`, which permits requests from any origin.

**Fix:** Restrict the CORS policy to a known allow-list of origins.

## SEC003 - HTTPS redirection missing

**Default severity:** `warning`

Fires when the project is a web app but no call to `UseHttpsRedirection` was found, meaning HTTP
requests won't be automatically upgraded to HTTPS.

**Fix:** Call `app.UseHttpsRedirection()` in `Program.cs`.

## SEC004 - Secrets in configuration files

**Default severity:** `error`

Scans the project's `appsettings*.json` files and fires when one contains a literal secret:

- a connection string with a non-empty `Password`, `Pwd`, `AccountKey`, or `SharedAccessKey`, or
- a non-empty value under a key ending in `Password`, `Pwd`, `Secret`, `ApiKey`, `SigningKey`,
  `PrivateKey`, `AccessKey`, or `Token` (case-insensitive - so `Jwt:SigningKey` and
  `GitHub:AccessToken` match, but `TokenLifetime` does not).

These files are copied into every `dotnet publish` output and container image, so a secret in one
leaks to anyone with the build artifact - whether or not the file is also committed to git.

`appsettings.Development.json` is skipped: a local database password there is normal and never
reaches production. Empty values, Azure Key Vault references (`@Microsoft.KeyVault(...)`), and
`${VARIABLE}` placeholders are not reported. The report names the key, file, and line - never the
secret value.

**Fix:** Leave the value empty in `appsettings.json` and supply it at runtime: `dotnet user-secrets`
in development, environment variables (e.g. `Jwt__SigningKey`) or a secret store such as Azure Key
Vault in production.
