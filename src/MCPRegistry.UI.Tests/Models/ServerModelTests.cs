using System.Text.Json;
using FluentAssertions;
using MCPRegistry.UI.Models;

namespace MCPRegistry.UI.Tests.Models;

public class ServerModelTests
{
    [Fact]
    public void ServerDetail_DefaultValues()
    {
        var server = new ServerDetail();

        server.Name.Should().BeEmpty();
        server.Version.Should().BeEmpty();
        server.Description.Should().BeNull();
        server.Title.Should().BeNull();
        server.Packages.Should().BeNull();
        server.Remotes.Should().BeNull();
        server.Repository.Should().BeNull();
    }

    [Fact]
    public void ServerDetail_SerializesCorrectly()
    {
        var server = new ServerDetail
        {
            Name = "com.test/server",
            Version = "1.0.0",
            Title = "Test",
            Description = "A test server",
            WebsiteUrl = "https://example.com",
            Repository = new RepositoryInfo { Url = "https://github.com/test/repo", Source = "github" },
            Packages = [new PackageInfo { RegistryType = "npm", Identifier = "@test/mcp", Transport = new TransportInfo() }],
            Remotes = [new RemoteInfo { Type = "streamable-http", Url = "https://api.example.com/mcp" }]
        };

        var json = JsonSerializer.Serialize(server);

        json.Should().Contain("com.test/server");
        json.Should().Contain("@test/mcp");
        json.Should().Contain("streamable-http");
        json.Should().Contain("github.com/test/repo");
    }

    [Fact]
    public void ServerDetail_DeserializesFromApiResponse()
    {
        var json = """
        {
            "name": "com.microsoft/azure",
            "version": "2.0.0-beta.6",
            "title": "Azure MCP Server",
            "description": "Azure tools",
            "websiteUrl": "https://azure.microsoft.com/",
            "repository": { "url": "https://github.com/microsoft/mcp", "source": "github" },
            "packages": [
                { "registryType": "npm", "identifier": "@azure/mcp", "version": "2.0.0-beta.6", "transport": { "$transport-type": "stdio", "type": "stdio" } }
            ],
            "remotes": [
                { "$transport-type": "streamable-http", "type": "streamable-http", "url": "https://api.example.com/mcp/" }
            ]
        }
        """;

        var server = JsonSerializer.Deserialize<ServerDetail>(json);

        server.Should().NotBeNull();
        server!.Name.Should().Be("com.microsoft/azure");
        server.Version.Should().Be("2.0.0-beta.6");
        server.Repository.Should().NotBeNull();
        server.Repository!.Source.Should().Be("github");
        server.Packages.Should().HaveCount(1);
        server.Packages![0].RegistryType.Should().Be("npm");
        server.Packages[0].Identifier.Should().Be("@azure/mcp");
        server.Packages[0].Transport!.Type.Should().Be("stdio");
        server.Remotes.Should().HaveCount(1);
        server.Remotes![0].Url.Should().Be("https://api.example.com/mcp/");
    }

    [Fact]
    public void PackageInfo_DefaultTransportIsStdio()
    {
        var transport = new TransportInfo();

        transport.Type.Should().Be("stdio");
        transport.TransportType.Should().Be("stdio");
    }

    [Fact]
    public void RemoteInfo_DefaultTransportIsStreamableHttp()
    {
        var remote = new RemoteInfo();

        remote.Type.Should().Be("streamable-http");
        remote.TransportType.Should().Be("streamable-http");
    }

    [Fact]
    public void ServerListResponse_DefaultsToEmpty()
    {
        var response = new ServerListResponse();

        response.Servers.Should().BeEmpty();
        response.Metadata.Count.Should().Be(0);
        response.Metadata.NextCursor.Should().BeNull();
    }
}
