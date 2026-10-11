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

## SEC008 - Authorization not enforced

**Default severity:** `warning`

SEC001 checks that callers *can* be identified; SEC008 checks that anything actually *requires*
it. Authentication on its own only reads the token - an endpoint with no authorization requirement
still serves anonymous requests. So a project can register JWT bearer, pass SEC001, and leave every
endpoint public with no visible symptom.

Fires when the project is a web app, calls `AddAuthentication`, and none of these appear anywhere in
its source (including referenced projects):

- the `[Authorize]` attribute in any form - `[Authorize]`, `[Authorize(Roles = "...")]`,
  `[ApiController, Authorize]`, `[Microsoft.AspNetCore.Authorization.Authorize]`
- `RequireAuthorization()` on an endpoint, a route group (`MapGroup(...)`), or `MapControllers()`
- a fallback policy - `options.FallbackPolicy = ...` or `SetFallbackPolicy(...)`
- a global `AuthorizeFilter` or `new AuthorizeAttribute()` in MVC filters/conventions
- Razor Pages conventions - `AuthorizeFolder`, `AuthorizePage`, `AuthorizeAreaFolder`,
  `AuthorizeAreaPage`

`[AllowAnonymous]` and look-alike names such as `[AuthorizeView]` don't count, and neither do
mentions in comments. Stays silent when authentication isn't configured at all - that's SEC001's
finding. This rule checks that *some* endpoint requires authorization, not that the *right* ones
do; an explicit `FallbackPolicy = null` also counts as "configured".

**Fix:** Add `[Authorize]` / `.RequireAuthorization()` where callers must be identified, or protect
everything by default with a fallback policy and opt specific endpoints out with `[AllowAnonymous]`:

```csharp
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
```
