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

Starts the API and Redis together. Redis must be healthy before the API container starts.

```bash
docker compose up --build
```

The API will be available at `http://localhost:8080`.

## Usage

```
GET /stories/best/{n}
```

Returns the top *n* stories (1–500) sorted by score descending.

**Example request:**

```bash
curl http://localhost:8080/stories/best/10
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

**Validation:** `n` must be between 1 and 500 (inclusive). Values outside this range return `400 Bad Request` with a `ProblemDetails` body.

### API documentation

When running in Development mode, interactive documentation (Scalar UI) is available at `/scalar/v1` and the raw OpenAPI schema at `/openapi/v1.json`.

## Configuration

All settings live under the `HackerNews` key in `appsettings.json`:

| Setting | Default | Description |
|---|---|---|
| `BaseUrl` | `https://hacker-news.firebaseio.com/v0/` | Hacker News API base URL |
| `CacheExpiryTimeInSeconds` | `60` | Redis TTL for the cached story list |
| `LockExpiryInSeconds` | `30` | Maximum lifetime of the Redlock distributed lock |
| `LockWaitInSeconds` | `5` | How long to wait to acquire the lock before returning 503 |
| `LockRetryIntervalInMilliseconds` | `100` | Polling interval while waiting for the lock |
| `MaxDegreeOfParallelism` | `10` | Concurrent Hacker News item requests per cache miss |
| `TimeoutInSeconds` | `10` | Per-request HTTP timeout to the Hacker News API |
| `MaxRetryAttempts` | `3` | Retry attempts on transient HTTP failures |
| `DelayRetryInMilliseconds` | `500` | Base delay between retries |

## Caching strategy

To avoid overloading the Hacker News API under high request volumes, a cache-aside pattern is applied using Redis (`IDistributedCache`) combined with **Redlock** distributed locking to prevent cache stampede.

On a cache miss, **all** story IDs returned by `beststories.json` are fetched and their details retrieved in parallel (up to `MaxDegreeOfParallelism` concurrent requests). The complete list is sorted by score descending, serialised, and stored under the key `hackernews:beststories` with a configurable TTL (default 60 s). Subsequent requests — regardless of the value of *n* — are served directly from cache by reading the full list and applying `.Take(n)`.

### Cache stampede prevention (Redlock)

When a cache entry expires, many concurrent requests could simultaneously miss the cache and all call the Hacker News API — the "thundering herd" problem. This is prevented with a **double-check locking** pattern backed by the [Redlock](https://redis.io/docs/latest/develop/use/patterns/distributed-locks/) distributed algorithm:

1. Check cache → hit → return immediately (fast path).
2. Miss → acquire a distributed Redlock on `hackernews:beststories:lock` (only one instance proceeds).
3. Re-check cache after acquiring the lock — another instance may have already populated it.
4. Still a miss → fetch from Hacker News API, write to cache, release lock.
5. Lock not acquired after `LockWaitInSeconds` → return `503 Service Unavailable` with a `Retry-After` header set to `LockExpiryInSeconds`.

## HTTP resilience

Outbound calls to the Hacker News API are made through a typed `HttpClient` configured with `AddStandardResilienceHandler`, which composes:

- **Retry** – up to `MaxRetryAttempts` retries with a `DelayRetryInMilliseconds` base delay on transient failures.
- **Circuit breaker** – opens automatically when the error rate exceeds the threshold, preventing further calls to an unavailable upstream.
- **Total request timeout** – caps the total allowed time per outbound call to `TimeoutInSeconds`.

## Testing

The solution includes a unit test project (`Api.Tests`) covering all layers without external dependencies.

```bash
dotnet test Api.Tests/Api.Tests.csproj
```

**What is tested (37 tests):**

| Layer | Scenarios covered |
|---|---|
| `GetBestStoriesUseCase` | Field mapping, Unix → `DateTimeOffset` conversion, parameter forwarding |
| `GetBestStoriesEndpoint` | Boundary validation (`total` 1–500), HTTP 400/200 responses, content-type |
| `HackerNewsService` | Cache hit (no lock acquired), `count` limiting, lock timeout → exception, API fetch + cache write, score ordering, double-check locking, null story exclusion, null URL → empty string |
| `HackerNewsHttpClient` | Correct endpoint paths, deserialization, null response handling |
| `GlobalExceptionHandler` | 503 + `Retry-After` for `CacheLockNotAcquiredException`, 500 for unhandled exceptions |

The endpoint tests use `WebApplicationFactory<Program>` with Redis replaced by an in-memory cache and all infrastructure mocked via NSubstitute, so no external services are required.

## Assumptions

- The Hacker News `beststories.json` endpoint returns story IDs already ranked by score. **All** IDs are fetched and their details retrieved in parallel, then the full list is re-sorted by score before caching — this guarantees correct ordering regardless of any ranking drift and allows the cache to serve any value of *n* without a new API call.
- Stories without a URL (e.g. Ask HN posts) are returned with an empty `uri` string.
- The `commentCount` field maps to the `descendants` field in the Hacker News item response.

## Enhancements given more time

- **Background refresh** – proactively refresh the best stories list before it expires, eliminating cold-start latency spikes on cache miss.
- **Structured logging and observability** – expose metrics (cache hit rate, Hacker News API latency) for monitoring dashboards and alerting.
- **Integration / contract tests** – test the full pipeline end-to-end against a containerised Redis and a stubbed Hacker News API (e.g. WireMock) to catch serialisation and network-level issues.
