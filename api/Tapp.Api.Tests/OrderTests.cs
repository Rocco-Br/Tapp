using Tapp.Api.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tapp.Api.Catalog;
using Tapp.Api.Events;
using Tapp.Api.Orders;
using Tapp.Api.Stock;
using Xunit;

namespace Tapp.Api.Tests;

// A running event is global state, so each test gets its own factory and database.
public class OrderTests
{
    private record Ctx(HttpClient Admin, HttpClient Guest, string GuestId, ProductDto Cola, int ZeroStock, int NormalStock);

    private static async Task<HttpClient> LoginAsync(TestFactory f, string user, string pass)
    {
        var client = f.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { username = user, password = pass })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<int> StockAsync(HttpClient admin, string name, int quantity)
    {
        var item = (await (await admin.PostAsJsonAsync("/api/stock", new StockItemRequest(name, "bottle"))).Content.ReadFromJsonAsync<StockItemDto>())!;
        await admin.PostAsJsonAsync($"/api/stock/{item.Id}/corrections", new CorrectionRequest(quantity, "restock"));
        return item.Id;
    }

    private static async Task<Ctx> SetupAsync(TestFactory f, bool guest18 = false, bool product18 = false, bool startEvent = true, int stock = 10)
    {
        var admin = await LoginAsync(f, "admin", "admin-pass");
        var zero = await StockAsync(admin, "cola zero", stock);
        var normal = await StockAsync(admin, "cola normal", stock);
        var cola = (await (await admin.PostAsJsonAsync("/api/products", new ProductDto(null, "Cola", null, null, "drinks", 250, product18, false, [],
            [new(null, "Type", [new(null, "Zero", [new(zero, 1)]), new(null, "Normal", [new(normal, 1)])])]))).Content.ReadFromJsonAsync<ProductDto>())!;
        if (startEvent) await admin.PostAsJsonAsync("/api/events", new StartEventRequest("Party", true));

        var created = await admin.PostAsJsonAsync("/api/users", new { username = "anna", password = "secret1", role = "guest", is18Plus = guest18 });
        var guestId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        return new Ctx(admin, await LoginAsync(f, "anna", "secret1"), guestId, cola, zero, normal);
    }

    private static object Order(Ctx c, int quantity = 1, int optionIndex = 0, string? guestId = null) => new
    {
        guestId,
        lines = new[] { new { productId = c.Cola.Id, quantity, optionIds = new[] { c.Cola.OptionGroups[0].Options[optionIndex].Id } } }
    };

    private static async Task<int> QuantityAsync(Ctx c, int stockId) =>
        (await c.Admin.GetFromJsonAsync<List<StockItemDto>>("/api/stock"))!.Single(s => s.Id == stockId).Quantity;

    [Fact]
    public async Task Order_deducts_option_stock_and_snapshots_the_price()
    {
        using var f = new TestFactory();
        var c = await SetupAsync(f);

        var res = await c.Guest.PostAsJsonAsync("/api/orders", Order(c, quantity: 2, optionIndex: 0));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var order = (await res.Content.ReadFromJsonAsync<OrderDto>())!;

        Assert.Equal(500, order.TotalCents);
        Assert.Equal(250, order.Lines[0].UnitPriceCents);
        Assert.Equal("Type: Zero", order.Lines[0].Options);
        Assert.Equal(8, await QuantityAsync(c, c.ZeroStock));
        Assert.Equal(10, await QuantityAsync(c, c.NormalStock));
    }

    [Fact]
    public async Task Eighteen_plus_is_checked_on_the_guest_even_when_staff_place_the_order()
    {
        using var f = new TestFactory();
        var c = await SetupAsync(f, guest18: false, product18: true);

        Assert.Equal(HttpStatusCode.Forbidden, (await c.Guest.PostAsJsonAsync("/api/orders", Order(c))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.Admin.PostAsJsonAsync("/api/orders", Order(c, guestId: c.GuestId))).StatusCode);
        Assert.Equal(10, await QuantityAsync(c, c.ZeroStock));
    }

    [Fact]
    public async Task Adult_guest_can_order_eighteen_plus_and_staff_can_order_for_a_guest()
    {
        using var f = new TestFactory();
        var c = await SetupAsync(f, guest18: true, product18: true);

        Assert.Equal(HttpStatusCode.Created, (await c.Guest.PostAsJsonAsync("/api/orders", Order(c))).StatusCode);
        var byStaff = (await (await c.Admin.PostAsJsonAsync("/api/orders", Order(c, guestId: c.GuestId))).Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal(c.GuestId, byStaff.GuestId);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Admin.PostAsJsonAsync("/api/orders", Order(c))).StatusCode); // staff must name a guest
    }

    [Fact]
    public async Task Guest_cannot_order_for_someone_else()
    {
        using var f = new TestFactory();
        var c = await SetupAsync(f);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.Guest.PostAsJsonAsync("/api/orders", Order(c, guestId: "someone-else"))).StatusCode);
    }

    [Fact]
    public async Task Order_without_running_event_is_409()
    {
        using var f = new TestFactory();
        var c = await SetupAsync(f, startEvent: false);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Guest.PostAsJsonAsync("/api/orders", Order(c))).StatusCode);
    }

    [Fact]
    public async Task Invalid_options_products_and_cart_size_are_rejected()
    {
        using var f = new TestFactory();
        var c = await SetupAsync(f);

        Assert.Equal(HttpStatusCode.BadRequest, (await c.Guest.PostAsJsonAsync("/api/orders",
            new { lines = new[] { new { productId = c.Cola.Id, quantity = 1, optionIds = Array.Empty<int>() } } })).StatusCode); // option missing
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Guest.PostAsJsonAsync("/api/orders",
            new { lines = new[] { new { productId = c.Cola.Id, quantity = 1, optionIds = new[] { 99999 } } } })).StatusCode);       // unknown option
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Guest.PostAsJsonAsync("/api/orders",
            new { lines = new[] { new { productId = 99999, quantity = 1, optionIds = Array.Empty<int>() } } })).StatusCode);        // unknown product
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Guest.PostAsJsonAsync("/api/orders", Order(c, quantity: 11))).StatusCode); // cart too big
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Guest.PostAsJsonAsync("/api/orders", Order(c, quantity: 0))).StatusCode);
        Assert.Equal(10, await QuantityAsync(c, c.ZeroStock));
    }

    [Fact]
    public async Task Stock_stops_at_zero_and_the_order_still_goes_through()
    {
        using var f = new TestFactory();
        var c = await SetupAsync(f, stock: 2);
        Assert.Equal(HttpStatusCode.Created, (await c.Guest.PostAsJsonAsync("/api/orders", Order(c, quantity: 5))).StatusCode);
        Assert.Equal(0, await QuantityAsync(c, c.ZeroStock));
    }

    [Fact]
    public async Task Rapid_ordering_is_rate_limited_per_user()
    {
        using var f = new TestFactory();
        var c = await SetupAsync(f);
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 7; i++) codes.Add((await c.Guest.PostAsJsonAsync("/api/orders", Order(c))).StatusCode);

        Assert.Equal(5, codes.Count(x => x == HttpStatusCode.Created));
        Assert.Equal(2, codes.Count(x => x == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Guests_see_only_their_own_orders_and_only_staff_change_status()
    {
        using var f = new TestFactory();
        var c = await SetupAsync(f);
        var mine = (await (await c.Guest.PostAsJsonAsync("/api/orders", Order(c))).Content.ReadFromJsonAsync<OrderDto>())!;
        await c.Admin.PostAsJsonAsync("/api/users", new { username = "bert", password = "secret1", role = "guest", is18Plus = false });
        var other = await LoginAsync(f, "bert", "secret1");

        Assert.Empty((await other.GetFromJsonAsync<List<OrderDto>>("/api/orders"))!);
        Assert.Single((await c.Guest.GetFromJsonAsync<List<OrderDto>>("/api/orders"))!);

        var status = new OrderStatusRequest(OrderStatus.Served);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.Guest.PatchAsJsonAsync($"/api/orders/{mine.Id}/status", status)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Admin.PatchAsJsonAsync($"/api/orders/{mine.Id}/status", status)).StatusCode);
        Assert.Equal(OrderStatus.Served, (await c.Admin.GetFromJsonAsync<List<OrderDto>>("/api/orders"))!.Single().Status);
    }
}
