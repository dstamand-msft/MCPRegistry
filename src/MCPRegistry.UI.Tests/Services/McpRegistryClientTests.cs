using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MCPRegistry.UI.Models;
using MCPRegistry.UI.Services;

namespace MCPRegistry.UI.Tests.Services;

public class McpRegistryClientTests
{
    private class StubHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public HttpRequestMessage? CapturedRequest { get; private set; }

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public StubHandler(HttpResponseMessage response) : this(_ => response) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            return Task.FromResult(_handler(request));
        }
    }

    private static McpRegistryClient CreateClient(HttpResponseMessage response)
    {
        var handler = new StubHandler(response);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.api/") };
        return new McpRegistryClient(http);
    }

    private static HttpResponseMessage JsonResponse<T>(T data, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(data)
        };
    }

    [Fact]
    public async Task GetServersAsync_ReturnsServers()
    {
        var expected = new ServerListResponse
        {
            Servers = [new ServerResponseItem { Server = new ServerDetail { Name = "com.test/a", Version = "1.0.0" } }],
            Metadata = new ServerListMetadata { Count = 1 }
        };
        var client = CreateClient(JsonResponse(expected));

        var result = await client.GetServersAsync();

        result.Servers.Should().HaveCount(1);
        result.Servers[0].Server.Name.Should().Be("com.test/a");
    }

    [Fact]
    public async Task GetServersAsync_ReturnsEmptyList()
    {
        var expected = new ServerListResponse();
        var client = CreateClient(JsonResponse(expected));

        var result = await client.GetServersAsync();

        result.Servers.Should().BeEmpty();
    }

    [Fact]
    public async Task GetServersAsync_PassesSearchParam()
    {
        var handler = new StubHandler(JsonResponse(new ServerListResponse()));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.api/") };
        var client = new McpRegistryClient(http);

        await client.GetServersAsync("azure");

        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.RequestUri!.Query.Should().Contain("search=azure");
    }

    [Fact]
    public async Task GetServerVersionsAsync_ReturnsVersions()
    {
        var expected = new ServerListResponse
        {
            Servers =
            [
                new ServerResponseItem { Server = new ServerDetail { Name = "com.test/a", Version = "1.0.0" } },
                new ServerResponseItem { Server = new ServerDetail { Name = "com.test/a", Version = "2.0.0" } }
            ],
            Metadata = new ServerListMetadata { Count = 2 }
        };
        var client = CreateClient(JsonResponse(expected));

        var result = await client.GetServerVersionsAsync("com.test/a");

        result.Servers.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetServerVersionsAsync_EncodesServerName()
    {
        var handler = new StubHandler(JsonResponse(new ServerListResponse()));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.api/") };
        var client = new McpRegistryClient(http);

        await client.GetServerVersionsAsync("com.test/server");

        handler.CapturedRequest!.RequestUri!.AbsolutePath.Should().Contain("com.test%2Fserver");
    }

    [Fact]
    public async Task AddServersAsync_ReturnsTrue_OnSuccess()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.Created));

        var result = await client.AddServersAsync([new ServerDetail { Name = "com.test/a", Version = "1.0.0" }]);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AddServersAsync_ReturnsFalse_OnFailure()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await client.AddServersAsync([new ServerDetail { Name = "com.test/a", Version = "1.0.0" }]);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddServersAsync_SendsPostRequest()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.Created));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.api/") };
        var client = new McpRegistryClient(http);

        await client.AddServersAsync([new ServerDetail { Name = "com.test/a", Version = "1.0.0" }]);

        handler.CapturedRequest!.Method.Should().Be(HttpMethod.Post);
        handler.CapturedRequest.RequestUri!.AbsolutePath.Should().Contain("v0.1/servers");
    }

    [Fact]
    public async Task DeleteServerVersionAsync_ReturnsTrue_OnSuccess()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK));

        var result = await client.DeleteServerVersionAsync("com.test/a", "1.0.0");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteServerVersionAsync_ReturnsFalse_OnNotFound()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.DeleteServerVersionAsync("com.test/a", "9.9.9");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteServerVersionAsync_SendsDeleteRequest()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.api/") };
        var client = new McpRegistryClient(http);

        await client.DeleteServerVersionAsync("com.test/server", "1.0.0");

        handler.CapturedRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.CapturedRequest.RequestUri!.AbsolutePath.Should().Contain("com.test%2Fserver");
        handler.CapturedRequest.RequestUri.AbsolutePath.Should().Contain("1.0.0");
    }
}
