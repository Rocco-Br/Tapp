using Microsoft.EntityFrameworkCore;
using Tapp.Api.Auth;
using Tapp.Api.Data;

namespace Tapp.Api.Stock;

public record StockItemRequest(string Name, string Unit);
public record StockItemDto(int Id, string Name, string Unit, int Quantity);
public record CorrectionRequest(int Delta, string Reason);

public static class StockEndpoints
{
    public static void MapStock(this WebApplication app)
    {
        var stock = app.MapGroup("/api/stock");

        stock.MapGet("", async (AppDbContext db) =>
            await db.StockItems.AsNoTracking().OrderBy(s => s.Name)
                .Select(s => new StockItemDto(s.Id, s.Name, s.Unit, s.Quantity)).ToListAsync())
            .RequireAuthorization(Roles.Staff);

        // Quantity starts at 0; every change goes through a correction so the log is complete.
        stock.MapPost("", async (StockItemRequest r, AppDbContext db) =>
        {
            if (Invalid(r) is { } problem) return problem;
            var item = new StockItem { Name = r.Name.Trim(), Unit = r.Unit.Trim() };
            db.StockItems.Add(item);
            await db.SaveChangesAsync();
            return Results.Created($"/api/stock/{item.Id}", new StockItemDto(item.Id, item.Name, item.Unit, item.Quantity));
        }).RequireAuthorization(Roles.Admin);

        stock.MapPut("{id:int}", async (int id, StockItemRequest r, AppDbContext db) =>
        {
            if (Invalid(r) is { } problem) return problem;
            var item = await db.StockItems.FindAsync(id);
            if (item is null) return Results.NotFound();
            item.Name = r.Name.Trim();
            item.Unit = r.Unit.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(new StockItemDto(item.Id, item.Name, item.Unit, item.Quantity));
        }).RequireAuthorization(Roles.Admin);

        // Refused while a product or option still uses the item; its log goes with it.
        stock.MapDelete("{id:int}", async (int id, AppDbContext db) =>
        {
            var item = await db.StockItems.FindAsync(id);
            if (item is null) return Results.NotFound();
            db.StockItems.Remove(item);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { return Results.Conflict(new { error = "Stock item is used by a product or option" }); }
            return Results.NoContent();
        }).RequireAuthorization(Roles.Admin);

        stock.MapPost("{id:int}/corrections", async (int id, CorrectionRequest r, AppDbContext db, HttpContext http) =>
        {
            if (r.Delta == 0 || string.IsNullOrWhiteSpace(r.Reason))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["correction"] = ["Delta must not be 0 and a reason is required"] });

            await using var tx = await db.Database.BeginTransactionAsync();
            // Atomic and never below zero, so two admins correcting at once cannot corrupt the quantity.
            var changed = await db.StockItems.Where(s => s.Id == id && s.Quantity + r.Delta >= 0)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => x.Quantity + r.Delta));
            if (changed == 0)
                return await db.StockItems.AnyAsync(s => s.Id == id)
                    ? Results.ValidationProblem(new Dictionary<string, string[]> { ["delta"] = ["Quantity cannot go below 0"] })
                    : Results.NotFound();

            db.StockLogs.Add(new StockLog { StockItemId = id, Delta = r.Delta, Reason = r.Reason.Trim(), User = http.User.Identity!.Name!, At = DateTime.UtcNow });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            var item = await db.StockItems.AsNoTracking().FirstAsync(s => s.Id == id);
            return Results.Ok(new StockItemDto(item.Id, item.Name, item.Unit, item.Quantity));
        }).RequireAuthorization(Roles.Admin);

        stock.MapGet("{id:int}/log", async (int id, AppDbContext db) =>
            await db.StockLogs.AsNoTracking().Where(l => l.StockItemId == id).OrderByDescending(l => l.Id).ToListAsync())
            .RequireAuthorization(Roles.Admin);
    }

    private static IResult? Invalid(StockItemRequest r) =>
        string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Unit)
            ? Results.ValidationProblem(new Dictionary<string, string[]> { ["stock"] = ["Name and unit are required"] })
            : null;
}
