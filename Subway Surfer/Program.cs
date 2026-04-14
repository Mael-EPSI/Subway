using SubwaySurfer.Game;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

// WebSocket endpoint — one GameSession per connection
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    var session = new GameSession(ws);
    await session.RunAsync();
});

// Leaderboard API
var scores = new List<ScoreEntry>();
var scoresLock = new object();

app.MapGet("/api/scores", () =>
{
    lock (scoresLock)
    {
        return scores.OrderByDescending(s => s.Score).Take(10).ToList();
    }
});

app.MapPost("/api/scores", (ScoreEntry entry) =>
{
    var name = entry.Name?.Trim() ?? "";
    if (name.Length == 0 || name.Length > 50 || entry.Score < 0 || entry.Score > 999_999_999)
        return Results.BadRequest();

    lock (scoresLock)
    {
        if (scores.Count >= 10_000) return Results.StatusCode(503);
        scores.Add(new ScoreEntry(name.Length > 20 ? name[..20] : name, entry.Score));
    }
    return Results.Ok();
});

app.Run();

record ScoreEntry(string Name, int Score);
