# Backend (`Odin/`) — Claude Code rules

This file is loaded in addition to the root `CLAUDE.md` when working anywhere under `Odin/`.

## Auth — Auth0 JWT, DB-sourced roles, `email_verified` resolution

JWT bearer is configured in [Odin.Api/Program.cs](Odin.Api/Program.cs) (`Jwt:Authority` / `Jwt:Audience`).
After auth, [RoleEnrichmentMiddleware](Odin.Api/Middleware/RoleEnrichmentMiddleware.cs) attaches an
`app_role` claim from the `application_users` table (roles are **DB-sourced on purpose** so promotions
take effect immediately) and an `app_email_verified` claim. The middleware is fully bypassed under the
`Testing` environment — [TestAuthHandler](Odin.Api/Authentication/TestAuthHandler.cs) supplies claims
from `X-Test-*` headers instead.

**`email_verified` resolution + the `/userinfo` optimization.** Auth0 API access tokens usually omit
`email_verified`, so the middleware falls back to calling Auth0 `/userinfo` (cached per-token: 15 min
verified / 2 min unverified). To remove that network call from the hot path, add a **post-login Auth0
Action** that stamps a namespaced claim onto the **access token**, then set `Jwt:EmailVerifiedClaim` to
that exact claim type:

```js
// Auth0 → Actions → Login flow
exports.onExecutePostLogin = async (event, api) => {
  const ns = "https://odin.ancestrify.io/"; // any namespace you control; must match Jwt:EmailVerifiedClaim
  if (event.authorization) {
    api.accessToken.setCustomClaim(`${ns}email_verified`, event.user.email_verified === true);
  }
};
```

Set `Jwt:EmailVerifiedClaim` (appsettings / `Jwt__EmailVerifiedClaim` env var) to
`https://odin.ancestrify.io/email_verified`. [Auth0EmailVerifiedClaims.GetJwtEmailVerifiedBoolean](Odin.Api/Authentication/Auth0EmailVerifiedClaims.cs)
reads it first; the `/userinfo` fallback stays for tokens issued before the Action, so rollout is
zero-downtime and the fallback can be retired once all live tokens carry the claim. Leaving the key
empty (the default) preserves the original `/userinfo` behaviour.

The per-request role lookup is cached via [UserRoleCacheKeys](Odin.Api/Authentication/UserRoleCacheKeys.cs)
and invalidated on every write to `User.Role` (see [UserService](Odin.Api/Endpoints/UserManagement/UserService.cs)
`UpdateUserRoleAsync` / `DeleteUserAsync`) so promotions still apply immediately.

## Multi-app data isolation — separate accounts + data per application (`X-App`)

Several frontends (odin-react = `ancestrify`, odin-aurora = `aurora`, more later) share this backend, DB, and
Auth0 tenant. The **same Auth0 sub is a SEPARATE account per app**: identity is the composite
`application_users (IdentityId, App)`, and every user-owned row carries an `App`. Reference/seed data is shared
(NOT partitioned).

How it works:
- Each frontend sends an **`X-App` header** (axios default header). SignalR can't set headers on its
  WebSocket, so its hub URL carries `?app=<key>` instead — both are read by
  [AppResolutionMiddleware](Odin.Api/Middleware/AppResolutionMiddleware.cs), which runs **after**
  `UseAuthentication` and **before** `UseRoleEnrichment`, validates against the `applications` registry
  (unknown/inactive ⇒ 400; missing ⇒ default `ancestrify`), and records the app on the scoped
  [IAppContext](Odin.Api/Authentication/IAppContext.cs).
- [ApplicationDbContext](Odin.Api/Data/ApplicationDbContext.cs) constructor-injects `IAppContext` (so it is
  **`AddDbContext`, not pooled**) and applies a global query filter `App == _appContext.App` to every
  [IAppScoped](Odin.Api/Data/Entities/IAppScoped.cs) entity, plus auto-stamps `App` on inserts in a
  `SaveChanges` override. `RawGeneticFile` folds its soft-delete predicate into that filter; `Calculator`
  uses `IsAdmin || App == current` (admin/global calculators show in every app).
- The auth hot path keys on the app: provisioning sets `App`, and the role-lookup cache key is
  `(identityId, app)`.

**When you add code, remember:**
- **New user-owned entity** ⇒ implement `IAppScoped` (add `App`, `IsRequired().HasMaxLength(50)`), add it to the
  query-filter list in `ApplicationDbContext.OnModelCreating`, and (if uniqueness is per-user) make indexes
  app-leading. New *reference* tables stay shared — don't implement `IAppScoped`.
- **Background jobs run with no HTTP request, so `IAppContext` defaults to `ancestrify`** — every job that
  reads or writes `IAppScoped` data must be made app-aware, or it silently sees/affects only ancestrify.
  Three patterns, by job shape:
  - **Per-entity job** (processes one owning entity): load it by PK with `IgnoreQueryFilters()`, then
    `RequestAppContext.SetApp(entity.App)` so the rest of the job is pinned to that entity's app. See
    [YHaplogroupComputeService](Odin.Api/Endpoints/CladeFinderManagement/YHaplogroupComputeService.cs) and
    `MergeJob.RunCoreAsync` ([MergeJob.cs](Odin.Api/Endpoints/MergeManagement/MergeJob.cs)).
  - **Global coordinator / sweep** (one shared resource serving all apps): query across apps with
    `IgnoreQueryFilters()` and do **not** pin a single app. `MergeJob.DispatchPendingMergesAsync` counts
    in-flight and picks candidates globally because the `merge` Hangfire queue is a single app-agnostic
    worker; `CleanupOrphansAsync` already sweeps cross-app. The per-merge runner it enqueues then pins the
    app per-entity (above).
  - **Cross-app batch over shared reference data**: scan every app with `IgnoreQueryFilters()` and stamp each
    new `IAppScoped` row from **its own source entity's `App`** (not the ambient context, which can't vary
    per row). See `OrderService.RecomputeG25DistanceResultsAsync`
    ([OrderService.G25Distances.cs](Odin.Api/Endpoints/OrderManagement/OrderService.G25Distances.cs)) — driven
    by a shared population-sample change, so it refreshes all apps' G25 distance results in one run.
  Reach for the per-entity pattern by default; only sweep/stamp-per-row when the job legitimately spans apps.
- **Admin user-management is app-scoped** (each app's admin manages that app — the global filter handles it; no
  cross-app super-admin view yet). Consequence: the **first admin in a new app must be promoted via a one-time
  DB edit** (`UPDATE application_users SET role='Admin' WHERE identity_id=… AND app=…`), since a fresh app login
  provisions a default `User` account. Cross-app admin queries that are intentional must use
  `IgnoreQueryFilters()`.
- **SignalR's real-time push still targets the raw sub** ([UserIdProvider](Odin.Api/Hubs/UserIdProvider.cs)), so
  a user signed into two apps may see a notification *toast* in both tabs — the *persisted* notification list is
  correctly app-scoped. App-keying the live push is a deferred enhancement.

The `applications` registry (key, display name, frontend URL, from-email, is_active) is seeded idempotently by
[ApplicationsSeeder](Odin.Api/Data/Seeders/ApplicationsSeeder.cs) (startup + after test Respawn), NOT by the
migration — so adding an app is one seed row + the frontend header, no schema change. Per-app branding columns
exist for future email/redirect use.

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

Merges run **one at a time** by **deliberate policy** (not a RAM constraint — the tools-api merges with Poseidon `trident` at ~1.3 GB; the old `mergeit` engine that needed ~25 GB was removed). The user wants merges kept strictly serialized; keep it that way.

- **Serialized — pinned to one at a time.** Merges run **strictly sequentially**: the dispatcher's in-flight cap (`MergeJob._maxInFlight`) and the Hangfire `merge`-queue `WorkerCount` are both **hardcoded to 1** in `Program.cs`. `MaxConcurrentMerges` is deliberately **not** bound from config (`Program.cs` wires `MergeJobOptions` to a literal `1`), so no appsettings/Coolify override can let two merges run concurrently. The `MergeJobOptions.MaxConcurrentMerges` setter survives only so `MergeJobTests` can exercise the dispatcher's admission arithmetic with other caps. **Do not raise these** — serialized-at-1 is a standing user preference, not a RAM limit.
- **No automatic retries.** `RunAsync` is `[AutomaticRetry(Attempts = 0)]`; any failure (bad upload 400, panel unavailable 503, tool error 500, timeout) is recorded `MergeStatus.Failed` and does **not** rethrow — an immediate retry usually fails the same way and ties up the single worker; an admin re-runs it instead. `MergeStatus.Retrying` is now unused in the normal path; `MergeJobFailureStateFilter` still reconciles a hard-crashed (worker-died) job to Failed.
- **Admin-initiated retry only.** `IMergeJob.RequeueAsync(rawGeneticFileId)` resets a non-running merge to `NotStarted` and dispatches; exposed as `POST api/admin/merge/{rawGeneticFileId}/retry` (`AdminOnly`) and surfaced as a **Retry** button on the Input page's results grid (admin-only, shown for `Failed` rows). FE: `src/api/merge-admin.ts` + `useRetryMerge`.

NB: with trident a merge peaks at ~1.3 GB and never OOMs — no swap/host-resize needed. Merge *latency* (~31 min) is now bound by the host's slow disk, not RAM; that's an infra lever (faster disk), not a code one.

**Manual delete of a merged bundle.** `DELETE api/genetic-inspections/{id}/merged-data` (`ScientistOrAdmin`, in `GeneticInspectionEndpoints`) resolves the inspection's raw file and calls `IMergeJob.DeleteAsync` (removes the tools-api bundle, marks `Deleted`; idempotent). Surfaced on the Input results grid as a red delete button (shown for `Ready` rows), with the "Download merged data" icon shown green when a bundle exists (`mergeStatus === "Ready"`). FE: `geneticInspectionsApi.deleteMergedData` + `useDeleteMergedData`.

**Bulk delete of all merged bundles.** `DELETE api/genetic-inspections/merged-data` (`AdminOnly`, `strict` rate limit) calls `IMergeJob.DeleteAllReadyMergedDataAsync`, which deletes **every** `Ready` bundle on the tools-api volume right now (unconditional — unlike `CleanupOrphansAsync`, no retention/completed filter) and returns the count freed (`DeleteAllMergedDataContract.Response`). Each per-bundle delete is isolated so one failure doesn't abort the sweep. Surfaced on the Input results grid's toolbar as an admin-only "Delete all merged data" sweep button (badge = number of `Ready` bundles; disabled when zero). FE: `geneticInspectionsApi.deleteAllMergedData` + `useDeleteAllMergedData`. The literal `merged-data` route can't collide with `{id:int}/merged-data`.

## Public pre-launch waitlist subscribe endpoint

`POST /v1/api/public/subscribe` ([`SubscribeEndpoints`](Odin.Api/Endpoints/Subscribe/SubscribeEndpoints.cs), **`AllowAnonymous`**, `strict` rate limit) is the pre-launch waitlist signup the marketing site calls while self-service registration is disabled. It takes `{ email }`, validates it (`MailAddress.TryCreate`), and forwards to the Resend Audience via `IResendAudienceService.AddContactAsync` — **no DB entity**, Resend is the store of record for the waitlist. Resend/infra failures are **logged and masked** as `{ success: true }` so anonymous visitors never see a 500 (and we don't leak internals); only an invalid/empty email returns 400. Integration coverage: `SubscribeEndpointsTests` (the test host registers `NoOpResendAudienceService`, so no real Resend calls).
