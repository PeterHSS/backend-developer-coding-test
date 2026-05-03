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

To avoid overloading the Hacker News API under high request volumes, a two-level cache-aside pattern is applied using Redis (`IDistributedCache`) combined with **Redlock** distributed locking to prevent cache stampede:

1. **Best story IDs list** – cached for 5 minutes. All requests within the window share the same ID list without hitting HN.
2. **Individual story details** – each story is cached independently for 5 minutes. A warm cache means any subsequent request for overlapping stories (e.g. top 5 then top 10) pays no additional cost.

### Cache stampede prevention (Redlock)

When a cache entry expires, many concurrent requests could simultaneously miss the cache and all call the Hacker News API — the "thundering herd" problem. `LockedCache` prevents this with a **double-check locking** pattern backed by the [Redlock](https://redis.io/docs/latest/develop/use/patterns/distributed-locks/) distributed algorithm:

1. Check cache → hit → return immediately (fast path).
2. Miss → acquire a distributed Redlock on `lock:<key>` (only one instance proceeds).
3. Re-check cache after acquiring the lock — another instance may have already populated it.
4. Still a miss → call the factory, write to cache, release lock.

## Enhancements given more time

- **Distributed cache (Redis)** – replace the in-memory cache with Redis so the cache is shared across multiple API instances in a scaled-out deployment.
- **Background refresh** – proactively refresh the best stories list and individual story details before they expire, eliminating cold-start latency spikes.
- **Concurrency limiting** – add a `SemaphoreSlim` to cap the number of simultaneous outbound calls to the Hacker News API on cache miss, preventing thundering-herd scenarios.
- **Resilience** – wrap `HttpClient` calls with [Polly](https://github.com/App-vNext/Polly) retry and circuit-breaker policies to handle transient HN API failures gracefully.
- **Input validation** – reject requests where `n` is non-positive or exceeds a configurable maximum (the HN API caps best stories at 500).
- **Structured logging and observability** – add request/response logging and expose metrics (cache hit rate, HN API latency) for monitoring.
- **Integration tests** – test the full request pipeline against a mock HN API using `WebApplicationFactory`.
