using FluentAssertions;
using MCPRegistry.Data;
using MCPRegistry.Models;
using MCPRegistry.Services;
using NSubstitute;
using Xunit;

namespace MCPRegistry.Tests.Services;

public class ServerRegistryServiceTests
{
    private readonly IServerRepository _repo;
    private readonly ServerRegistryService _service;

    public ServerRegistryServiceTests()
    {
        _repo = Substitute.For<IServerRepository>();
        _service = new ServerRegistryService(_repo);
    }

    private static ServerDetail CreateTestServer(string name = "com.test/server", string version = "1.0.0") => new()
    {
        Name = name,
        Version = version,
        Description = "Test",
        Status = "active",
        AddedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        IsLatest = true
    };

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServersAsync_DefaultsTo30PageSize()
    {
        _repo.GetServersAsync(null, null, 30, null, null, null)
            .Returns(new List<ServerDetail>());

        await _service.GetServersAsync(null, null, null, null, null);

        await _repo.Received(1).GetServersAsync(null, null, 30, null, null, null);
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServersAsync_UsesProvidedLimit()
    {
        _repo.GetServersAsync(null, null, 10, null, null, null)
            .Returns(new List<ServerDetail>());

        await _service.GetServersAsync(null, 10, null, null, null);

        await _repo.Received(1).GetServersAsync(null, null, 10, null, null, null);
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServersAsync_ParsesCompositeCursor()
    {
        _repo.GetServersAsync("com.test/server", "1.0.0", 30, null, null, null)
            .Returns(new List<ServerDetail>());

        await _service.GetServersAsync("com.test/server:1.0.0", null, null, null, null);

        await _repo.Received(1).GetServersAsync("com.test/server", "1.0.0", 30, null, null, null);
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServersAsync_TreatsMalformedCursorAsServerNameOnly()
    {
        _repo.GetServersAsync("malformed-cursor", null, 30, null, null, null)
            .Returns(new List<ServerDetail>());

        await _service.GetServersAsync("malformed-cursor", null, null, null, null);

        await _repo.Received(1).GetServersAsync("malformed-cursor", null, 30, null, null, null);
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServersAsync_ReturnsNextCursor_WhenPageIsFull()
    {
        var servers = Enumerable.Range(1, 30)
            .Select(i => CreateTestServer(version: $"{i}.0.0"))
            .ToList();
        _repo.GetServersAsync(null, null, 30, null, null, null)
            .Returns(servers);

        var (_, nextCursor) = await _service.GetServersAsync(null, null, null, null, null);

        nextCursor.Should().Be("com.test/server:30.0.0");
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServersAsync_ReturnsNullCursor_WhenPageIsNotFull()
    {
        var servers = new List<ServerDetail> { CreateTestServer() };
        _repo.GetServersAsync(null, null, 30, null, null, null)
            .Returns(servers);

        var (_, nextCursor) = await _service.GetServersAsync(null, null, null, null, null);

        nextCursor.Should().BeNull();
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServersAsync_PassesSearchAndFilters()
    {
        var updatedSince = new DateTime(2026, 1, 1);
        _repo.GetServersAsync(null, null, 30, "azure", updatedSince, "latest")
            .Returns(new List<ServerDetail>());

        await _service.GetServersAsync(null, null, "azure", updatedSince, "latest");

        await _repo.Received(1).GetServersAsync(null, null, 30, "azure", updatedSince, "latest");
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServerVersionsAsync_DelegatesToRepository()
    {
        var versions = new List<ServerDetail> { CreateTestServer() };
        _repo.GetServerVersionsAsync("com.test/server")
            .Returns(versions);

        var result = await _service.GetServerVersionsAsync("com.test/server");

        result.Should().BeEquivalentTo(versions);
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServerVersionsAsync_ReturnsEmpty_WhenServerNotFound()
    {
        _repo.GetServerVersionsAsync("com.test/unknown")
            .Returns(new List<ServerDetail>());

        var result = await _service.GetServerVersionsAsync("com.test/unknown");

        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServerVersionAsync_ReturnsServer_WhenFound()
    {
        var server = CreateTestServer();
        _repo.GetServerVersionAsync("com.test/server", "1.0.0")
            .Returns(server);

        var result = await _service.GetServerVersionAsync("com.test/server", "1.0.0");

        result.Should().NotBeNull();
        result!.Name.Should().Be("com.test/server");
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServerVersionAsync_ReturnsNull_WhenNotFound()
    {
        _repo.GetServerVersionAsync("com.test/server", "9.9.9")
            .Returns((ServerDetail?)null);

        var result = await _service.GetServerVersionAsync("com.test/server", "9.9.9");

        result.Should().BeNull();
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task DeleteServerVersionAsync_ReturnsTrue_WhenDeleted()
    {
        _repo.DeleteServerVersionAsync("com.test/server", "1.0.0")
            .Returns(true);

        var result = await _service.DeleteServerVersionAsync("com.test/server", "1.0.0");

        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task DeleteServerVersionAsync_ReturnsFalse_WhenNotFound()
    {
        _repo.DeleteServerVersionAsync("com.test/server", "9.9.9")
            .Returns(false);

        var result = await _service.DeleteServerVersionAsync("com.test/server", "9.9.9");

        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task AddServerAsync_DelegatesToRepository()
    {
        var server = CreateTestServer();

        await _service.AddServerAsync(server);

        await _repo.Received(1).AddServerAsync(server);
    }
}
