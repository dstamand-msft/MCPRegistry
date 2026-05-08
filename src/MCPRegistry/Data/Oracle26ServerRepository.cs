using Dapper;
using MCPRegistry.Models;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Text.Json;

namespace MCPRegistry.Data;

// Implementation targeting Oracle Database 26ai (also compatible with 23ai+).
// Notes for implementers and testers:
//  - Expected SERVERS columns: SERVER_NAME VARCHAR2, VERSION VARCHAR2, "STATUS" VARCHAR2,
//    ADDED_AT TIMESTAMP WITH TIME ZONE, UPDATED_AT TIMESTAMP WITH TIME ZONE,
//    IS_LATEST BOOLEAN, "VALUE" JSON.
//  - JSON is stored using the native JSON datatype (introduced in 21c, hardened in 23ai/26ai).
//  - JSON dot-notation access (e.g. s."VALUE".server.title) is used for searches.
//  - Native BOOLEAN datatype (introduced in 23ai) is used for IS_LATEST.
//  - Pagination uses OFFSET/FETCH.
//  - Bind parameters use the ':' prefix. If named parameter binding fails at runtime,
//    configure OracleCommand.BindByName = true in the Dapper/Oracle command pipeline.
public class Oracle26ServerRepository : IServerRepository
{
    private readonly string _connectionString;

    public Oracle26ServerRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<List<ServerDetail>> GetServersAsync(
        string? cursorServerName,
        string? cursorVersion,
        int take,
        string? search,
        DateTime? updatedSince,
        string? version)
    {
        using var connection = CreateConnection();
        // JSON_SERIALIZE returns the native JSON column as text for client deserialization.
        var sql = "SELECT JSON_SERIALIZE(s.\"VALUE\" RETURNING CLOB) AS \"VALUE\", s.\"STATUS\", s.ADDED_AT, s.UPDATED_AT, s.IS_LATEST FROM SERVERS s WHERE 1=1";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(search))
        {
            // Dot-notation access available against native JSON columns.
            sql += @" AND (
                s.SERVER_NAME LIKE :Search
                OR s.""VALUE"".server.title LIKE :Search
                OR s.""VALUE"".server.description LIKE :Search
            )";
            parameters.Add("Search", $"%{search}%");
        }

        if (updatedSince.HasValue)
        {
            sql += " AND s.UPDATED_AT >= :UpdatedSince";
            parameters.Add("UpdatedSince", updatedSince.Value);
        }

        if (!string.IsNullOrEmpty(version))
        {
            if (version == "latest")
            {
                sql += " AND s.IS_LATEST = TRUE";
            }
            else
            {
                sql += " AND s.VERSION = :Version";
                parameters.Add("Version", version);
            }
        }

        if (!string.IsNullOrEmpty(cursorServerName))
        {
            if (!string.IsNullOrEmpty(cursorVersion))
            {
                sql += " AND (s.SERVER_NAME > :CursorServerName OR (s.SERVER_NAME = :CursorServerName AND s.VERSION > :CursorVersion))";
                parameters.Add("CursorServerName", cursorServerName);
                parameters.Add("CursorVersion", cursorVersion);
            }
            else
            {
                sql += " AND s.SERVER_NAME > :CursorServerName";
                parameters.Add("CursorServerName", cursorServerName);
            }
        }

        sql += " ORDER BY s.SERVER_NAME ASC, s.VERSION ASC OFFSET 0 ROWS FETCH NEXT :Take ROWS ONLY";
        parameters.Add("Take", take);

        var results = await connection.QueryAsync(sql, parameters);
        return MapResults(results);
    }

    public async Task<List<ServerDetail>> GetServerVersionsAsync(string serverName)
    {
        using var connection = CreateConnection();
        const string sql = "SELECT JSON_SERIALIZE(\"VALUE\" RETURNING CLOB) AS \"VALUE\", \"STATUS\", ADDED_AT, UPDATED_AT, IS_LATEST FROM SERVERS WHERE SERVER_NAME = :Name ORDER BY ADDED_AT DESC";
        var results = await connection.QueryAsync(sql, new { Name = serverName });
        return MapResults(results);
    }

    public async Task<ServerDetail?> GetServerVersionAsync(string serverName, string version)
    {
        using var connection = CreateConnection();
        string sql;
        object param;

        if (version == "latest")
        {
            sql = "SELECT JSON_SERIALIZE(\"VALUE\" RETURNING CLOB) AS \"VALUE\", \"STATUS\", ADDED_AT, UPDATED_AT, IS_LATEST FROM SERVERS WHERE SERVER_NAME = :Name AND IS_LATEST = TRUE";
            param = new { Name = serverName };
        }
        else
        {
            sql = "SELECT JSON_SERIALIZE(\"VALUE\" RETURNING CLOB) AS \"VALUE\", \"STATUS\", ADDED_AT, UPDATED_AT, IS_LATEST FROM SERVERS WHERE SERVER_NAME = :Name AND VERSION = :Version";
            param = new { Name = serverName, Version = version };
        }

        var result = await connection.QueryFirstOrDefaultAsync(sql, param);
        return result is null ? null : MapResult(result);
    }

    public async Task<bool> DeleteServerVersionAsync(string serverName, string version)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string updateSql = "UPDATE SERVERS SET \"STATUS\" = 'deleted' WHERE SERVER_NAME = :Name AND VERSION = :Version";
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
        if (server.Status != "active")
        {
            throw new Exception("Only active servers can be added.");
        }

        using var connection = CreateConnection();
        var jsonData = JsonSerializer.Serialize(server);

        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string unsetLatestSql = "UPDATE SERVERS SET IS_LATEST = FALSE WHERE SERVER_NAME = :Name";
            await connection.ExecuteAsync(unsetLatestSql, new { Name = server.Name }, transaction);

            // Cast text bind to native JSON via the JSON() constructor.
            const string sql = @"
                INSERT INTO SERVERS (SERVER_NAME, VERSION, ""STATUS"", UPDATED_AT, ADDED_AT, IS_LATEST, ""VALUE"")
                VALUES (:Name, :Version, :Status, :UpdatedAt, :AddedAt, :IsLatest, JSON(:Value))";

            await connection.ExecuteAsync(sql, new
            {
                Name = server.Name,
                Version = server.Version,
                Status = server.Status,
                UpdatedAt = DateTimeOffset.UtcNow,
                AddedAt = DateTimeOffset.UtcNow,
                IsLatest = server.IsLatest,
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

    private static List<ServerDetail> MapResults(IEnumerable<dynamic> results)
    {
        var servers = new List<ServerDetail>();
        foreach (var result in results)
        {
            var mapped = MapResult(result);
            if (mapped is not null)
            {
                servers.Add(mapped);
            }
        }
        return servers;
    }

    private static ServerDetail? MapResult(dynamic result)
    {
        string json = result.VALUE is string s ? s : Convert.ToString(result.VALUE)!;
        var server = JsonSerializer.Deserialize<ServerDetail>(json);
        if (server is null)
        {
            return null;
        }

        server.AddedAt = result.ADDED_AT;
        server.UpdatedAt = result.UPDATED_AT;
        server.Status = result.STATUS;
        server.IsLatest = Convert.ToBoolean(result.IS_LATEST);
        return server;
    }
}
