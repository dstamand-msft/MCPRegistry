using FluentAssertions;
using MCPRegistry.Controllers;
using MCPRegistry.Models;
using MCPRegistry.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MCPRegistry.Tests.Controllers;

public class ServersControllerTests
{
    private readonly IServerRegistryService _service;
    private readonly ServersController _controller;

    public ServersControllerTests()
    {
        _service = Substitute.For<IServerRegistryService>();
        var logger = Substitute.For<ILogger<ServersController>>();
        _controller = new ServersController(_service, logger);
    }

    private static ServerDetail CreateTestServer(string name = "com.test/server", string version = "1.0.0") => new()
    {
        Name = name,
        Version = version,
        Description = "Test server",
        Title = "Test Server",
        Status = "active",
        AddedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        IsLatest = true
    };

    [Fact]
    [Trait("Type","Unit")]
    public async Task ListServers_ReturnsEmptyList_WhenNoServersExist()
    {
        _service.GetServersAsync(null, null, null, null, null)
            .Returns((new List<ServerDetail>(), (string?)null));

        var result = await _controller.ListServers(null, null, null, null, null);

        var jsonResult = result.Result.Should().BeOfType<JsonResult>().Subject;
        var serverList = jsonResult.Value.Should().BeOfType<ServerList>().Subject;
        serverList.Servers.Should().BeEmpty();
        serverList.Metadata.Should().NotBeNull();
        serverList.Metadata.Count.Should().Be(0);
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task ListServers_ReturnsServers_WhenServersExist()
    {
        var servers = new List<ServerDetail> { CreateTestServer() };
        _service.GetServersAsync(null, null, null, null, null)
                .Returns((servers, (string?)null));

        var result = await _controller.ListServers(null, null, null, null, null);

        var jsonResult = result.Result.Should().BeOfType<JsonResult>().Subject;
        var serverList = jsonResult.Value.Should().BeOfType<ServerList>().Subject;
        serverList.Servers.Should().HaveCount(1);
        serverList.Metadata.Should().NotBeNull();
        serverList.Metadata.Count.Should().Be(1);
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task ListServers_ReturnsBadRequest_WhenLimitIsZero()
    {
        var result = await _controller.ListServers(null, 0, null, null, null);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ListServers_ReturnsBadRequest_WhenLimitIsNegative()
    {
        var result = await _controller.ListServers(null, -1, null, null, null);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("1.2")]
    [InlineData("abc.def.ghi")]
    [Trait("Type", "Unit")]
    public async Task ListServers_ReturnsBadRequest_WhenVersionIsInvalidSemver(string version)
    {
        var result = await _controller.ListServers(null, null, null, null, version);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("1.0.0")]
    [InlineData("2.0.0-beta.6")]
    [InlineData("1.0.0+build.123")]
    [Trait("Type", "Unit")]
    public async Task ListServers_AcceptsValidVersionFormats(string version)
    {
        _service.GetServersAsync(null, null, null, null, version)
            .Returns((new List<ServerDetail>(), (string?)null));

        var result = await _controller.ListServers(null, null, null, null, version);

        result.Result.Should().BeOfType<JsonResult>();
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task ListServers_PassesSearchToService()
    {
        _service.GetServersAsync(null, null, "azure", null, null)
            .Returns((new List<ServerDetail>(), (string?)null));

        await _controller.ListServers(null, null, "azure", null, null);

        await _service.Received(1).GetServersAsync(null, null, "azure", null, null);
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task ListServers_Returns500_WhenServiceThrows()
    {
        _service.GetServersAsync(null, null, null, null, null)
            .ThrowsAsync(new Exception("DB error"));

        var result = await _controller.ListServers(null, null, null, null, null);

        var problemResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task ListServerVersions_ReturnsNotFound_WhenServerDoesNotExist()
    {
        _service.GetServerVersionsAsync("com.test/unknown")
            .Returns(new List<ServerDetail>());

        var result = await _controller.ListServerVersions("com.test/unknown");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task ListServerVersions_ReturnsVersions_WhenServerExists()
    {
        var versions = new List<ServerDetail>
        {
            CreateTestServer(version: "1.0.0"),
            CreateTestServer(version: "2.0.0")
        };
        _service.GetServerVersionsAsync("com.test/server")
            .Returns(versions);

        var result = await _controller.ListServerVersions("com.test/server");

        var jsonResult = result.Result.Should().BeOfType<JsonResult>().Subject;
        var serverList = jsonResult.Value.Should().BeOfType<ServerList>().Subject;
        serverList.Servers.Should().HaveCount(2);
        serverList.Metadata.Should().NotBeNull();
        serverList.Metadata.Count.Should().Be(2);
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task ListServerVersions_DecodesUrlEncodedServerName()
    {
        _service.GetServerVersionsAsync("com.test/server")
            .Returns(new List<ServerDetail> { CreateTestServer() });

        await _controller.ListServerVersions("com.test/server");

        await _service.Received(1).GetServerVersionsAsync("com.test/server");
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServerVersion_ReturnsNotFound_WhenVersionDoesNotExist()
    {
        _service.GetServerVersionAsync("com.test/server", "9.9.9")
            .Returns((ServerDetail?)null);

        var result = await _controller.GetServerVersion("com.test/server", "9.9.9");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServerVersion_ReturnsServer_WhenVersionExists()
    {
        var server = CreateTestServer();
        _service.GetServerVersionAsync("com.test/server", "1.0.0")
            .Returns(server);

        var result = await _controller.GetServerVersion("com.test/server", "1.0.0");

        var jsonResult = result.Result.Should().BeOfType<JsonResult>().Subject;
        var response = jsonResult.Value.Should().BeOfType<ServerResponse>().Subject;
        response.Server.Name.Should().Be("com.test/server");
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task GetServerVersion_IncludesMetadata()
    {
        var server = CreateTestServer();
        _service.GetServerVersionAsync("com.test/server", "1.0.0")
            .Returns(server);

        var result = await _controller.GetServerVersion("com.test/server", "1.0.0");

        var jsonResult = result.Result.Should().BeOfType<JsonResult>().Subject;
        var response = jsonResult.Value.Should().BeOfType<ServerResponse>().Subject;
        response.Meta.Should().ContainKey("io.modelcontextprotocol.registry/official");
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task DeleteServerVersion_ReturnsNotFound_WhenServerDoesNotExist()
    {
        _service.GetServerVersionAsync("com.test/server", "1.0.0")
            .Returns((ServerDetail?)null);

        var result = await _controller.DeleteServerVersion("com.test/server", "1.0.0");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task DeleteServerVersion_ReturnsOk_WhenDeleteSucceeds()
    {
        _service.GetServerVersionAsync("com.test/server", "1.0.0")
            .Returns(CreateTestServer());
        _service.DeleteServerVersionAsync("com.test/server", "1.0.0")
            .Returns(true);

        var result = await _controller.DeleteServerVersion("com.test/server", "1.0.0");

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task DeleteServerVersion_Returns500_WhenDeleteFails()
    {
        _service.GetServerVersionAsync("com.test/server", "1.0.0")
            .Returns(CreateTestServer());
        _service.DeleteServerVersionAsync("com.test/server", "1.0.0")
            .Returns(false);

        var result = await _controller.DeleteServerVersion("com.test/server", "1.0.0");

        var problemResult = result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task AddServers_ReturnsBadRequest_WhenListIsEmpty()
    {
        var result = await _controller.AddServers(new List<ServerDetail>());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task AddServers_ReturnsCreated_WhenServersAdded()
    {
        var servers = new List<ServerDetail> { CreateTestServer() };

        var result = await _controller.AddServers(servers);

        result.Should().BeOfType<CreatedResult>();
        await _service.Received(1).AddServerAsync(Arg.Any<ServerDetail>());
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task AddServers_CallsServiceForEachServer()
    {
        var servers = new List<ServerDetail>
        {
            CreateTestServer("com.test/a", "1.0.0"),
            CreateTestServer("com.test/b", "2.0.0")
        };

        await _controller.AddServers(servers);

        await _service.Received(2).AddServerAsync(Arg.Any<ServerDetail>());
    }

    [Fact]
    [Trait("Type", "Unit")]
    public async Task AddServers_Returns500_WhenServiceThrows()
    {
        _service.AddServerAsync(Arg.Any<ServerDetail>())
            .ThrowsAsync(new Exception("DB error"));

        var result = await _controller.AddServers(new List<ServerDetail> { CreateTestServer() });

        var problemResult = result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
