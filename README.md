# Odin

## API integration tests (`Odin.Api.IntegrationTests`)

### One-time setup

By default the suite boots a **disposable PostgreSQL 16 container** via [Testcontainers](https://dotnet.testcontainers.org/) (`postgres:16-alpine`), applies EF migrations + seed data, and resets data between tests with Respawn. **The only prerequisite is a running Docker-API daemon:**

- **macOS / Windows:** install **Docker Desktop** (on macOS, Colima / OrbStack / Rancher Desktop also work). Testcontainers auto-detects the daemon via `/var/run/docker.sock` or the active `docker context` — no extra configuration.
- **CI:** GitHub-hosted `ubuntu-latest` has Docker preinstalled (see [.github/workflows/backend-tests.yml](.github/workflows/backend-tests.yml)).

No database to create and no connection string to set — just have Docker running, then run the tests.

#### Optional: run against an external Postgres instead of the container

Set `ConnectionStrings__DefaultConnection` (a full Npgsql connection string) before `dotnet test`; the factory then skips the container and runs Respawn against that database, for example:

`Host=localhost;Port=5432;Database=odin_integration_test;Username=odin;Password=your_password`

Create the database first (`CREATE DATABASE odin_integration_test;`). Leave the variable unset (or empty) to use the container — the default, and exactly what CI does. Defaults otherwise match [`Odin.Api/appsettings.Testing.json`](Odin.Api/appsettings.Testing.json).

> ⚠️ Respawn **wipes every public table between tests** — only point the external option at a throwaway database, never one with data you care about.

### Running tests

For daily work, run **only the impacted tests** — the full suite is slow (real PostgreSQL, heavy seeding, many minutes). Targeted runs use the `FullyQualifiedName~` filter:

```bash
# from the Odin/ directory
dotnet test Odin.Api.IntegrationTests/Odin.Api.IntegrationTests.csproj --filter "FullyQualifiedName~EraEndpointsTests"
```

The `/be-test <class-or-namespace>` slash command wraps this. See [CLAUDE.md](CLAUDE.md) for the full agent-policy: when targeted runs are appropriate vs when to run the full suite (CI, pre-merge, or changes touching shared test infrastructure like `CustomWebApplicationFactory` / `IntegrationTestBase`).

Full-suite invocation when you really want it:

```bash
dotnet test Odin.Api.IntegrationTests/Odin.Api.IntegrationTests.csproj
```

Unit tests in `Odin.Api.Tests` are fast and a separate project — prefer them when they cover the change.
