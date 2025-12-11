using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using MCPRegistry.Models;
using MCPRegistry.Services;

namespace MCPRegistry.Controllers;

[ApiController]
[Route("v0.1/[controller]")]
public class ServersController : ControllerBase
{
    private readonly IServerRegistryService _registryService;
    private readonly ILogger<ServersController> _logger;

    // Regex to validate semantic versioning (semver) format
    // follows Backus–Naur Form Grammar for Valid SemVer Versions (https://semver.org/#backusnaur-form-grammar-for-valid-semver-versions)
    private readonly Regex _versionRegex = new Regex(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|[0-9A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|[0-9A-Za-z-][0-9A-Za-z-]*))*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ServersController(IServerRegistryService registryService, ILogger<ServersController> logger)
    {
        _registryService = registryService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a paginated list of servers that match the specified filter criteria.
    /// </summary>
    /// <remarks>Use the returned NextCursor value from the response metadata to retrieve subsequent pages of
    /// results. If no more results are available, NextCursor will be null or empty.</remarks>
    /// <param name="cursor">An optional pagination cursor indicating the position from which to retrieve the next set of results. Pass null
    /// or omit to start from the beginning.</param>
    /// <param name="limit">The maximum number of servers to return in the response. Must be a positive integer if specified.</param>
    /// <param name="search">An optional search term to filter servers by name (substring match). If null or empty, no search
    /// filtering is applied.</param>
    /// <param name="updated_since">An optional timestamp to return only servers that have been updated since the specified date and time (RFC3339 datetime). If null,
    /// no update time filtering is applied.</param>
    /// <param name="version">An optional server version string to filter the results. If null or empty, servers of all versions are included. Use 'latest' for latest version or an exact version (1.2.3)</param>
    /// <returns>An ActionResult containing a ServerList object with the matching servers and pagination metadata. Returns a 200
    /// OK response with the results, or a 500 Internal Server Error if an unexpected error occurs.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ServerList), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ServerList>> ListServers(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        [FromQuery] string? search,
        [FromQuery] DateTime? updated_since,
        [FromQuery] string? version)
    {
        if (limit.HasValue && limit <= 0)
        {
            return BadRequest("Limit must be a positive integer");
        }

        if (!string.IsNullOrWhiteSpace(version) &&
            !string.Equals(version, "latest", StringComparison.OrdinalIgnoreCase) &&
            !_versionRegex.IsMatch(version))
        {
            return BadRequest("Version is not valid. It should follow the semver format.");
        }

        try
        {
            var (servers, nextCursor, count) = await _registryService.GetServersAsync(
                cursor, limit, search, updated_since, version);

            var response = new ServerList
            {
                Servers = servers,
                Metadata = new ServerListMetadata
                {
                    NextCursor = nextCursor,
                    Count = count
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            // TODO: add correlation ID to logs and response
            _logger.LogError(ex, "Error listing servers");
            return Problem("Internal server error", statusCode: StatusCodes.Status500InternalServerError, title: "Internal server error");
        }
    }


    /// <summary>
    /// Retrieves a list of available versions for the specified server.
    /// </summary>
    /// <remarks>Returns a 404 Not Found response if the specified server does not exist. The response
    /// includes metadata with the total count of available versions.</remarks>
    /// <param name="serverName">The name of the server for which to retrieve available versions. This value is case-sensitive and must not be
    /// null or empty.</param>
    /// <returns>An <see cref="ActionResult{T}"/> containing a <see cref="ServerList"/> with the available server versions if the
    /// server exists; otherwise, a 404 Not Found response with an error message.</returns>
    [HttpGet("{serverName}/versions")]
    [ProducesResponseType(typeof(ServerList), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ServerList>> ListServerVersions(string serverName)
    {
        try
        {
            var decodedServerName = Uri.UnescapeDataString(serverName);
            var (versions, count) = await _registryService.GetServerVersionsAsync(decodedServerName);

            if (versions.Count == 0)
            {
                return NotFound("Server not found");
            }

            var response = new ServerList
            {
                Servers = versions,
                Metadata = new ServerListMetadata
                {
                    Count = count
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing server versions for {ServerName}", serverName);
            return Problem("Internal server error", statusCode: StatusCodes.Status500InternalServerError, title: "Internal server error");
        }
    }

    /// <summary>
    /// Retrieves information about a specific version of a server by server name and version identifier.
    /// </summary>
    /// <remarks>Returns a 500 Internal Server Error response if an unexpected error occurs during
    /// processing.</remarks>
    /// <param name="serverName">The name of the server to retrieve. This value should be URL-encoded if it contains special characters.</param>
    /// <param name="version">The version identifier of the server to retrieve. This value should be URL-encoded if it contains special
    /// characters.</param>
    /// <returns>An <see cref="ActionResult{T}"/> containing a <see cref="ServerResponse"/> with the server version details if
    /// found; otherwise, a 404 Not Found response with an <see cref="ProblemDetails"/>.</returns>
    [HttpGet("{serverName}/versions/{version}")]
    [ProducesResponseType(typeof(ServerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ServerResponse>> GetServerVersion(string serverName, string version)
    {
        try
        {
            var decodedServerName = Uri.UnescapeDataString(serverName);
            var decodedVersion = Uri.UnescapeDataString(version);

            var serverVersion = await _registryService.GetServerVersionAsync(decodedServerName, decodedVersion);

            if (serverVersion == null)
            {
                return NotFound("Server not found");
            }

            return Ok(serverVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting server version {ServerName}@{Version}", serverName, version);
            return Problem($"Error getting server version {serverName}@{version}", statusCode: StatusCodes.Status500InternalServerError, title: "Internal server error");
        }
    }

    /// <summary>
    /// Delete specific version of an MCP server
    /// </summary>
    [HttpDelete("{serverName}/versions/{version}")]
    [ProducesResponseType(typeof(ServerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteServerVersion(string serverName, string version)
    {
        try
        {
            var decodedServerName = Uri.UnescapeDataString(serverName);
            var decodedVersion = Uri.UnescapeDataString(version);

            var serverVersion = await _registryService.GetServerVersionAsync(decodedServerName, decodedVersion);
            if (serverVersion == null)
            {
                return NotFound("Server version not found");
            }

            var deleted = await _registryService.DeleteServerVersionAsync(decodedServerName, decodedVersion);

            if (!deleted)
            {
                return Problem("Failed to delete server version", statusCode: StatusCodes.Status500InternalServerError, title: "Internal server error");
            }

            return Ok(serverVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting server version {ServerName}@{Version}", serverName, version);
            return Problem("Failed to delete server version", statusCode: StatusCodes.Status500InternalServerError, title: "Internal server error");
        }
    }
}
