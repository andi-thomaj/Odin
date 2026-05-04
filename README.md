# Odin

## Docs

- [Paddle catalog setup](docs/paddle-setup.md) — end-to-end walkthrough: API keys, webhook destination, creating products, applying the migration, running the sync, verifying the catalog API.
- [Paddle `custom_data` reference](docs/paddle-custom-data.md) — focused reference for the `custom_data` shape on Paddle products, with examples for services, addons, and common mistakes.

## API integration tests (`Odin.Api.IntegrationTests`)

### One-time setup

Tests use a **real PostgreSQL** database (no Docker/Testcontainers). They apply EF migrations and reset data between tests.

1. Create a dedicated database (once), e.g. `odin_integration_test`, with credentials your API can use.
2. Prefer setting the connection string when running tests (overrides defaults):

   `ConnectionStrings__DefaultConnection` — full Npgsql connection string, for example:

   `Host=localhost;Port=5432;Database=odin_integration_test;Username=odin;Password=your_password`

3. If the variable is unset, the factory loads **Odin.Api** user secrets (`dotnet user secrets` on the API project). When the secret `ConnectionStrings:DefaultConnection` targets `ancestrify_development` or `odin_db`, the database name is rewritten to **`odin_integration_test`** so tests never hit the dev database. Create that database once on the server (`CREATE DATABASE odin_integration_test;`).

4. If there are no user secrets, defaults match [`Odin.Api/appsettings.Testing.json`](Odin.Api/appsettings.Testing.json).

### Running tests

For daily work, run **only the impacted tests** — the full suite is slow (real PostgreSQL, heavy seeding, many minutes). Targeted runs use the `FullyQualifiedName~` filter:

```bash
# from the Odin/ directory
dotnet test Odin.Api.IntegrationTests/Odin.Api.IntegrationTests.csproj --filter "FullyQualifiedName~CatalogEndpointsTests"
```

The `/be-test <class-or-namespace>` slash command wraps this. See [CLAUDE.md](CLAUDE.md) for the full agent-policy: when targeted runs are appropriate vs when to run the full suite (CI, pre-merge, or changes touching shared test infrastructure like `CustomWebApplicationFactory` / `IntegrationTestBase`).

Full-suite invocation when you really want it:

```bash
dotnet test Odin.Api.IntegrationTests/Odin.Api.IntegrationTests.csproj
```

Unit tests in `Odin.Api.Tests` are fast and a separate project — prefer them when they cover the change.
