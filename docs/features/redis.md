# Redis

```bash
aspireforge add redis
```

Adds a distributed cache backed by Redis.

## What it does

| Step | Detail |
|---|---|
| Added StackExchange.Redis client | Adds `Microsoft.Extensions.Caching.StackExchangeRedis` to `Infrastructure`. |
| Added connection configuration | Adds a `Redis` entry under `ConnectionStrings` in `Api`'s `appsettings.json` (default: `localhost:6379`). |
| Added distributed cache registration | Wires `builder.Services.AddStackExchangeRedisCache(options => options.Configuration = ...)` into `Api`'s `Program.cs`, registering the standard `IDistributedCache`. |

## Using it

Inject `IDistributedCache` wherever you need caching - it's the standard ASP.NET Core
abstraction, so this works with any code written against it:

```csharp
app.MapGet("/cached", async (IDistributedCache cache) =>
{
    var value = await cache.GetStringAsync("key");
    // ...
});
```

## Combining with Docker

If you've also run `aspireforge add docker` (or generated with it, which is the default), re-run
`aspireforge add docker` after `add redis` to refresh `docker-compose.yml` with a matching `redis`
service. See [`add`](../commands/add.md).
