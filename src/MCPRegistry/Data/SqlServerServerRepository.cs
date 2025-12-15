using Dapper;
using MCPRegistry.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace MCPRegistry.Data;

public class SqlServerServerRepository : IServerRepository
{
    private readonly string _connectionString;

    public SqlServerServerRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<List<ServerDetail>> GetServersAsync(
        string? cursorServerName,
        string? cursorVersion,
        int take,
        string? search,
        DateTime? updatedSince,
        string? version)
    {
        using var connection = CreateConnection();
        var sql = "SELECT [Value], [Status], [AddedAt], [UpdatedAt], IsLatest FROM Servers WHERE 1=1";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(search))
        {
            var searchClause = @" AND (
                ServerName LIKE @Search 
                OR JSON_VALUE([Value], '$.server.title') LIKE @Search 
                OR JSON_VALUE([Value], '$.server.description') LIKE @Search
            )";
            sql += searchClause;
            parameters.Add("Search", $"%{search}%");
        }

        if (updatedSince.HasValue)
        {
            var updatedClause = " AND UpdatedAt >= @UpdatedSince";
            sql += updatedClause;
            parameters.Add("UpdatedSince", updatedSince.Value);
        }

        if (!string.IsNullOrEmpty(version))
        {
            if (version == "latest")
            {
                var latestClause = " AND IsLatest = 1";
                sql += latestClause;
            }
            else
            {
                var versionClause = " AND Version = @Version";
                sql += versionClause;
                parameters.Add("Version", version);
            }
        }

        // Rules:
        // (server_name > @CursorServerName OR (server_name = @CursorServerName AND version > @CursorVersion))
        // Fallback for malformed cursor: treat as server name only (server_name > @CursorServerName)
        if (!string.IsNullOrEmpty(cursorServerName))
        {
            if (!string.IsNullOrEmpty(cursorVersion))
            {
                sql += " AND (ServerName > @CursorServerName OR (ServerName = @CursorServerName AND Version > @CursorVersion))";
                parameters.Add("CursorServerName", cursorServerName);
                parameters.Add("CursorVersion", cursorVersion);
            }
            else
            {
                sql += " AND ServerName > @CursorServerName";
                parameters.Add("CursorServerName", cursorServerName);
            }
        }

        // Order lexicographically by ServerName then Version to align with cursor
        sql += " ORDER BY ServerName ASC, Version ASC OFFSET 0 ROWS FETCH NEXT @Take ROWS ONLY";
        parameters.Add("Take", take);

        var results = await connection.QueryAsync(sql, parameters);

        var servers = new List<ServerDetail>();
        foreach (var result in results)
        {
            ServerDetail server = JsonSerializer.Deserialize<ServerDetail>(result.Value);
            if (server == null)
            {
                continue;
            }

            server.AddedAt = result.AddedAt;
            server.UpdatedAt = result.UpdatedAt;
            server.Status = result.Status;
            server.IsLatest = result.IsLatest;
            servers.Add(server);
        }

        return servers;
    }

    public async Task<List<ServerDetail>> GetServerVersionsAsync(string serverName)
    {
        using var connection = CreateConnection();
        var sql = "SELECT [Value], [Status], [AddedAt], [UpdatedAt], IsLatest FROM Servers WHERE ServerName = @Name ORDER BY CreatedAt DESC";
        var results = await connection.QueryAsync(sql, new { Name = serverName });

        var servers = new List<ServerDetail>();
        foreach (var result in results)
        {
            ServerDetail server = JsonSerializer.Deserialize<ServerDetail>(result.Value);
            if (server == null)
            {
                continue;
            }

            server.AddedAt = result.AddedAt;
            server.UpdatedAt = result.UpdatedAt;
            server.Status = result.Status;
            server.IsLatest = result.IsLatest;
            servers.Add(server);
        }

        return servers;
    }

    public async Task<ServerDetail?> GetServerVersionAsync(string serverName, string version)
    {
        using var connection = CreateConnection();
        string sql;
        object param;

        if (version == "latest")
        {
            sql = "SELECT [Value], [Status], [AddedAt], [UpdatedAt], IsLatest FROM Servers WHERE ServerName = @Name AND IsLatest = 1";
            param = new { Name = serverName };
        }
        else
        {
            sql = "SELECT [Value], [Status], [AddedAt], [UpdatedAt], IsLatest FROM Servers WHERE ServerName = @Name AND Version = @Version";
            param = new { Name = serverName, Version = version };
        }

        var result = await connection.QueryFirstOrDefaultAsync(sql, param);

        if (result != null)
        {
            ServerDetail server = JsonSerializer.Deserialize<ServerDetail>(result.Value);
            server.AddedAt = result.AddedAt;
            server.UpdatedAt = result.UpdatedAt;
            server.Status = result.Status;
            server.IsLatest = result.IsLatest;

            return server;
        }

        return null;
    }

    public async Task<bool> DeleteServerVersionAsync(string serverName, string version)
    {
        using var connection = CreateConnection();
        
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // soft-delete instead of hard delete
            var updateSql = "UPDATE Servers SET [Status] = 'deleted' WHERE ServerName = @Name AND Version = @Version";
            await connection.ExecuteAsync(updateSql, new { Name = serverName, Version = version }, transaction);

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task AddServerAsync(ServerDetail server)
    {
        using var connection = CreateConnection();
        
        var jsonData = JsonSerializer.Serialize(server);
        
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var unsetLatestSql = "UPDATE Servers SET IsLatest = 0 WHERE ServerName = @Name";
            await connection.ExecuteAsync(unsetLatestSql, new { Name = server.Name }, transaction);

            var sql = @"
                INSERT INTO Servers (ServerName, Version, Status, UpdatedAt, CreatedAt, IsLatest, [Value])
                VALUES (@Name, @Version, @Status, @UpdatedAt, @CreatedAt, @IsLatest, @Value)";

            await connection.ExecuteAsync(sql, new
            {
                Name = server.Name,
                Version = server.Version,
                Status = "active",
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                IsLatest = true,
                Value = jsonData
            }, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
