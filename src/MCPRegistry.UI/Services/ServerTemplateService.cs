using System.Text.Json;
using MCPRegistry.UI.Models;

namespace MCPRegistry.UI.Services;

public class ServerTemplateService
{
    private readonly IWebHostEnvironment _env;
    private List<ServerDetail>? _templates;

    public ServerTemplateService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<List<ServerDetail>> GetTemplatesAsync()
    {
        if (_templates is not null)
            return _templates;

        var dataPath = Path.Combine(_env.WebRootPath, "sample-seed-data.json");
        if (!File.Exists(dataPath))
        {
            _templates = [];
            return _templates;
        }

        var json = await File.ReadAllTextAsync(dataPath);
        _templates = JsonSerializer.Deserialize<List<ServerDetail>>(json) ?? [];
        return _templates;
    }
}
