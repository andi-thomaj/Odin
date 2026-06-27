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

## Single application — identity keyed on the Auth0 sub (multi-app isolation REMOVED)

This backend serves **one** application. The web (odin-charmander) and the iOS app are the same product to the
backend, so a user is **one account everywhere**: identity is `application_users.IdentityId` (the Auth0 sub),
which is **globally unique**. Reference/seed data is shared.

The former **multi-app data-isolation** capability (a separate account + data silo per `X-App` value) was
**removed** — migration `RemoveMultiAppIsolation` drops the `App` column from every user-owned table, drops the
`applications` table, and re-points the indexes (e.g. `application_users` unique on `IdentityId` alone). **Do not
reintroduce it**: there is no `X-App` header, no `AppResolutionMiddleware`, no `IAppContext`/`RequestAppContext`,
no `IAppScoped`, no `App` column, no `applications` registry/seeder. Frontends no longer send `X-App` / `?app=`.

- The auth hot path keys on the sub alone: provisioning inserts `User { IdentityId }`, role-lookup cache key is
  `UserRoleCacheKeys.ForIdentity(identityId)`.
- The **only** remaining global query filter is `RawGeneticFile`'s soft-delete (`!IsDeleted`) in
  [ApplicationDbContext](Odin.Api/Data/ApplicationDbContext.cs). Background jobs that must see soft-deleted files
  (e.g. `MergeJob`) call `IgnoreQueryFilters()` purely to bypass that — there is no app dimension anymore.
- **First admin** is still promoted via a one-time DB edit:
  `UPDATE application_users SET role='Admin' WHERE identity_id=…`.

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
- **⚠️ ALL Hangfire job-filter attributes MUST be on `IMergeJob` (the interface), not the concrete `MergeJob`.** Jobs are enqueued via `Enqueue<IMergeJob>(svc => svc.RunAsync(..))` / `AddOrUpdate<IMergeJob>(..)`, so Hangfire reads filter attributes off the **interface** method; the same attribute on the concrete method is silently ignored. This applies to `[Queue("merge")]` + `[AutomaticRetry(Attempts = 0)]` on `RunAsync` **and** `[DisableConcurrentExecution(60)]` on `DispatchPendingMergesAsync`. They were on the concrete `MergeJob` for a while, so merges actually ran on the multi-worker **`default`** queue (not the single-worker `merge` queue) AND got Hangfire's **default 10 retries** instead of `Attempts = 0`. This — combined with the invisibility bug below — let re-fetched merge jobs run **concurrently** (overlapping forges). The dispatcher had the same defect: `[DisableConcurrentExecution]` sat on the concrete method (so it was ignored), letting the every-minute recurring dispatch and the per-event dispatches (order creation, merge completion, requeue, stop) **race the count→admit step and over-admit past the cap of 1 — several merges showing "Merging" at once**. If you add any queue/retry/concurrency filter, put it on the interface. `MergeJobTests.HangfireFilters_LiveOnTheInterface_SoSerializationActuallyTakesEffect` reflection-asserts all of these so the regression can't return.
- **⚠️ Long merges + Hangfire invisibility timeout.** `UsePostgreSqlStorage` is configured with **`UseSlidingInvisibilityTimeout = true`** (+ a 2 h `InvisibilityTimeout` ceiling) in `Program.cs`. This is **load-bearing, not cosmetic**: the AADR merge is disk-bound and runs ~31 min on the shared host — longer than Hangfire.PostgreSql's **default fixed 30-min invisibility window**. Without the sliding timeout, the storage decided the still-running merge job was "lost" and **re-fetched + re-ran the same job every 30 min** — a perpetual loop (the merge never reached Succeeded/Failed; each re-run kicked off a fresh tools-api forge that again ran >30 min). The sliding timeout heartbeats the lease while a worker is genuinely processing, so a long merge runs exactly once. **Don't remove it**, and keep `ToolsApi:MergeTimeoutSeconds` (now 3600s) below that 2 h ceiling and above the real merge wall-time.
- **No automatic retries.** `RunAsync` is `[AutomaticRetry(Attempts = 0)]` (on the interface — see above); any failure (bad upload 400, panel unavailable 503, tool error 500, timeout) is recorded `MergeStatus.Failed` and does **not** rethrow — an immediate retry usually fails the same way and ties up the single worker; an admin re-runs it instead. `MergeStatus.Retrying` is now unused in the normal path; `MergeJobFailureStateFilter` still reconciles a hard-crashed (worker-died) job to Failed.
- **A failed merge must leave NO bundle on disk (bounded-disk invariant).** The tools-api itself is clean — all intermediates live in a per-merge temp workdir removed in `finally`, and the persistent `<merge_id>.tar.gz` is written only on the last step (a failure before that writes nothing; a partial tar is unlinked). The leak risk is .NET-side: a merge can fail while the tools-api **already wrote / is still writing** the bundle (a .NET client timeout doesn't cancel forge's uncancellable `run_in_threadpool`; also a lost response or a worker death). So on failure `RunCoreAsync` calls `DropAnyProducedBundleAsync` → `IMergePipelineService.CancelMergeAsync(mergeId)` (tools-api `/cancel` SIGKILLs a live forge **and** deletes any finished/partial bundle; uses `CancellationToken.None` so it runs even when the failure *was* a timeout/cancel; best-effort, never masks the original failure). **Backstop:** the weekly `CleanupOrphansAsync` also reclaims any `Failed` row that still carries a `MergeId` (the worker-died case where the inline cleanup never ran) via `TryDropFailedBundleAsync` — it deletes the bundle, keeps the `Failed` status + error, and nulls the `MergeId` (so it isn't revisited and an admin Retry mints a fresh id). Note neither the `Ready` orphan sweep nor an admin Retry would otherwise ever reclaim a `Failed` row's bundle, so this backstop is load-bearing for the 80 GB host. Covered by `MergeJobTests` (`RunAsync_MergeFailure_DropsAnyProducedBundle_SoNoTrashIsLeft`, `CleanupOrphans_ReclaimsOrphanedFailedBundle_*`).
- **Admin-initiated retry only.** `IMergeJob.RequeueAsync(rawGeneticFileId)` resets a non-running merge to `NotStarted` and dispatches; exposed as `POST api/admin/merge/{rawGeneticFileId}/retry` (`AdminOnly`) and surfaced as a **Retry** button on the Input page's results grid (admin-only, shown for `Failed` rows). FE: `src/api/merge-admin.ts` + `useRetryMerge`.
- **Admin Stop (cancel an in-progress merge).** `IMergeJob.StopAsync(rawGeneticFileId)` stops a `Queued`/`Converting`/`Merging` merge: it (1) deletes the running Hangfire `RunAsync` job (found via the monitoring API by inspection id — best-effort, so a monitoring hiccup can't block the stop) so the invocation's cancellation token fires and it never re-runs, (2) calls `IMergePipelineService.CancelMergeAsync(mergeId)` → tools-api `POST /v1/merge/{mergeId}/cancel`, which SIGKILLs the running tool subprocess (plink/trident/convertf) and drops any partial bundle, and (3) marks the file `Failed` ("Merge stopped by an administrator.") so an admin can Retry. Exposed as `POST api/admin/merge/{rawGeneticFileId}/stop` (`AdminOnly`), surfaced as a red **Stop** button on the Input grid for in-progress rows. FE: `mergeAdminApi.stop` + `useStopMerge`. (404 if the file is gone, 409 if no merge is in progress.)

NB: with trident a merge peaks at ~1.3 GB and never OOMs — no swap/host-resize needed. Merge *latency* (~31 min) is now bound by the host's slow disk, not RAM; that's an infra lever (faster disk), not a code one.

**Manual delete of a merged bundle.** `DELETE api/genetic-inspections/{id}/merged-data` (`ScientistOrAdmin`, in `GeneticInspectionEndpoints`) resolves the inspection's raw file and calls `IMergeJob.DeleteAsync` (removes the tools-api bundle, marks `Deleted`; idempotent). Surfaced on the Input results grid as a red delete button (shown for `Ready` rows), with the "Download merged data" icon shown green when a bundle exists (`mergeStatus === "Ready"`). FE: `geneticInspectionsApi.deleteMergedData` + `useDeleteMergedData`.

**Bulk delete of all merged bundles.** `DELETE api/genetic-inspections/merged-data` (`AdminOnly`, `strict` rate limit) calls `IMergeJob.DeleteAllReadyMergedDataAsync`, which deletes **every** `Ready` bundle on the tools-api volume right now (unconditional — unlike `CleanupOrphansAsync`, no retention/completed filter) and returns the count freed (`DeleteAllMergedDataContract.Response`). Each per-bundle delete is isolated so one failure doesn't abort the sweep. Surfaced on the Input results grid's toolbar as an admin-only "Delete all merged data" sweep button (badge = number of `Ready` bundles; disabled when zero). FE: `geneticInspectionsApi.deleteAllMergedData` + `useDeleteAllMergedData`. The literal `merged-data` route can't collide with `{id:int}/merged-data`.

## Public pre-launch waitlist subscribe endpoint

`POST /v1/api/public/subscribe` ([`SubscribeEndpoints`](Odin.Api/Endpoints/Subscribe/SubscribeEndpoints.cs), **`AllowAnonymous`**, `strict` rate limit) is the pre-launch waitlist signup the marketing site calls while self-service registration is disabled. It takes `{ email }`, validates it (`MailAddress.TryCreate`), and forwards to the Resend Audience via `IResendAudienceService.AddContactAsync` — **no DB entity**, Resend is the store of record for the waitlist. Resend/infra failures are **logged and masked** as `{ success: true }` so anonymous visitors never see a 500 (and we don't leak internals); only an invalid/empty email returns 400. Integration coverage: `SubscribeEndpointsTests` (the test host registers `NoOpResendAudienceService`, so no real Resend calls).

## Apple StoreKit in-app purchase — paid orders (iOS) are server-validated

The **iOS app** requires a paid in-app purchase before a qpAdm/G25 order is created; the backend verifies the Apple purchase before creating it. (The web has **no** IAP — a future web payment path is separate.)

- **Paid endpoint:** `POST /v1/api/orders/purchase` ([`OrderEndpoints`](Odin.Api/Endpoints/OrderManagement/OrderEndpoints.cs), `EmailVerified`, `file-upload` limit) — the same multipart form as `POST /orders` **plus** an `AppStoreTransaction` field (the StoreKit 2 signed transaction JWS). Handled by `OrderService.CreatePaidAsync` ([OrderService.cs](Odin.Api/Endpoints/OrderManagement/OrderService.cs)).
- **Legacy `POST /orders` is now `AdminOnly`** (was `EmailVerified`) so payment can't be bypassed. Admins keep a free back-office create path; the web is pre-launch (no public order creation). iOS and web hit the same backend and can't be told apart, so the paid flow is a dedicated endpoint rather than a branch on the caller.
- **Validation** ([`Endpoints/Payments/`](Odin.Api/Endpoints/Payments/)): `IAppStorePurchaseService.ValidateTransaction` verifies the JWS signature + Apple cert chain (`AppStoreJwsVerifier`, anchored on **Apple Root CA - G3**), then the bundle id, environment, and **product → service mapping** (a G25 product can't create a qpAdm order). Bound via `AppleIapOptions` (`AppleIap:*` — bundle id, product ids, prices, `VerifySignature`, `AppleRootCertPath`, allowed environments).
- **Signature verification is skipped under the `Testing` host env or when `AppleIap:VerifySignature=false`** (local dev / **Xcode StoreKit testing**, whose transactions are signed by a LOCAL test cert, not Apple's root) — the payload + business checks still run. **Production must set `AppleIap:AppleRootCertPath`** (the public `AppleRootCA-G3.cer`) and keep `VerifySignature=true`.
- **Idempotency / anti-replay:** every consumed purchase is recorded as an `AppStoreTransaction` (table `app_store_transactions`) with a **unique `TransactionId`** index, linked to the order it created. The transaction is recorded in the **same DB transaction** as the order. Replaying the same StoreKit transaction returns the order it already created (the iOS app replays unfinished transactions after a dropped response), so a purchase always maps to exactly one order. The order's `Price` is finally populated (from the product→price map).
- **Refunds:** `POST /v1/api/webhooks/app-store` ([`AppStoreWebhookEndpoints`](Odin.Api/Endpoints/Payments/AppStoreWebhookEndpoints.cs), `AllowAnonymous`, signature-verified) handles App Store Server Notifications V2 — `REFUND`/`REVOKE` mark the transaction `Refunded`. Configure the URL in App Store Connect.
- **Admin view:** `GET /v1/api/admin/app-store-transactions` ([`AppStoreTransactionAdminEndpoints`](Odin.Api/Endpoints/Payments/AppStoreTransactionAdminEndpoints.cs), `AdminOnly`) lists all transactions with owner + linked-order info. Surfaced in odin-react as **Admin → App Store Transactions** (`AppStoreTransactionsAdminPage`, a read-only DataGridPro).
- **Tests:** `AppStorePurchaseServiceTests` (unit, `Odin.Api.Tests`) and `OrderPurchaseEndpointsTests` (integration). The shared `TestDataHelper.CreateOrderViaApiAsync` now posts to `/orders/purchase` with a crafted unsigned test transaction (`BuildAppStoreTransactionJws`), so all downstream order tests exercise the paid path.
