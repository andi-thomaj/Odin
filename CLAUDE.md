# Backend (`Odin/`) — Claude Code rules

This file is loaded in addition to the root `CLAUDE.md` when working anywhere under `Odin/`.

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
