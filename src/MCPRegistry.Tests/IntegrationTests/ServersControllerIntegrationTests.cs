using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MCPRegistry.Models;
using Xunit;

namespace MCPRegistry.Tests.IntegrationTests;

/// <summary>
/// End-to-end integration tests for <see cref="MCPRegistry.Controllers.ServersController"/> hitting a real SQL Server
/// instance hosted in a Testcontainers container.
/// </summary>
public class ServersControllerIntegrationTests : IClassFixture<MCPRegistryWebApplicationFactory>, IAsyncLifetime
{
    private readonly MCPRegistryWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ServersControllerIntegrationTests(MCPRegistryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async ValueTask InitializeAsync() => await DatabaseHelper.ClearServersAsync(_factory.ConnectionString);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ServerDetail BuildServer(string name, string version) => new()
    {
        Name = name,
        Version = version,
        Description = "Integration test server",
        Title = "Integration Test Server"
    };

    private async Task SeedAsync(params ServerDetail[] servers)
    {
        var response = await _client.PostAsJsonAsync("/v0.1/servers", servers);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Seed POST failed with {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        }
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task ListServers_ReturnsEmptyList_WhenDatabaseIsEmpty()
    {
        var response = await _client.GetAsync("/v0.1/servers", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<ServerList>(_jsonOptions, TestContext.Current.CancellationToken);
        list.Should().NotBeNull();
        list.Servers.Should().BeEmpty();
        list.Metadata!.Count.Should().Be(0);
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task ListServers_ReturnsSeededServers()
    {
        await SeedAsync(BuildServer("com.test/alpha", "1.0.0"), BuildServer("com.test/beta", "2.1.0"));

        var response = await _client.GetAsync("/v0.1/servers", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<ServerList>(_jsonOptions, TestContext.Current.CancellationToken);
        list!.Servers.Should().HaveCount(2);
        list.Servers.Select(s => s.Server.Name).Should().BeEquivalentTo(["com.test/alpha", "com.test/beta"]);
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task ListServers_FiltersBySearch()
    {
        await SeedAsync(BuildServer("com.test/alpha", "1.0.0"), BuildServer("com.test/beta", "1.0.0"));

        var response = await _client.GetAsync("/v0.1/servers?search=alpha", TestContext.Current.CancellationToken);

        var list = await response.Content.ReadFromJsonAsync<ServerList>(_jsonOptions, TestContext.Current.CancellationToken);
        list!.Servers.Should().ContainSingle();
        list.Servers[0].Server.Name.Should().Be("com.test/alpha");
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task ListServers_FiltersByLatestVersion()
    {
        await SeedAsync(BuildServer("com.test/alpha", "1.0.0"));
        await SeedAsync(BuildServer("com.test/alpha", "2.0.0"));

        var response = await _client.GetAsync("/v0.1/servers?version=latest", TestContext.Current.CancellationToken);

        var list = await response.Content.ReadFromJsonAsync<ServerList>(_jsonOptions, TestContext.Current.CancellationToken);
        list!.Servers.Should().ContainSingle();
        list.Servers[0].Server.Version.Should().Be("2.0.0");
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task ListServers_RespectsLimit()
    {
        await SeedAsync(
            BuildServer("com.test/a", "1.0.0"),
            BuildServer("com.test/b", "1.0.0"),
            BuildServer("com.test/c", "1.0.0"));

        var response = await _client.GetAsync("/v0.1/servers?limit=2", TestContext.Current.CancellationToken);

        var list = await response.Content.ReadFromJsonAsync<ServerList>(_jsonOptions, TestContext.Current.CancellationToken);
        list!.Servers.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task ListServers_ReturnsBadRequest_WhenLimitIsZero()
    {
        var response = await _client.GetAsync("/v0.1/servers?limit=0", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task ListServers_ReturnsBadRequest_WhenVersionInvalid()
    {
        var response = await _client.GetAsync("/v0.1/servers?version=not-a-semver", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task ListServerVersions_ReturnsAllVersions_ForServer()
    {
        await SeedAsync(BuildServer("com.test/alpha", "1.0.0"));
        await SeedAsync(BuildServer("com.test/alpha", "2.0.0"));

        var response = await _client.GetAsync("/v0.1/servers/com.test%2Falpha/versions", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<ServerList>(_jsonOptions, TestContext.Current.CancellationToken);
        list!.Servers.Should().HaveCount(2);
        list.Servers.Select(s => s.Server.Version).Should().BeEquivalentTo(["1.0.0", "2.0.0"]);
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task ListServerVersions_ReturnsNotFound_WhenServerMissing()
    {
        var response = await _client.GetAsync("/v0.1/servers/com.test%2Funknown/versions", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task GetServerVersion_ReturnsServer_WhenVersionExists()
    {
        await SeedAsync(BuildServer("com.test/alpha", "1.0.0"));

        var response = await _client.GetAsync("/v0.1/servers/com.test%2Falpha/versions/1.0.0", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var server = await response.Content.ReadFromJsonAsync<ServerResponse>(_jsonOptions, TestContext.Current.CancellationToken);
        server!.Server.Name.Should().Be("com.test/alpha");
        server.Server.Version.Should().Be("1.0.0");
        server.Meta.Should().ContainKey("io.modelcontextprotocol.registry/official");
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task GetServerVersion_ReturnsLatest_WhenRequestedAsLatest()
    {
        await SeedAsync(BuildServer("com.test/alpha", "1.0.0"));
        await SeedAsync(BuildServer("com.test/alpha", "2.0.0"));

        var response = await _client.GetAsync("/v0.1/servers/com.test%2Falpha/versions/latest", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var server = await response.Content.ReadFromJsonAsync<ServerResponse>(_jsonOptions, TestContext.Current.CancellationToken);
        server!.Server.Version.Should().Be("2.0.0");
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task GetServerVersion_ReturnsNotFound_WhenVersionMissing()
    {
        await SeedAsync(BuildServer("com.test/alpha", "1.0.0"));

        var response = await _client.GetAsync("/v0.1/servers/com.test%2Falpha/versions/9.9.9", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task DeleteServerVersion_SoftDeletesServer()
    {
        await SeedAsync(BuildServer("com.test/alpha", "1.0.0"));

        var deleteResponse = await _client.DeleteAsync("/v0.1/servers/com.test%2Falpha/versions/1.0.0", TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await DatabaseHelper.GetStatusAsync(_factory.ConnectionString, "com.test/alpha", "1.0.0");
        status.Should().Be("deleted");
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task DeleteServerVersion_ReturnsNotFound_WhenServerMissing()
    {
        var response = await _client.DeleteAsync("/v0.1/servers/com.test%2Fmissing/versions/1.0.0", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task AddServers_PersistsServers_AndExposesThemViaList()
    {
        var payload = new[] { BuildServer("com.test/alpha", "1.0.0"), BuildServer("com.test/beta", "1.2.3") };

        var response = await _client.PostAsJsonAsync("/v0.1/servers", payload, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.GetAsync("/v0.1/servers", TestContext.Current.CancellationToken);
        var list = await listResponse.Content.ReadFromJsonAsync<ServerList>(_jsonOptions, TestContext.Current.CancellationToken);
        list!.Servers.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task AddServers_ReturnsBadRequest_WhenListEmpty()
    {
        var response = await _client.PostAsJsonAsync("/v0.1/servers", Array.Empty<ServerDetail>(), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Type", "Integration")]
    public async Task AddServers_OnlyLatestFlagSetForNewestVersion()
    {
        await SeedAsync(BuildServer("com.test/alpha", "1.0.0"));
        await SeedAsync(BuildServer("com.test/alpha", "2.0.0"));

        var response = await _client.GetAsync("/v0.1/servers/com.test%2Falpha/versions", TestContext.Current.CancellationToken);
        var list = await response.Content.ReadFromJsonAsync<ServerList>(_jsonOptions, TestContext.Current.CancellationToken);

        var server1Meta = (JsonElement)list!.Servers.Single(s => s.Server.Version == "1.0.0").Meta["io.modelcontextprotocol.registry/official"];
        var server2Meta = (JsonElement)list.Servers.Single(s => s.Server.Version == "2.0.0").Meta["io.modelcontextprotocol.registry/official"];

        server1Meta.GetProperty("isLatest").GetBoolean().Should().BeFalse();
        server2Meta.GetProperty("isLatest").GetBoolean().Should().BeTrue();
    }
}
