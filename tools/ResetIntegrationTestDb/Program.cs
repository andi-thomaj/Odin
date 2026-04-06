using Npgsql;

const string dbName = "odin_integration_test";

if (args.Length is not 1)
{
    Console.Error.WriteLine("Usage: dotnet run -- <connection_string_to_postgres_database>");
    return 1;
}

var masterCs = args[0];
await using var conn = new NpgsqlConnection(masterCs);
await conn.OpenAsync();

await using (var terminate = new NpgsqlCommand(
                   $"""
                    SELECT pg_terminate_backend(pid)
                    FROM pg_stat_activity
                    WHERE datname = '{dbName.Replace("'", "''")}' AND pid <> pg_backend_pid()
                    """,
                   conn))
{
    await terminate.ExecuteNonQueryAsync();
}

await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{dbName.Replace("\"", "\"\"")}\"", conn))
{
    await drop.ExecuteNonQueryAsync();
}

await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{dbName.Replace("\"", "\"\"")}\"", conn))
{
    await create.ExecuteNonQueryAsync();
}

Console.WriteLine($"Recreated database '{dbName}'.");
return 0;
