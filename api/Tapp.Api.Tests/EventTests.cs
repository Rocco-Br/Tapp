using System.Net;
using System.Net.Http.Json;
using Tapp.Api.Data;
using Tapp.Api.Events;
using Tapp.Api.Stock;
using Xunit;

namespace Tapp.Api.Tests;

// Events are global state (one running at a time), so each test gets its own factory and database.
public class EventTests
{
    private static async Task<HttpClient> AdminAsync(TestFactory f)
    {
        var client = f.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "admin-pass" })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<int> StockWithQuantityAsync(HttpClient admin, int quantity)
    {
        var item = (await (await admin.PostAsJsonAsync("/api/stock", new StockItemRequest("Beer", "bottle"))).Content.ReadFromJsonAsync<StockItemDto>())!;
        await admin.PostAsJsonAsync($"/api/stock/{item.Id}/corrections", new CorrectionRequest(quantity, "restock"));
        return item.Id;
    }

    [Fact]
    public async Task Only_one_event_runs_and_stop_frees_the_slot()
    {
        using var f = new TestFactory();
        var admin = await AdminAsync(f);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/events/current")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/events", new StartEventRequest("Party", true))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/events", new StartEventRequest("Other", true))).StatusCode);

        var stopped = (await (await admin.PostAsync("/api/events/current/stop", null)).Content.ReadFromJsonAsync<Event>())!;
        Assert.NotNull(stopped.EndedAt);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/events", new StartEventRequest("Next", true))).StatusCode);
    }

    [Fact]
    public async Task Carry_over_keeps_stock_and_start_empty_zeroes_it_with_a_log_entry()
    {
        using var f = new TestFactory();
        var admin = await AdminAsync(f);
        var id = await StockWithQuantityAsync(admin, 12);

        await admin.PostAsJsonAsync("/api/events", new StartEventRequest("One", true));
        Assert.Equal(12, (await admin.GetFromJsonAsync<List<StockItemDto>>("/api/stock"))!.Single().Quantity);
        await admin.PostAsync("/api/events/current/stop", null);

        await admin.PostAsJsonAsync("/api/events", new StartEventRequest("Two", false));
        Assert.Equal(0, (await admin.GetFromJsonAsync<List<StockItemDto>>("/api/stock"))!.Single().Quantity);
        var log = (await admin.GetFromJsonAsync<List<StockLog>>($"/api/stock/{id}/log"))!;
        Assert.Equal(-12, log[0].Delta);
    }

    [Fact]
    public async Task Guest_can_see_current_event_but_not_start_one()
    {
        using var f = new TestFactory();
        var admin = await AdminAsync(f);
        await admin.PostAsJsonAsync("/api/users", new { username = "anna", password = "secret1", role = "guest", is18Plus = false });
        await admin.PostAsJsonAsync("/api/events", new StartEventRequest("Party", true));

        var guest = f.CreateClient();
        (await guest.PostAsJsonAsync("/api/auth/login", new { username = "anna", password = "secret1" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/events/current")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PostAsJsonAsync("/api/events", new StartEventRequest("Mine", true))).StatusCode);
    }
}
