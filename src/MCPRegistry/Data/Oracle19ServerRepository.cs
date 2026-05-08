using Dapper;
using MCPRegistry.Models;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Text.Json;

namespace MCPRegistry.Data;

// Implementation targeting Oracle Database 19c.
// Notes for implementers and testers:
//  - Expected SERVERS columns: SERVER_NAME VARCHAR2, VERSION VARCHAR2, "STATUS" VARCHAR2,
//    ADDED_AT TIMESTAMP WITH TIME ZONE, UPDATED_AT TIMESTAMP WITH TIME ZONE,
//    IS_LATEST NUMBER(1), "VALUE" CLOB CHECK ("VALUE" IS JSON).
//  - JSON is stored in a CLOB column with an IS JSON check constraint.
//  - JSON_VALUE is used for searching nested JSON fields (supported since 12.1.0.2).
//  - Boolean values are represented as NUMBER(1) (0/1) since BOOLEAN type is not available until 23ai.
//  - Pagination uses OFFSET/FETCH (supported since 12c).
//  - Bind parameters use the ':' prefix. If named parameter binding fails at runtime,
//    configure OracleCommand.BindByName = true in the Dapper/Oracle command pipeline.
public class Oracle19ServerRepository : IServerRepository
{
    private readonly string _connectionString;

    public Oracle19ServerRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection CreateConnection()
    {
        var connection = new OracleConnection(_connectionString);
        return connection;
    }

    public async Task<List<ServerDetail>> GetServersAsync(
        string? cursorServerName,
        string? cursorVersion,
        int take,
        string? search,
        DateTime? updatedSince,
        string? version)
    {
        using var connection = CreateConnection();
        var sql = "SELECT \"VALUE\", \"STATUS\", ADDED_AT, UPDATED_AT, IS_LATEST FROM SERVERS WHERE 1=1";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(search))
        {
            sql += @" AND (
                SERVER_NAME LIKE :Search
                OR JSON_VALUE(""VALUE"", '$.server.title') LIKE :Search
                OR JSON_VALUE(""VALUE"", '$.server.description') LIKE :Search
            )";
            parameters.Add("Search", $"%{search}%");
        }

        if (updatedSince.HasValue)
        {
            sql += " AND UPDATED_AT >= :UpdatedSince";
            parameters.Add("UpdatedSince", updatedSince.Value);
        }

        if (!string.IsNullOrEmpty(version))
        {
            if (version == "latest")
            {
                sql += " AND IS_LATEST = 1";
            }
            else
            {
                sql += " AND VERSION = :Version";
                parameters.Add("Version", version);
            }
        }

        if (!string.IsNullOrEmpty(cursorServerName))
        {
            if (!string.IsNullOrEmpty(cursorVersion))
            {
                sql += " AND (SERVER_NAME > :CursorServerName OR (SERVER_NAME = :CursorServerName AND VERSION > :CursorVersion))";
                parameters.Add("CursorServerName", cursorServerName);
                parameters.Add("CursorVersion", cursorVersion);
            }
            else
            {
                sql += " AND SERVER_NAME > :CursorServerName";
                parameters.Add("CursorServerName", cursorServerName);
            }
        }

        sql += " ORDER BY SERVER_NAME ASC, VERSION ASC OFFSET 0 ROWS FETCH NEXT :Take ROWS ONLY";
        parameters.Add("Take", take);

        var results = await connection.QueryAsync(sql, parameters);
        return MapResults(results);
    }

    public async Task<List<ServerDetail>> GetServerVersionsAsync(string serverName)
    {
        using var connection = CreateConnection();
        const string sql = "SELECT \"VALUE\", \"STATUS\", ADDED_AT, UPDATED_AT, IS_LATEST FROM SERVERS WHERE SERVER_NAME = :Name ORDER BY ADDED_AT DESC";
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
            sql = "SELECT \"VALUE\", \"STATUS\", ADDED_AT, UPDATED_AT, IS_LATEST FROM SERVERS WHERE SERVER_NAME = :Name AND IS_LATEST = 1";
            param = new { Name = serverName };
        }
        else
        {
            sql = "SELECT \"VALUE\", \"STATUS\", ADDED_AT, UPDATED_AT, IS_LATEST FROM SERVERS WHERE SERVER_NAME = :Name AND VERSION = :Version";
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
            const string unsetLatestSql = "UPDATE SERVERS SET IS_LATEST = 0 WHERE SERVER_NAME = :Name";
            await connection.ExecuteAsync(unsetLatestSql, new { Name = server.Name }, transaction);

            const string sql = @"
                INSERT INTO SERVERS (SERVER_NAME, VERSION, ""STATUS"", UPDATED_AT, ADDED_AT, IS_LATEST, ""VALUE"")
                VALUES (:Name, :Version, :Status, :UpdatedAt, :AddedAt, :IsLatest, :Value)";

            await connection.ExecuteAsync(sql, new
            {
                Name = server.Name,
                Version = server.Version,
                Status = server.Status,
                UpdatedAt = DateTimeOffset.UtcNow,
                AddedAt = DateTimeOffset.UtcNow,
                IsLatest = server.IsLatest ? 1 : 0,
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
        // Oracle 19c stores IsLatest as NUMBER(1).
        server.IsLatest = Convert.ToInt32(result.IS_LATEST) == 1;
        return server;
    }
}
