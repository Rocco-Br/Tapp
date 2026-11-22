using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tapp.Api.Auth;
using Tapp.Api.Data;

namespace Tapp.Api.Orders;

public record OrderLineRequest(int ProductId, int Quantity, List<int>? OptionIds);
public record PlaceOrderRequest(string? GuestId, List<OrderLineRequest> Lines);
public record OrderLineDto(int ProductId, string ProductName, string? Options, int UnitPriceCents, int Quantity);
public record OrderDto(int Id, int EventId, string GuestId, OrderStatus Status, DateTime CreatedAt, int TotalCents, List<OrderLineDto> Lines);

public static class OrderEndpoints
{
    // shortcut: fixed limits, move to config if the venue wants to tune them
    private const int MaxItemsPerOrder = 10;
    private const int OrdersPerWindow = 5;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    public static void AddOrderRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            // Per user, so one spammer does not block the rest of the party.
            o.AddPolicy("orders", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.User.Identity?.Name ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = OrdersPerWindow, Window = Window }));
        });

    public static void MapOrders(this WebApplication app)
    {
        app.MapPost("/api/orders", PlaceAsync).RequireAuthorization().RequireRateLimiting("orders");
    }

    private static IResult Problem(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    private static async Task<IResult> PlaceAsync(PlaceOrderRequest r, ClaimsPrincipal principal, UserManager<AppUser> users, AppDbContext db)
    {
        var caller = (await users.GetUserAsync(principal))!;
        var isStaff = principal.IsInRole(Roles.Admin) || principal.IsInRole(Roles.Employee);

        AppUser? guest;
        if (isStaff)
        {
            if (string.IsNullOrEmpty(r.GuestId)) return Problem("guestId", "Required when staff place an order");
            guest = await users.FindByIdAsync(r.GuestId);
            if (guest is null) return Problem("guestId", "Unknown guest");
        }
        else
        {
            if (r.GuestId is not null && r.GuestId != caller.Id) return Results.StatusCode(403);
            guest = caller;
        }

        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.IsRunning);
        if (ev is null) return Results.Conflict(new { error = "No event is running" });

        if (r.Lines is not { Count: > 0 } || r.Lines.Any(l => l.Quantity < 1)) return Problem("lines", "Add at least one item, each with quantity 1 or more");
        if (r.Lines.Sum(l => l.Quantity) > MaxItemsPerOrder) return Problem("lines", $"At most {MaxItemsPerOrder} items per order");

        var ids = r.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking()
            .Include(p => p.Stock).Include(p => p.OptionGroups).ThenInclude(g => g.Options).ThenInclude(o => o.Stock)
            .Where(p => ids.Contains(p.Id) && !p.IsHidden).ToDictionaryAsync(p => p.Id);
        if (products.Count != ids.Count) return Problem("lines", "Unknown or unavailable product");

        // 18+ is checked on the guest the order is for, never on who placed it.
        if (!guest.Is18Plus && products.Values.Any(p => p.Is18Plus))
            return Results.Json(new { error = "This order contains 18+ products" }, statusCode: 403);

        var order = new Order { EventId = ev.Id, GuestId = guest.Id, PlacedById = caller.Id, Status = OrderStatus.New, CreatedAt = DateTime.UtcNow };
        var deductions = new Dictionary<int, int>();

        foreach (var l in r.Lines)
        {
            var p = products[l.ProductId];
            var optionIds = l.OptionIds ?? [];
            // Exactly one known option per group, nothing extra.
            if (optionIds.Count != p.OptionGroups.Count || p.OptionGroups.Any(g => g.Options.Count(o => optionIds.Contains(o.Id)) != 1))
                return Problem("lines", $"Choose exactly one option per group for {p.Name}");

            var chosen = p.OptionGroups.Select(g => (g, o: g.Options.Single(o => optionIds.Contains(o.Id)))).ToList();
            foreach (var s in p.Stock.Concat(chosen.SelectMany(c => c.o.Stock)))
                deductions[s.StockItemId] = deductions.GetValueOrDefault(s.StockItemId) + s.Amount * l.Quantity;

            order.Lines.Add(new OrderLine
            {
                ProductId = p.Id, ProductName = p.Name, UnitPriceCents = p.PriceCents, Quantity = l.Quantity,
                Options = chosen.Count == 0 ? null : string.Join(", ", chosen.Select(c => $"{c.g.Name}: {c.o.Name}"))
            });
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        // Rough tracking: an order never fails on stock, quantities just stop at 0 (the counts may be off at a party).
        foreach (var (stockId, amount) in deductions)
            await db.StockItems.Where(s => s.Id == stockId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => Math.Max(0, x.Quantity - amount)));
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Results.Created($"/api/orders/{order.Id}", new OrderDto(order.Id, order.EventId, order.GuestId, order.Status, order.CreatedAt,
            order.Lines.Sum(l => l.UnitPriceCents * l.Quantity),
            order.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.Options, l.UnitPriceCents, l.Quantity)).ToList()));
    }
}
