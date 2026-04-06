# Odin

## API integration tests (`Odin.Api.IntegrationTests`)

Tests use a **real PostgreSQL** database (no Docker/Testcontainers). They apply EF migrations and reset data between tests.

1. Create a dedicated database (once), e.g. `odin_integration_test`, with credentials your API can use.
2. Prefer setting the connection string when running tests (overrides defaults):

   `ConnectionStrings__DefaultConnection` — full Npgsql connection string, for example:

   `Host=localhost;Port=5432;Database=odin_integration_test;Username=odin;Password=your_password`

3. If the variable is unset, the factory loads **Odin.Api** user secrets (`dotnet user secrets` on the API project). When the secret `ConnectionStrings:DefaultConnection` targets `ancestrify_development` or `odin_db`, the database name is rewritten to **`odin_integration_test`** so tests never hit the dev database. Create that database once on the server (`CREATE DATABASE odin_integration_test;`).

4. If there are no user secrets, defaults match [`Odin.Api/appsettings.Testing.json`](Odin.Api/appsettings.Testing.json).

From the `Odin/` directory:

`dotnet test Odin.Api.IntegrationTests/Odin.Api.IntegrationTests.csproj`
