using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();

var app = builder.Build();

// CORS: noderna kommer från olika ACI-origins
app.UseCors(c => c.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

// Nod-registret: stad → (url, registrerad)
var nodes = new ConcurrentDictionary<string, NodeEntry>(StringComparer.OrdinalIgnoreCase);

// POST /nodes — en nod registrerar sig
app.MapPost("/nodes", (NodeEntry entry) =>
{
    entry = entry with { RegisteredAt = DateTime.UtcNow };
    nodes[entry.City] = entry;
    app.Logger.LogInformation("Nod registrerad: {City} → {Url}", entry.City, entry.Url);
    return Results.Ok(new { status = "registrerad", city = entry.City });
});

// GET /nodes — hämta alla registrerade noder
app.MapGet("/nodes", () =>
    Results.Ok(nodes.Values.OrderBy(n => n.City)));

// DELETE /nodes/{city} — avregistrera (om en nod stängs ned)
app.MapDelete("/nodes/{city}", (string city) =>
{
    nodes.TryRemove(city, out _);
    return Results.Ok(new { status = "borttagen", city });
});

// GET / — enkel HTML-dashboard för Marcus att kolla live under lektionstid
app.MapGet("/", () => Results.Content(BuildDashboard(nodes), "text/html"));

app.Run();

static string BuildDashboard(ConcurrentDictionary<string, NodeEntry> nodes)
{
    var rows = nodes.Values
        .OrderBy(n => n.City)
        .Select(n => $"""
            <tr>
              <td>{n.City}</td>
              <td><a href="{n.Url}/status" target="_blank">{n.Url}</a></td>
              <td>{n.RegisteredAt:HH:mm:ss} UTC</td>
            </tr>
            """);

    return $"""
        <!DOCTYPE html>
        <html lang="sv">
        <head>
          <meta charset="utf-8">
          <meta http-equiv="refresh" content="10">
          <title>ScanTrack Registry</title>
          <style>
            body {{ font-family: monospace; background: #0f1117; color: #dde3ed; padding: 2rem; }}
            h1 {{ color: #22c55e; }}
            table {{ border-collapse: collapse; width: 100%; }}
            th {{ text-align: left; padding: 0.5rem 1rem; background: #1e2635; color: #67e8f9; }}
            td {{ padding: 0.5rem 1rem; border-bottom: 1px solid #1e2635; }}
            a {{ color: #67e8f9; }}
            .count {{ color: #f97316; font-size: 1.2rem; }}
          </style>
        </head>
        <body>
          <h1>ScanTrack Registry</h1>
          <p class="count">{nodes.Count} noder online · uppdateras var 10:e sekund</p>
          <table>
            <tr><th>Stad</th><th>URL</th><th>Registrerad</th></tr>
            {string.Join("\n", rows)}
          </table>
        </body>
        </html>
        """;
}

record NodeEntry(string City, string Url, DateTime RegisteredAt = default);
