using AquacultureMonitoring.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace AquacultureMonitoring.Data;

/// <summary>Creates the same safe connection string for runtime and EF migration tooling.</summary>
public static class AquaDbConnection
{
    public static string Build(PostgreSqlOptions options) => new NpgsqlConnectionStringBuilder
    {
        Host = options.Host,
        Port = options.Port,
        Database = options.DatabaseName,
        Username = options.Username,
        Password = options.Password,
        Timeout = Math.Min(options.CommandTimeoutSeconds, 30),
        CommandTimeout = options.CommandTimeoutSeconds,
        Pooling = true,
        ApplicationName = "AquaOps"
    }.ConnectionString;
}

/// <summary>
/// Design-time factory does not connect to PostgreSQL; it only lets dotnet-ef build migrations
/// on a workstation before PostgreSQL has been installed.
/// </summary>
public sealed class AquaDbContextDesignFactory : IDesignTimeDbContextFactory<AquaDbContext>
{
    public AquaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AquaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=aquaculture_monitoring;Username=aquaops;Password=design-time-only")
            .Options;
        return new AquaDbContext(options);
    }
}
