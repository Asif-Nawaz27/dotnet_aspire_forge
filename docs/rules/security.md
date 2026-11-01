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

## SEC005 - Weak JWT validation

**Default severity:** `error` (authenticity/expiry) / `warning` (everything else)

Flags JWT bearer settings that switch off a check the token handler performs by default. These are
usually added to get past an error during development - "IDX10223: Lifetime validation failed" -
and then ship, quietly turning token *validation* into token *parsing*.

| Setting | What it allows | Severity |
| --- | --- | --- |
| `RequireSignedTokens = false` | unsigned (`"alg": "none"`) tokens | error |
| `SignatureValidator` that returns `new JwtSecurityToken(token)` / `new JsonWebToken(token)` | tokens with no signature check at all - anyone can mint one | error |
| `ValidateLifetime = false` | expired tokens, forever | error |
| `ValidateIssuer = false` | tokens from any issuer that shares the signing key | warning |
| `ValidateAudience = false` | tokens minted for a different API | warning |
| `RequireExpirationTime = false` | tokens with no expiry, which never expire | warning |
| `RequireHttpsMetadata = false` | signing keys fetched over plain HTTP, replaceable in transit | warning |
| `ClockSkew` above 5 minutes (`TimeSpan.MaxValue`, `FromHours(1)`, ...) | every token outliving its expiry by that much | warning |

Only literal assignments count (`ValidateLifetime = false`, not `== false` comparisons or computed
values such as `RequireHttpsMetadata = !env.IsDevelopment()`). Settings inside a development-only
guard - `if (...IsDevelopment())` at any nesting depth, or `#if DEBUG` - are allowed; negated checks
and `IsProduction()` are not. The finding lists each setting with its file and line, most severe
first.

`ValidateIssuerSigningKey = false` is deliberately **not** flagged: it's the default, and it doesn't
disable signature validation - it only skips extra checks on the key itself, such as certificate
expiry.

**Fix:** Keep the defaults and configure the values the checks need (`ValidIssuer`,
`ValidAudience`, the signing key or `Authority`). If a relaxed setting is needed locally, limit it:

```csharp
options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
```

## SEC007 - Development features enabled in production

**Default severity:** `error` (`warning` for API documentation)

Flags middleware meant for a developer's machine, registered where it also runs in production:

| Call | What it exposes | Severity |
| --- | --- | --- |
| `UseDeveloperExceptionPage` | every unhandled exception returns the stack trace, source snippets, headers, and cookies to whoever sent the request | error |
| `UseMigrationsEndPoint` / `UseDatabaseErrorPage` | EF Core's development endpoint, which can apply pending migrations to the database over HTTP | error |
| `MapOpenApi`, `UseSwagger`, `UseSwaggerUI`, `MapScalarApiReference`, `UseReDoc`, NSwag's `UseOpenApi` / `UseSwaggerUi` | a map (or interactive console) of every endpoint and model | warning |

API documentation is sometimes published deliberately for a public API, so it's a warning rather
than an error.

A call is fine inside:

- a development guard - `if (app.Environment.IsDevelopment())` (braced or braceless, at any nesting
  depth), the **`else`** of `if (!app.Environment.IsDevelopment())` (the ASP.NET Core template
  shape), or `#if DEBUG`;
- a **non-production** guard - `if (!app.Environment.IsProduction())`, or the `else` of
  `if (app.Environment.IsProduction())` - because this rule's concern is production specifically,
  and keeping API docs on in staging is a common, deliberate choice.

The `else` of a *positive* development check, `IsProduction()` blocks, and calls after a closed guard
block are flagged; service registrations (`AddOpenApi`, `AddSwaggerGen`) and comments aren't. Only
checked in web projects. The finding lists each call with its file and line, errors first.

**Fix:**

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}
```

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

## SEC009 - Middleware in the wrong order

**Default severity:** `error` (authentication/authorization) / `warning` (everything else)

Middleware runs in the order it's added, and several pairs only work one way round. The wrong order
compiles, starts, and passes a casual smoke test, then breaks under real traffic. SEC009 checks
these pairs, following the
[ASP.NET Core middleware order](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/#middleware-order):

| Must come first | Before | If reversed | Severity |
| --- | --- | --- | --- |
| `UseAuthentication` | `UseAuthorization` | authorization runs before the caller is identified, so protected endpoints reject valid credentials | error |
| `UseRouting` | `UseAuthorization` | authorization can't see the endpoint's `[Authorize]` / `RequireAuthorization()` requirements | error |
| `UseCors` | `UseAuthorization` | CORS preflight (`OPTIONS`) requests carry no credentials, so authorization rejects them and browsers block the call | warning |
| `UseRouting` | `UseCors` | endpoint-specific CORS policies (`[EnableCors]`, `RequireCors`) are ignored | warning |
| `UseRouting` | `UseRateLimiter` | endpoint-specific rate limit policies (`[EnableRateLimiting]`, `RequireRateLimiting`) are ignored | warning |
| `UseExceptionHandler` / `UseDeveloperExceptionPage` | `UseRouting`, `UseCors`, `UseAuthentication`, `UseAuthorization`, `UseRateLimiter`, `UseResponseCompression` | exceptions thrown by the earlier middleware bypass the error handler | warning |

A pair is checked only when both calls appear in the same source file, comparing the position of
each call's first occurrence. The `UseRouting` pairs apply only when `UseRouting()` is called
explicitly - without it, `WebApplication` adds routing at the start of the pipeline. Mentions in
comments are ignored. The finding lists each violation with its file and line, most severe first.

**Fix:** Reorder the calls in `Program.cs`. A safe order:

```csharp
app.UseExceptionHandler();
app.UseHsts();
app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseRouting();          // only if you call it explicitly
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
```

## SEC010 - TLS certificate validation disabled

**Default severity:** `error` (callbacks) / `warning` (`TrustServerCertificate`)

Flags code that accepts any TLS certificate. The connection is still encrypted - but to whoever
answers, so anyone on the network path can present their own certificate and read or change the
traffic (a man-in-the-middle attack). This is almost always a "temporary" fix for a self-signed
certificate in development that then ships.

| Pattern | Severity |
| --- | --- |
| `HttpClientHandler.DangerousAcceptAnyServerCertificateValidator` | error |
| `ServerCertificateCustomValidationCallback` set to a callback that always returns `true` | error |
| `RemoteCertificateValidationCallback` (`SslStream`, `SslClientAuthenticationOptions`) that always returns `true` | error |
| `ServicePointManager.ServerCertificateValidationCallback` that always returns `true` - affects every connection in the process | error |
| `TrustServerCertificate=true` / `Trust Server Certificate=true` in a connection string (C# source, or `appsettings*.json` other than `appsettings.Development.json`) | warning |

"Always returns `true`" covers `(_, _, _, _) => true`, `=> { return true; }`, and
`delegate { return true; }`, assigned with `=` or `+=`. A callback that actually inspects the
certificate (`errors == SslPolicyErrors.None`, a thumbprint comparison) is not flagged, and neither
is a method group (`= ValidatePinnedCertificate`) whose body isn't visible to the rule. Code inside
a development-only guard (`if (...IsDevelopment())`, `#if DEBUG`) is allowed. The finding lists each
occurrence with its file and line, errors first.

**Fix:** Trust the specific certificate rather than every certificate - install its CA on the
machine or container, or pin it in the callback:

```csharp
handler.ServerCertificateCustomValidationCallback = (_, cert, _, errors) =>
    errors == SslPolicyErrors.None || cert?.GetCertHashString() == expectedThumbprint;
```

For databases, use a certificate the server's clients trust and remove `TrustServerCertificate`;
keep any bypass in `appsettings.Development.json` or behind `IsDevelopment()`.

## SEC011 - SQL injection risk in raw SQL

**Default severity:** `error`

Fires when SQL is built from runtime values at the call site of an API that executes it verbatim.
The value is pasted into the SQL text, so input such as `' OR 1=1 --` changes the query instead of
being treated as data.

Unsafe first-argument shapes:

- an interpolated string with holes - `FromSqlRaw($"... WHERE Name = '{name}'")`
- `string.Format(...)` - `ExecuteSqlRaw(string.Format("... {0}", id))`
- a literal concatenated with a non-literal - `new SqlCommand("... WHERE Id = " + id, conn)`

APIs checked:

| API | Flagged for |
| --- | --- |
| EF Core `FromSqlRaw`, `ExecuteSqlRaw(Async)`, `SqlQueryRaw` | interpolation, concatenation, `string.Format` |
| EF Core `FromSql`, `ExecuteSql(Async)`, `SqlQuery`, `ExecuteSqlCommand(Async)` | concatenation, `string.Format` only - with an interpolated string these take a `FormattableString` and turn every hole into a parameter, which is the safe pattern |
| ADO.NET `new SqlCommand` / `NpgsqlCommand` / `MySqlCommand` / `SqliteCommand` / `OracleCommand` / `OdbcCommand` / `OleDbCommand`, and `CommandText = ...` | interpolation, concatenation, `string.Format` |
| Dapper `Query*` / `Execute*` (only in files with `using Dapper;`) | interpolation, concatenation, `string.Format` |

Not flagged: parameterised calls (`FromSqlRaw("... {0}", id)`, `@id` with `SqlParameter`), constant
SQL (`"SELECT a " + "FROM b"`, interpolated strings with no holes or only `{{escaped}}` braces),
comments, and a variable passed in (`FromSqlRaw(sql)`) - where that variable was built isn't
visible to a text heuristic. The finding lists each call with its file and line.

**Fix:** Let the database driver handle the values:

```csharp
// EF Core: FromSql/ExecuteSql/SqlQuery parameterise every interpolation hole
db.Users.FromSql($"SELECT * FROM users WHERE name = {name}");

// ADO.NET
command.CommandText = "SELECT * FROM users WHERE name = @name";
command.Parameters.AddWithValue("name", name);

// Dapper
connection.Query<User>("SELECT * FROM users WHERE name = @name", new { name });
```

## SEC012 - Unsafe deserialization

**Default severity:** `error` (`warning` when a `SerializationBinder` is set)

Flags deserializers that let the payload choose which .NET type to create. An attacker who controls
the input - a request body, cookie, cache entry, or queue message - can name a "gadget" type whose
constructor or property setters run code: remote code execution from a crafted payload.

| Pattern | Severity |
| --- | --- |
| `BinaryFormatter`, `SoapFormatter`, `NetDataContractSerializer`, `LosFormatter`, `ObjectStateFormatter` | error |
| `new JavaScriptSerializer(new SimpleTypeResolver())` | error |
| Newtonsoft.Json `TypeNameHandling` / `ItemTypeNameHandling` = `All`, `Auto`, `Objects`, or `Arrays` - on `JsonSerializerSettings`, `JsonSerializer`, or `[JsonProperty(...)]` | error |
| the same `TypeNameHandling` in a file that also assigns a `SerializationBinder` | warning |

The formatters are on Microsoft's
[insecure deserializers list](https://learn.microsoft.com/dotnet/standard/serialization/binaryformatter-security-guide):
they can't be made safe for untrusted input. Every `TypeNameHandling` value other than `None` is
exploitable, not only `All`/`Auto` (code analysis rules CA2326/CA2327). A `SerializationBinder` can
restrict the allowed types, so with one present the finding drops to a warning - the rule can't
verify that the binder actually allow-lists them.

Not flagged: `TypeNameHandling.None`, comparisons (`== TypeNameHandling.Auto`), System.Text.Json
(it never reads type names from the payload, and `[JsonDerivedType]` polymorphism is an explicit
allow-list), and look-alike names such as `MyBinaryFormatterWrapper`.

**Fix:** Deserialize into known types with System.Text.Json, using `[JsonDerivedType]` where you
need polymorphism:

```csharp
[JsonDerivedType(typeof(CardPayment), "card")]
[JsonDerivedType(typeof(BankPayment), "bank")]
public abstract record Payment;
```

If you must keep Newtonsoft type names, set a `SerializationBinder` that allow-lists exactly the
expected types.

## SEC013 - Hard-coded secrets in code

**Default severity:** `error`

[SEC004](#sec004---secrets-in-configuration-files)'s counterpart for C# source: secrets written as
string literals. Once compiled they're in every build artifact and container image - and in source
control history forever, even after the line is deleted, so the secret has to be rotated.

| Pattern | Example |
| --- | --- |
| Literal key material passed to a crypto API | `new SymmetricSecurityKey(Encoding.UTF8.GetBytes("..."))`, `new SymmetricSecurityKey(Convert.FromBase64String("..."))`, `new HMACSHA256(Encoding.UTF8.GetBytes("..."))` |
| A literal password in a credential | `new NetworkCredential("user", "...")` |
| A connection-string credential in a string literal | `"Host=db;Password=..."`, also `Pwd=`, `AccountKey=`, `SharedAccessKey=` |
| A literal assigned to a secret-named variable, field, property, or named argument | names ending in `Password`, `Pwd`, `Secret`, `ApiKey`, `SigningKey`, `PrivateKey`, `AccessKey`, or `Token` - e.g. `const string ApiKey = "sk_live_..."`, `public string GitHubToken { get; set; } = "ghp_..."`, `signingKey: "..."` |

To keep noise down, some literals are skipped:

- **Secret-named literals** that look like something else: shorter than 8 characters, containing
  whitespace or parentheses (messages, code snippets), header names (`"X-Api-Key"`), configuration
  paths (`"Jwt:SigningKey"`), or URLs.
- **Connection strings to the local machine** - server `localhost`, `127.0.0.1`, `::1`, `.`, or
  `(localdb)`. A credential that only works on the developer's own machine (a local default or a
  generated template) isn't a leaked secret.
- Empty values, interpolation holes (`Password={password}`), placeholders (`<password>`, `***`),
  comparisons (`==`), expression-bodied members (`=>`), and comments.

Development guards don't exempt anything: a literal in source is committed regardless of which
environment runs it. The finding names each location - never the value.

**Fix:** Rotate the secret, then read it from configuration (`IConfiguration` / `IOptions`) backed by
`dotnet user-secrets` in development and environment variables or a secret store such as Azure Key
Vault in production:

```csharp
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey));
```
