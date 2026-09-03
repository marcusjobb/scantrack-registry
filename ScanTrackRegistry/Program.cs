using System.Collections.Concurrent;
using System.Text.Json;

var statePath = Environment.GetEnvironmentVariable("STATE_FILE") ?? "/data/state.json";
var nodes = new ConcurrentDictionary<string, NodeEntry>(StringComparer.OrdinalIgnoreCase);
var offline = new ConcurrentDictionary<string, OfflineEntry>(StringComparer.OrdinalIgnoreCase);
var graph = BuildGraph();

LoadState(statePath, nodes, offline);

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();
builder.Services.AddSingleton(nodes);
builder.Services.AddSingleton(offline);
builder.Services.AddSingleton(graph);
builder.Services.AddHostedService<StaleNodeCleaner>();

var app = builder.Build();

app.UseCors(c => c.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

app.MapPost("/nodes", (NodeEntry entry) =>
{
    var now = DateTime.UtcNow;
    var existing = nodes.GetValueOrDefault(entry.City);
    var registeredAt = existing?.RegisteredAt ?? now;
    nodes[entry.City] = new NodeEntry(entry.City, entry.Url, registeredAt, now);
    offline.TryRemove(entry.City, out _);
    app.Logger.LogInformation("Nod registrerad/heartbeat: {City} → {Url}", entry.City, entry.Url);
    SaveState(statePath, nodes, offline);
    return Results.Ok(new { status = "registrerad", city = entry.City });
});

app.MapGet("/nodes", () =>
    Results.Ok(nodes.Values.OrderBy(n => n.City)));

app.MapDelete("/nodes/{city}", (string city) =>
{
    if (nodes.TryRemove(city, out var entry))
        offline[city] = new OfflineEntry(city, entry.Url, DateTime.UtcNow);
    SaveState(statePath, nodes, offline);
    return Results.Ok(new { status = "borttagen", city });
});

app.MapGet("/", () => Results.Content(BuildDashboard(nodes, offline, graph), "text/html"));

app.Run();

static void SaveState(
    string path,
    ConcurrentDictionary<string, NodeEntry> nodes,
    ConcurrentDictionary<string, OfflineEntry> offline)
{
    try
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var state = new { nodes = nodes.Values, offline = offline.Values };
        File.WriteAllText(path, JsonSerializer.Serialize(state));
    }
    catch { /* ingen persistent lagring monterad — OK */ }
}

static void LoadState(
    string path,
    ConcurrentDictionary<string, NodeEntry> nodes,
    ConcurrentDictionary<string, OfflineEntry> offline)
{
    try
    {
        if (!File.Exists(path)) return;
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("nodes", out var nodesEl))
            foreach (var n in nodesEl.EnumerateArray())
            {
                var entry = JsonSerializer.Deserialize<NodeEntry>(n.GetRawText());
                if (entry is not null) nodes[entry.City] = entry;
            }

        if (root.TryGetProperty("offline", out var offlineEl))
            foreach (var o in offlineEl.EnumerateArray())
            {
                var entry = JsonSerializer.Deserialize<OfflineEntry>(o.GetRawText());
                if (entry is not null) offline[entry.City] = entry;
            }
    }
    catch { /* korrupt eller saknad fil — börja fresh */ }
}

static Dictionary<string, List<string>> BuildGraph()
{
    var edges = new[]
    {
        ("Veberöd", "Malmö"),
        ("Helsingborg", "Malmö"),
        ("Helsingborg", "Göteborg"),
        ("Göteborg", "Malmö"),
        ("Göteborg", "Varberg"),
        ("Göteborg", "Jönköping"),
        ("Varberg", "Helsingborg"),
        ("Eskilstuna", "Västerås"),
        ("Eskilstuna", "Stockholm"),
        ("Göteborg", "Borås"),
        ("Göteborg", "Karlstad"),
        ("Borås", "Jönköping"),
        ("Borås", "Trollhättan"),
        ("Trollhättan", "Karlstad"),
        ("Malmö", "Jönköping"),
        ("Malmö", "Växjö"),
        ("Malmö", "Linköping"),
        ("Jönköping", "Linköping"),
        ("Jönköping", "Växjö"),
        ("Jönköping", "Skövde"),
        ("Jönköping", "Stockholm"),
        ("Växjö", "Kalmar"),
        ("Kalmar", "Linköping"),
        ("Kalmar", "Norrköping"),
        ("Linköping", "Norrköping"),
        ("Linköping", "Stockholm"),
        ("Norrköping", "Stockholm"),
        ("Skövde", "Örebro"),
        ("Karlstad", "Örebro"),
        ("Stockholm", "Örebro"),
        ("Stockholm", "Uppsala"),
        ("Stockholm", "Västerås"),
        ("Stockholm", "Gävle"),
        ("Stockholm", "Sundsvall"),
        ("Uppsala", "Gävle"),
        ("Västerås", "Örebro"),
        ("Örebro", "Gävle"),
        ("Örebro", "Falun"),
        ("Gävle", "Falun"),
        ("Gävle", "Sundsvall"),
        ("Falun", "Östersund"),
        ("Sundsvall", "Härnösand"),
        ("Sundsvall", "Umeå"),
        ("Sundsvall", "Östersund"),
        ("Umeå", "Luleå"),
        ("Umeå", "Östersund"),
        ("Kiruna", "Luleå"),
        ("Jukkasjärvi", "Veberöd"),
        ("Jukkasjärvi", "Malmö"),
        ("Jukkasjärvi", "Helsingborg"),
        ("Jukkasjärvi", "Göteborg"),
        ("Jukkasjärvi", "Varberg"),
        ("Jukkasjärvi", "Borås"),
        ("Jukkasjärvi", "Trollhättan"),
        ("Jukkasjärvi", "Karlstad"),
        ("Jukkasjärvi", "Jönköping"),
        ("Jukkasjärvi", "Växjö"),
        ("Jukkasjärvi", "Kalmar"),
        ("Jukkasjärvi", "Linköping"),
        ("Jukkasjärvi", "Norrköping"),
        ("Jukkasjärvi", "Skövde"),
        ("Jukkasjärvi", "Örebro"),
        ("Jukkasjärvi", "Stockholm"),
        ("Jukkasjärvi", "Uppsala"),
        ("Jukkasjärvi", "Västerås"),
        ("Jukkasjärvi", "Eskilstuna"),
        ("Jukkasjärvi", "Gävle"),
        ("Jukkasjärvi", "Falun"),
        ("Jukkasjärvi", "Sundsvall"),
        ("Jukkasjärvi", "Härnösand"),
        ("Jukkasjärvi", "Östersund"),
        ("Jukkasjärvi", "Umeå"),
        ("Jukkasjärvi", "Luleå"),
        ("Jukkasjärvi", "Kiruna"),
        ("Kiruna", "Jukkasjärvi"),
    };

    var g = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    foreach (var (from, to) in edges)
    {
        if (!g.ContainsKey(from)) g[from] = [];
        if (!g.ContainsKey(to)) g[to] = [];
        g[from].Add(to);
        g[to].Add(from);
    }
    return g;
}

static int CountReachable(string city, IEnumerable<string> onlineCities, Dictionary<string, List<string>> graph)
{
    var online = new HashSet<string>(onlineCities, StringComparer.OrdinalIgnoreCase);
    if (!online.Contains(city) || !graph.ContainsKey(city)) return 0;

    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { city };
    var queue = new Queue<string>();
    queue.Enqueue(city);

    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (!graph.ContainsKey(current)) continue;
        foreach (var neighbor in graph[current])
        {
            if (!visited.Contains(neighbor) && online.Contains(neighbor))
            {
                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }
    }

    return visited.Count - 1;
}

static string BuildDashboard(
    ConcurrentDictionary<string, NodeEntry> nodes,
    ConcurrentDictionary<string, OfflineEntry> offline,
    Dictionary<string, List<string>> graph)
{
    var now = DateTime.UtcNow;
    var onlineCities = nodes.Keys.ToList();

    var onlineRows = nodes.Values
        .OrderBy(n => n.City)
        .Select(n =>
        {
            var age = now - n.LastSeen;
            var ageStr = age.TotalMinutes < 2 ? "nyss" : $"{(int)age.TotalMinutes} min sedan";
            var reachable = CountReachable(n.City, onlineCities, graph);
            return $$"""
                <tr>
                  <td>{{n.City}}</td>
                  <td><a href="{{n.Url}}/status" target="_blank">{{n.Url}}</a></td>
                  <td class="center">{{reachable}}</td>
                  <td>{{n.LastSeen:HH:mm:ss}} UTC ({{ageStr}})</td>
                </tr>
                """;
        });

    var offlineRows = offline.Values
        .OrderByDescending(o => o.WentOffline)
        .Select(o =>
        {
            var ago = now - o.WentOffline;
            var agoStr = ago.TotalMinutes < 2 ? "nyss" : $"{(int)ago.TotalMinutes} min sedan";
            return $$"""
                <tr class="offline">
                  <td>{{o.City}}</td>
                  <td>{{o.Url}}</td>
                  <td class="center">—</td>
                  <td>offline sedan {{agoStr}}</td>
                </tr>
                """;
        });

    var allRows = onlineRows.Concat(offlineRows);

    return $$"""
        <!DOCTYPE html>
        <html lang="sv">
        <head>
          <meta charset="utf-8">
          <meta http-equiv="refresh" content="10">
          <title>ScanTrack Registry</title>
          <style>
            body { font-family: monospace; background: #0f1117; color: #dde3ed; padding: 2rem; }
            h1 { color: #22c55e; }
            table { border-collapse: collapse; width: 100%; }
            th { text-align: left; padding: 0.5rem 1rem; background: #1e2635; color: #67e8f9; }
            th.center { text-align: center; }
            td { padding: 0.5rem 1rem; border-bottom: 1px solid #1e2635; }
            td.center { text-align: center; }
            a { color: #67e8f9; }
            .count { color: #f97316; font-size: 1.2rem; }
            .offline td { color: #4b5563; }
            .offline td:first-child::after { content: " ⬤"; color: #ef4444; font-size: 0.6rem; vertical-align: middle; }
          </style>
        </head>
        <body>
          <h1>ScanTrack Registry</h1>
          <p class="count">{{nodes.Count}} noder online · uppdateras var 10:e sekund</p>
          <p style="color:#94a3b8;font-size:0.85rem">Noder utan heartbeat i 2 timmar tas bort automatiskt.</p>
          <table>
            <tr>
              <th>Stad</th>
              <th>URL</th>
              <th class="center">Nåbara noder</th>
              <th>Senast sedd</th>
            </tr>
            {{string.Join("\n", allRows)}}
          </table>
        </body>
        </html>
        """;
}

record NodeEntry(string City, string Url, DateTime RegisteredAt = default, DateTime LastSeen = default);
record OfflineEntry(string City, string Url, DateTime WentOffline);

internal class StaleNodeCleaner : BackgroundService
{
    private readonly ConcurrentDictionary<string, NodeEntry> _nodes;
    private readonly ConcurrentDictionary<string, OfflineEntry> _offline;
    private readonly ILogger<StaleNodeCleaner> _logger;

    public StaleNodeCleaner(
        ConcurrentDictionary<string, NodeEntry> nodes,
        ConcurrentDictionary<string, OfflineEntry> offline,
        ILogger<StaleNodeCleaner> logger)
    {
        _nodes = nodes;
        _offline = offline;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(15), ct);

            var cutoff = DateTime.UtcNow - TimeSpan.FromHours(2);
            foreach (var (city, entry) in _nodes)
            {
                if (entry.LastSeen < cutoff)
                {
                    _nodes.TryRemove(city, out _);
                    _offline[city] = new OfflineEntry(city, entry.Url, DateTime.UtcNow);
                    _logger.LogInformation("Nod borttagen (inaktiv >2h): {City}", city);
                }
            }
        }
    }
}
