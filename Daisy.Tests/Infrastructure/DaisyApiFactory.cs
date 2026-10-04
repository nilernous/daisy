using System.Text.RegularExpressions;
using Daisy.Application.Interfaces;
using Daisy.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Daisy.Tests.Infrastructure;

/// <summary>
/// Runs Daisy.Api in memory against a throw-away LocalDB database created with the EF migrations
/// and loaded with infra/database/DaisySeed.sql. The database is dropped after the test run.
/// </summary>
public sealed class DaisyApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string WebhookKey = "test-webhook-key";

    private readonly string _databaseName = $"DaisyTests_{Guid.NewGuid():N}";
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "daisy-tests", Guid.NewGuid().ToString("N"));

    public DaisyApiFactory()
    {
        // Program reads these while building the host, so they are set as environment variables.
        Set("ConnectionStrings__Daisy", ConnectionString);
        Set("Jwt__SigningKey", "integration-tests-signing-key-0123456789-abcdefghij");
        Set("VietQr__Provider", "Mock");
        Set("VietQr__BankBin", "970436");
        Set("VietQr__BankName", "Vietcombank");
        Set("VietQr__AccountNumber", "0123456789");
        Set("VietQr__AccountName", "DAISY TEST");
        Set("VietQr__WebhookApiKey", WebhookKey);
        Set("BackgroundJobs__Enabled", "false");
        Set("RateLimits__Auth", "100000");
        Set("RateLimits__Password", "100000");
        Set("RateLimits__Voucher", "100000");
        Set("Storage__RootPath", _storageRoot);
        Set("Smtp__Host", string.Empty);
    }

    public TestClock Clock { get; } = new();

    public CapturingEmailService Emails { get; } = new();

    public string ConnectionString =>
        $@"Server=(localdb)\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);
        });
    }

    public async Task InitializeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DaisyDbContext>();
            await db.Database.MigrateAsync();
        }

        await RunSeedAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(ConnectionString.Replace(_databaseName, "master"));
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END",
            connection);
        await command.ExecuteNonQueryAsync();

        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public Task WithDbAsync(Func<DaisyDbContext, Task> action) =>
        WithScopeAsync<object?>(async sp =>
        {
            await action(sp.GetRequiredService<DaisyDbContext>());
            return null;
        });

    /// <summary>Runs the real seed script, batch by batch, against the test database.</summary>
    private async Task RunSeedAsync()
    {
        var path = Path.Combine(FindRepositoryRoot(), "infra", "database", "DaisySeed.sql");
        var script = (await File.ReadAllTextAsync(path)).Replace("USE [Daisy];", string.Empty);
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Where(b => !string.IsNullOrWhiteSpace(b));

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        foreach (var batch in batches)
        {
            await using var command = new SqlCommand(batch, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Daisy.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Daisy.slnx not found.");
    }

    private static void Set(string name, string value) => Environment.SetEnvironmentVariable(name, value);
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<DaisyApiFactory>
{
    public const string Name = "Daisy API";
}
