# Santander – Backend Developer Coding Test

ASP.NET Core Web API that retrieves the best *n* stories from the [Hacker News API](https://github.com/HackerNews/API), ordered by score descending.

## How to run

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A running Redis instance (default: `localhost:6379`)

### Run locally

```bash
# Start Redis (requires Docker)
docker run -d -p 6379:6379 redis:7-alpine

cd Api
dotnet run
```

The API will be available at `https://localhost:7106` (HTTPS) or `http://localhost:5038` (HTTP).

### Run with Docker Compose (recommended)

Starts the API and Redis together:

```bash
docker compose up --build
```

### Usage

```
GET /stories/best/{n}
```

Returns the top *n* stories sorted by score descending.

**Example request:**

```bash
curl https://localhost:7106/stories/best/10
```

**Example response:**

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

The OpenAPI documentation is available at `/openapi/v1.json` when running in Development mode.

## Assumptions

- The Hacker News `beststories.json` endpoint returns story IDs already ranked by score. The top *n* IDs are fetched and their details retrieved in parallel, then re-sorted by score to guarantee correct order regardless of any ranking drift.
- Stories without a URL (e.g. Ask HN posts) are returned with an empty `uri` string.
- The `commentCount` field maps to the `descendants` field in the Hacker News item response.

## Caching strategy

To avoid overloading the Hacker News API under high request volumes, a cache-aside pattern is applied using Redis (`IDistributedCache`) combined with **Redlock** distributed locking to prevent cache stampede.

The full list of best stories is fetched once and stored under a single cache key (`hackernews:beststories`) with a **1-minute TTL** (configurable via `HackerNews:CacheExpiryTimeInSeconds`). All requests within that window are served from cache without hitting the Hacker News API, regardless of the value of `n`.

### Cache stampede prevention (Redlock)

When a cache entry expires, many concurrent requests could simultaneously miss the cache and all call the Hacker News API — the "thundering herd" problem. This is prevented with a **double-check locking** pattern backed by the [Redlock](https://redis.io/docs/latest/develop/use/patterns/distributed-locks/) distributed algorithm:

1. Check cache → hit → return immediately (fast path).
2. Miss → acquire a distributed Redlock on `hackernews:beststories:lock` (only one instance proceeds).
3. Re-check cache after acquiring the lock — another instance may have already populated it.
4. Still a miss → fetch from Hacker News API, write to cache, release lock.
5. Lock not acquired after timeout → return `503 Service Unavailable` with `Retry-After` header.

## Enhancements given more time

- **Background refresh** – proactively refresh the best stories list before it expires, eliminating cold-start latency spikes on cache miss.
- **Structured logging and observability** – expose metrics (cache hit rate, Hacker News API latency) for monitoring dashboards and alerting.
- **Integration tests** – test the full request pipeline against a mock Hacker News API using `WebApplicationFactory`.
