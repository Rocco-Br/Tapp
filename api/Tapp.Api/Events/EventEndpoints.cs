using Microsoft.EntityFrameworkCore;
using Tapp.Api.Auth;
using Tapp.Api.Data;

namespace Tapp.Api.Events;

public record StartEventRequest(string Name, bool CarryOverStock);

public static class EventEndpoints
{
    public static void MapEvents(this WebApplication app)
    {
        var events = app.MapGroup("/api/events");

        events.MapGet("current", async (AppDbContext db) =>
            await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.IsRunning) is { } e ? Results.Ok(e) : Results.NotFound())
            .RequireAuthorization();

        events.MapGet("", async (AppDbContext db) =>
            await db.Events.AsNoTracking().OrderByDescending(e => e.Id).ToListAsync())
            .RequireAuthorization(Roles.Admin);

        events.MapPost("", async (StartEventRequest r, AppDbContext db, HttpContext http) =>
        {
            if (string.IsNullOrWhiteSpace(r.Name))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Required"] });
            if (await db.Events.AnyAsync(e => e.IsRunning))
                return Results.Conflict(new { error = "An event is already running" });

            await using var tx = await db.Database.BeginTransactionAsync();
            if (!r.CarryOverStock)
            {
                // Start empty: zero everything, and log it so the stock history stays complete.
                var user = http.User.Identity!.Name!;
                foreach (var item in await db.StockItems.Where(s => s.Quantity != 0).ToListAsync())
                {
                    db.StockLogs.Add(new StockLog { StockItemId = item.Id, Delta = -item.Quantity, Reason = $"New event \"{r.Name.Trim()}\": start empty", User = user, At = DateTime.UtcNow });
                    item.Quantity = 0;
                }
            }

            var ev = new Event { Name = r.Name.Trim(), StartedAt = DateTime.UtcNow, IsRunning = true };
            db.Events.Add(ev);
            try { await db.SaveChangesAsync(); }
            // Two admins starting at once: the unique index lets only one win.
            catch (DbUpdateException) { return Results.Conflict(new { error = "An event is already running" }); }
            await tx.CommitAsync();
            return Results.Created("/api/events/current", ev);
        }).RequireAuthorization(Roles.Admin);

        events.MapPost("current/stop", async (AppDbContext db) =>
        {
            var ev = await db.Events.FirstOrDefaultAsync(e => e.IsRunning);
            if (ev is null) return Results.NotFound();
            ev.IsRunning = false;
            ev.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ev);
        }).RequireAuthorization(Roles.Admin);
    }
}
