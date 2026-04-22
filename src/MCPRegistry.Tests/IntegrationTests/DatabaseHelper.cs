using Microsoft.Data.SqlClient;

namespace MCPRegistry.Tests.IntegrationTests;

/// <summary>
/// Lightweight ADO.NET helpers used by integration tests to reset state and inspect rows that the
/// controller layer doesn't expose directly (e.g. the soft-delete <c>Status</c> column).
/// </summary>
internal static class DatabaseHelper
{
    public static async Task ClearServersAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Servers";
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<string?> GetStatusAsync(string connectionString, string serverName, string version)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT [Status] FROM Servers WHERE ServerName = @Name AND [Version] = @Version";
        command.Parameters.AddWithValue("@Name", serverName);
        command.Parameters.AddWithValue("@Version", version);
        var result = await command.ExecuteScalarAsync();

        return result as string;
    }
}
