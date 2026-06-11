# Backend (`Odin/`) — Claude Code rules

This file is loaded in addition to the root `CLAUDE.md` when working anywhere under `Odin/`.

## Caching — in-process `IMemoryCache`, single instance, invalidate on write

The API runs as a **single instance and is not designed to scale horizontally**, so caching is
plain in-process `IMemoryCache` (registered in [Odin.Api/Program.cs](Odin.Api/Program.cs)) — no
Redis / `IDistributedCache`. Follow the established pattern when caching read-mostly reference data:
`TryGetValue` → query on miss → `Set` with an `AbsoluteExpirationRelativeToNow` TTL safety net;
**skip the cache under the `Testing` environment** (`IHostEnvironment.IsEnvironment("Testing")`, so
integration tests read fresh); and **invalidate on write** via `cache.Remove(key)` in the
create/update/delete/import paths. Centralise keys in a small static class (e.g.
`OrderResultCacheKeys`, `G25SampleCacheKeys`) rather than inlining strings, and keep
[BackendCacheMaintenanceService](Odin.Api/Services/BackendCacheMaintenanceService.cs)'s "what gets
cleared" doc-comment current. Reference implementations: `EraService` (`AllEras`),
`G25CalculationService` (per-era distance samples), `PopulationService`/`EthnicityService`
(invalidation). Admin can flush everything via `POST /v1/api/admin/cache/clear`.

## API versioning — `/v1` today, side-by-side `/v2` for breaking changes

All business endpoints are mounted under `/v1` ([Odin.Api/Program.cs](Odin.Api/Program.cs) → `var v1 = app.MapGroup("/v1");`). SignalR hubs (`/hubs/...`) and infrastructure routes (`/health`, `/jobs`, `/swagger`) stay at the root by convention — they're not part of the versioned API surface.

When a request/response contract has to change in a **breaking** way (renamed field, semantic change, removed field), add a `/v2` group alongside `/v1` rather than mutating the existing endpoint. Same handler can serve both by accepting a wider DTO and projecting per version, or each version can have its own endpoint methods — pick whichever keeps the per-endpoint code clearest. `/v1` stays alive until the FE schema regenerates, the FE migrates, and a deprecation window passes.

Non-breaking changes (new field on a response, new optional field on a request) can ship in place on `/v1` — they're already non-breaking by construction. Don't pre-emptively v2 for these.

## Integration test database — Testcontainers Postgres

`Odin.Api.IntegrationTests` boots a disposable **PostgreSQL 16 container** (via `Testcontainers.PostgreSql`) on every test run. Running the suite locally requires Docker. CI uses GitHub-hosted `ubuntu-latest`, where Docker is preinstalled — see [.github/workflows/backend-tests.yml](.github/workflows/backend-tests.yml), which runs both `Odin.Api.Tests` (unit) and `Odin.Api.IntegrationTests` as parallel jobs on every PR and on pushes to `master` / `development`.

To target an **external** Postgres instead (e.g. a locally running dev database), set `ConnectionStrings__DefaultConnection` in the environment before invoking `dotnet test`. The factory skips the container when the env var is non-empty and runs Respawn against the supplied database. Leave it unset (or empty) to use the container.

Do not point integration tests at a database that holds data you care about — Respawn wipes every public table between tests.

## Integration tests — do not run the full suite after every code change

**Never** run the entire `Odin.Api.IntegrationTests` project as the default "verify my edit" step after local code changes. The suite is **slow** (Postgres container boot, heavy seeding, many HTTP flows) and routinely takes many minutes.

**Do** run only the tests you judge **impacted** by what you changed — for example a single class, namespace, or feature area — using `--filter`:

```bash
cd Odin
dotnet test Odin.Api.IntegrationTests/Odin.Api.IntegrationTests.csproj --filter "FullyQualifiedName~EraEndpointsTests"
```

Other examples: `FullyQualifiedName~GeneticInspectionEndpointsTests`, `FullyQualifiedName~OrderEndpointsTests`.

Reserve **full** `dotnet test` on `Odin.Api.IntegrationTests` for CI, pre-merge checks, release validation, or when your change touches **shared test infrastructure** (e.g. `CustomWebApplicationFactory`, collection fixtures, `IntegrationTestBase`, global middleware/DI used by most tests).

Unit tests (`Odin.Api.Tests`) are fast; prefer them when they cover the change. Run targeted integration tests when the behavior under test is HTTP/DB/integration-specific.

## After changing a request or response contract — refresh the FE OpenAPI client

The frontend consumes BE types via [`odin-react/src/api/_generated/schema.d.ts`](../odin-react/src/api/_generated/schema.d.ts), produced by `npm run gen:api` from the Swashbuckle doc at `/swagger/v1/swagger.json`. **Whenever you add, rename, or change a field on any `*Contract.Request` / `*Contract.Response` (or add/remove an endpoint), the FE schema is stale until it's regenerated.**

After the BE change builds:

1. **Run the BE locally** so the OpenAPI doc updates: `cd Odin/Odin.Api && dotnet run` (listens on 5190).
2. **In a second terminal, from `odin-react/`:** `npm run gen:api` — overwrites `src/api/_generated/schema.d.ts`.
3. **Commit the regenerated `schema.d.ts` in the same commit/PR** as the BE contract change. CI should treat schema drift between BE and committed FE snapshot as a failure.

If you add a new endpoint, also remember to annotate the success response on the route registration so it appears in the OpenAPI doc:

```csharp
endpoints.MapGet("/foo", GetFoo)
    .RequireAuthorization("EmailVerified")
    .Produces<GetFooContract.Response>(StatusCodes.Status200OK);
```

Without `.Produces<T>()`, Swashbuckle emits an empty `content` for the 200 response and `gen:api` won't expose the type to the FE.

## Admin merge-panel restore — streamed multi-GB upload (raised limits)

The AADR `HO` merge panel is a pre-built upload (`v66_2M_aadr_PUB.{geno,snp,ind}`), not Poseidon-provisioned. `MergePanelAdminEndpoints` (`api/admin/merge-panel/*`, `AdminOnly`) proxies the tools-api `/v1/merge/panel/restore/*` flow: `GET status`, `POST upload` (one file at a time), `POST activate`. The frontend surfaces it as Admin → "Restore merge panel".

These files are **2–10 GB**, so the upload path deliberately departs from the global 50 MB limits:

- **Streamed, never buffered.** The browser sends each file as the **raw request body** (`application/octet-stream`); `ext`/`panel`/`sha256` ride as query params. The `upload` handler pipes `HttpContext.Request.Body` straight into `StreamContent` to the tools-api (`MergePipelineService.UploadPanelFileAsync`) — no `IFormFile`/multipart, no memory or temp-file spool.
- **Per-route Kestrel cap lifted.** The handler sets `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize = null` (the global cap in `Program.cs` stays 50 MB for every other route). Must run before the body is read — hence reading `Request.Body` directly rather than model-binding a form.
- **Dedicated infinite-timeout client.** `MergePipelineService.PanelClientName` ("ToolsApiPanelRestore") has `Timeout = Timeout.InfiniteTimeSpan` so a slow multi-GB upload isn't killed by the 30-min merge-client timeout; it's bounded instead by the endpoint's **`PanelRestore`** request-timeout policy (2 h) + the request cancellation token.
- **Reverse proxy is the likely failure point.** Coolify/Traefik enforces its own request-body-size and read/idle timeouts; these must be raised for this route on the server (not in code), or a large upload 413s/times out before reaching .NET.

The 422 from activate carries a human-readable validation message (transposed `.geno`, `???` labels, truncated `.geno`); the FE shows it in a toast. Pass `force=true` to install despite "slow but usable" warnings.

## Panel sample labels — Scientist/Admin editor

`MergePanelLabelsEndpoints` (`api/merge-panel/labels`, `ScientistOrAdmin`) proxies the tools-api `/v1/merge/panel/ind*` routes so Scientists/Admins can correct the AADR panel's **population labels** (column 3 of the `.ind`) without re-uploading the multi-GB panel: `GET /` (list samples), `PUT /row` (edit one by row index), `POST /rename` (rename a label across all samples that have it). The frontend page is Tools → "Panel Labels" (`requireScientistOrAdminBeforeLoad`, `DataGridPro` inline edit + rename dialog).

The tools-api enforces the invariants (text-only on col 3, order-preserving — the `.geno` is keyed by row position; no whitespace in a label; atomic write + `*.ind.bak`); these endpoints just relay via `MergePipelineService` and map a 422 (validation) / 502 (tools-api unreachable) through the shared `Proxy(...)` helper. Adding methods to `IMergePipelineService` means the `StubMergeService` in `Odin.Api.Tests/.../MergeJobTests.cs` must implement them too, or the test project won't compile.

## Merge execution model — serialized, no auto-retry, admin-only retry

The AADR merge is memory-heavy (`mergeit` on the 2M panel needs **~25 GB RAM**), so the shared 30 GB host (both envs co-located) can only run **one at a time** — two concurrent merges OOM-kill each other (`mergeit` `exit -9`).

- **Serialized — pinned to one at a time.** Merges run **strictly sequentially**: the dispatcher's in-flight cap (`MergeJob._maxInFlight`) and the Hangfire `merge`-queue `WorkerCount` are both **hardcoded to 1** in `Program.cs`. `MaxConcurrentMerges` is deliberately **not** bound from config (`Program.cs` wires `MergeJobOptions` to a literal `1`), so no appsettings/Coolify override can let two ~25 GB merges run concurrently and OOM-kill each other. The `MergeJobOptions.MaxConcurrentMerges` setter survives only so `MergeJobTests` can exercise the dispatcher's admission arithmetic with other caps. To genuinely allow concurrent merges in future you must change the literals in `Program.cs` (and have the RAM headroom).
- **No automatic retries.** `RunAsync` is `[AutomaticRetry(Attempts = 0)]`; any failure (bad upload 400, panel unavailable 503, OOM-killed `mergeit` 500, timeout) is recorded `MergeStatus.Failed` and does **not** rethrow — an auto-retry of a 25 GB merge just OOMs again and ties up the single worker. `MergeStatus.Retrying` is now unused in the normal path; `MergeJobFailureStateFilter` still reconciles a hard-crashed (worker-died) job to Failed.
- **Admin-initiated retry only.** `IMergeJob.RequeueAsync(rawGeneticFileId)` resets a non-running merge to `NotStarted` and dispatches; exposed as `POST api/admin/merge/{rawGeneticFileId}/retry` (`AdminOnly`) and surfaced as a **Retry** button on the Input page's results grid (admin-only, shown for `Failed` rows). FE: `src/api/merge-admin.ts` + `useRetryMerge`.

NB: serialized + no-retry stops the OOM *storm*, but a single big merge still needs ~25 GB — until the host gets more RAM or swap, large merges will land in `Failed` and admin retry won't help them (only the smaller-SNP-overlap merges that fit in the free RAM succeed).

**Manual delete of a merged bundle.** `DELETE api/genetic-inspections/{id}/merged-data` (`ScientistOrAdmin`, in `GeneticInspectionEndpoints`) resolves the inspection's raw file and calls `IMergeJob.DeleteAsync` (removes the tools-api bundle, marks `Deleted`; idempotent). Surfaced on the Input results grid as a red delete button (shown for `Ready` rows), with the "Download merged data" icon shown green when a bundle exists (`mergeStatus === "Ready"`). FE: `geneticInspectionsApi.deleteMergedData` + `useDeleteMergedData`.
