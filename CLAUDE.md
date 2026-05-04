# Backend (`Odin/`) — Claude Code rules

This file is loaded in addition to the root `CLAUDE.md` when working anywhere under `Odin/`.

## Integration tests — do not run the full suite after every code change

**Never** run the entire `Odin.Api.IntegrationTests` project as the default "verify my edit" step after local code changes. The suite is **slow** (real PostgreSQL, heavy seeding, many HTTP flows) and routinely takes many minutes.

**Do** run only the tests you judge **impacted** by what you changed — for example a single class, namespace, or feature area — using `--filter`:

```bash
cd Odin
dotnet test Odin.Api.IntegrationTests/Odin.Api.IntegrationTests.csproj --filter "FullyQualifiedName~CatalogEndpointsTests"
```

Other examples: `FullyQualifiedName~EraEndpointsTests`, `FullyQualifiedName~GeneticInspectionEndpointsTests`.

Reserve **full** `dotnet test` on `Odin.Api.IntegrationTests` for CI, pre-merge checks, release validation, or when your change touches **shared test infrastructure** (e.g. `CustomWebApplicationFactory`, collection fixtures, `IntegrationTestBase`, global middleware/DI used by most tests).

Unit tests (`Odin.Api.Tests`) are fast; prefer them when they cover the change. Run targeted integration tests when the behavior under test is HTTP/DB/integration-specific.
