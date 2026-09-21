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
