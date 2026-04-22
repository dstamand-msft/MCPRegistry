using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.SqlServer.Dac;
using Testcontainers.MsSql;
using Xunit;

namespace MCPRegistry.Tests.IntegrationTests;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> that spins up a real SQL Server instance via Testcontainers,
/// publishes the MCPRegistryDatabase dacpac to it, and rewires the API connection string to point at the container.
/// </summary>
public class MCPRegistryWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string _databaseName = "MCPRegistryTests";

    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private string _connectionString = string.Empty;

    /// <summary>
    /// Gets the connection string targeting the deployed <c>MCPRegistry</c> database in the test container.
    /// </summary>
    public string ConnectionString => _connectionString;

    public async ValueTask InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        var masterConnection = _sqlContainer.GetConnectionString();
        _connectionString = ReplaceDatabase(masterConnection, _databaseName);

        TestContext.Current.TestOutputHelper?.WriteLine("Using connection string: {0}", _connectionString);

        DeployDacpac(masterConnection);

        // Force the host to be created so configuration overrides apply before the first request.
        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString
            });
        });
    }

    private void DeployDacpac(string masterConnectionString)
    {
        var dacpacPath = LocateDacpac();
        var services = new DacServices(masterConnectionString);

        using var package = DacPackage.Load(dacpacPath);
        var deployOptions = new DacDeployOptions
        {
            CreateNewDatabase = true,
            BlockOnPossibleDataLoss = false,
            IncludeTransactionalScripts = false
        };

        services.Deploy(package, _databaseName, upgradeExisting: true, options: deployOptions);
    }

    private static string LocateDacpac()
    {
        // The MCPRegistryDatabase project is referenced by the test project so MSBuild builds the dacpac
        // before the tests run. We probe a few well-known locations relative to the test output folder.
        var baseDir = AppContext.BaseDirectory;

        var candidates = new[]
        {
            Path.Combine(baseDir, "MCPRegistryDatabase.dacpac"),
            // TODO: modify this to be able to get the current build configuration at runtime
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "MCPRegistryDatabase", "bin", "Debug", "MCPRegistryDatabase.dacpac")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "MCPRegistryDatabase", "bin", "Release", "MCPRegistryDatabase.dacpac"))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException($"Could not locate MCPRegistryDatabase.dacpac. Probed: {string.Join(", ", candidates)}");
    }

    private static string ReplaceDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName,
            TrustServerCertificate = true
        };

        return builder.ConnectionString;
    }
}
