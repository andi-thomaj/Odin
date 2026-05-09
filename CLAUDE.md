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
dotnet test Odin.Api.IntegrationTests/Odin.Api.IntegrationTests.csproj --filter "FullyQualifiedName~CatalogEndpointsTests"
```

Other examples: `FullyQualifiedName~EraEndpointsTests`, `FullyQualifiedName~GeneticInspectionEndpointsTests`.

Reserve **full** `dotnet test` on `Odin.Api.IntegrationTests` for CI, pre-merge checks, release validation, or when your change touches **shared test infrastructure** (e.g. `CustomWebApplicationFactory`, collection fixtures, `IntegrationTestBase`, global middleware/DI used by most tests).

Unit tests (`Odin.Api.Tests`) are fast; prefer them when they cover the change. Run targeted integration tests when the behavior under test is HTTP/DB/integration-specific.
