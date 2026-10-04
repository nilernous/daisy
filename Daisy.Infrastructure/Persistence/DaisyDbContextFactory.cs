using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Daisy.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet-ef tools (migrations). The connection string can be overridden with
/// the DAISY_CONNECTION_STRING environment variable.
/// </summary>
public sealed class DaisyDbContextFactory : IDesignTimeDbContextFactory<DaisyDbContext>
{
    public DaisyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DAISY_CONNECTION_STRING")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=Daisy;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<DaisyDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new DaisyDbContext(options);
    }
}
