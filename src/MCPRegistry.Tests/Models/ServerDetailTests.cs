using FluentAssertions;
using MCPRegistry.Models;
using Xunit;

namespace MCPRegistry.Tests.Models;

public class ServerDetailTests
{
    private static ServerDetail CreateMinimal() => new()
    {
        Name = "com.test/server",
        Version = "1.0.0",
        Description = "Test",
        Status = "active"
    };

    [Fact]
    public void Schema_HasDefaultValue()
    {
        var server = CreateMinimal();

        server.Schema.Should().Contain("modelcontextprotocol.io");
    }

    [Fact]
    public void Meta_IsInitializedAsEmpty()
    {
        var server = CreateMinimal();

        server.Meta.Should().NotBeNull();
        server.Meta.Should().BeEmpty();
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        var now = DateTimeOffset.UtcNow;
        var server = new ServerDetail
        {
            Name = "com.test/server",
            Version = "1.0.0",
            Description = "Test server",
            Title = "Test",
            Status = "deprecated",
            AddedAt = now,
            UpdatedAt = now,
            IsLatest = false
        };

        server.Name.Should().Be("com.test/server");
        server.Version.Should().Be("1.0.0");
        server.Status.Should().Be("deprecated");
        server.IsLatest.Should().BeFalse();
    }
}
