using MCPRegistry.UI.Models;

namespace MCPRegistry.UI.Services;

public class McpRegistryClient
{
    private readonly HttpClient _http;

    public McpRegistryClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ServerListResponse> GetServersAsync(string? search = null)
    {
        var url = "v0.1/servers";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"?search={Uri.EscapeDataString(search)}";

        return await _http.GetFromJsonAsync<ServerListResponse>(url) ?? new ServerListResponse();
    }

    public async Task<ServerListResponse> GetServerVersionsAsync(string serverName)
    {
        var encoded = Uri.EscapeDataString(serverName);
        return await _http.GetFromJsonAsync<ServerListResponse>($"v0.1/servers/{encoded}/versions") ?? new ServerListResponse();
    }

    public async Task<bool> AddServersAsync(List<ServerDetail> servers)
    {
        var response = await _http.PostAsJsonAsync("v0.1/servers", servers);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteServerVersionAsync(string serverName, string version)
    {
        var encoded = Uri.EscapeDataString(serverName);
        var response = await _http.DeleteAsync($"v0.1/servers/{encoded}/versions/{Uri.EscapeDataString(version)}");
        return response.IsSuccessStatusCode;
    }
}
