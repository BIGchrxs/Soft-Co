namespace SoftCo.Data;

/// <summary>
/// Builds the Postgres connection string for <see cref="AppDbContext"/> from configuration or the
/// environment. Reads ConnectionStrings:DefaultConnection - the same key docker-compose.yml and
/// the ECS task definition set - so local, Docker and AWS all run the identical code path and only
/// the source of the value changes.
/// </summary>
public static class DbConnectionStringFactory
{
    public static string Build(IConfiguration config, IHostEnvironment env)
    {
        var connectionString = config.GetConnectionString("DefaultConnection");

        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        if (!env.IsDevelopment())
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection must be configured (as the deployment " +
                "environment variable ConnectionStrings__DefaultConnection, sourced from AWS " +
                "Secrets Manager) before the first run in a non-Development environment.");

        // Local dev convenience only, matching docker-compose.yml's postgres service, so that
        // `docker compose up` followed by `dotnet run` works with no further configuration.
        return "Host=localhost;Port=5432;Database=softco;Username=softco;Password=softco_dev_only";
    }
}
