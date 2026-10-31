# Performance rules

## PERF001 - Response compression missing

**Default severity:** `suggestion`

Fires when the project is a web app but no call to `AddResponseCompression` was found. Without it,
larger JSON responses are sent uncompressed, costing bandwidth and latency.

**Fix:** Call `builder.Services.AddResponseCompression()` and add the response compression
middleware to the pipeline.

## PERF002 - Rate limiting missing

**Default severity:** `suggestion`

Fires when the project is a web app but no call to `AddRateLimiter` was found. Without it, a single
client can send unlimited requests, with no built-in protection against abuse or accidental
overload.

**Fix:** Call `builder.Services.AddRateLimiter(...)` and apply a rate limiting policy to your
endpoints.

## PERF003 - Blocking call on asynchronous code

**Default severity:** `warning`

Unlike the other rules on this page, this one fires on the *presence* of a pattern rather than its
absence: a `.GetAwaiter().GetResult()` or `.Wait()` call found anywhere in the source. Blocking on
async code from a synchronous context risks thread-pool starvation and deadlocks under load.

This check isn't limited to web projects - a blocking call in a library or background service is
just as much of a problem.

A bare `.Result` was deliberately left out: too many unrelated types (DTOs, wrapper records) have a
property named `Result`, and a rule that fires on those would be noisy enough to undermine trust in
the ones that don't.

**Fix:** `await` the call instead of blocking on it.

## PERF004 - Synchronous I/O allowed

**Default severity:** `warning` (`suggestion` per request)

ASP.NET Core disallows synchronous reads and writes of request and response bodies by default. Each
blocking call parks a thread-pool thread while it waits on the network, and under load the pool
runs dry: requests queue, latency climbs, and the server can stop responding entirely (thread-pool
starvation). `AllowSynchronousIO = true` turns that protection off.

| Where | Severity |
| --- | --- |
| Server-wide - `KestrelServerOptions`, `IISServerOptions`, `TestServer`: `options.AllowSynchronousIO = true` | warning |
| Per request - `IHttpBodyControlFeature.AllowSynchronousIO = true`, directly or through a variable obtained from `HttpContext.Features` | suggestion |

The per-request form is Microsoft's documented workaround for a library that only does synchronous
I/O: it limits the damage to the endpoints that set it, but those endpoints still block threads.
Assignments inside a development-only guard (`if (...IsDevelopment())`, `#if DEBUG`) are allowed;
`= false`, comparisons, and comments aren't flagged. The finding lists each assignment with its file
and line, server-wide first.

**Fix:** Use the async APIs - `ReadAsync`, `WriteAsync`, `ReadFromJsonAsync`, `WriteAsJsonAsync`. For a
library that can only read synchronously, buffer the body asynchronously first and hand it the
buffered stream:

```csharp
context.Request.EnableBuffering();
await context.Request.Body.DrainAsync(context.RequestAborted); // read fully, asynchronously
context.Request.Body.Position = 0;                             // now safe to read synchronously
```
