using System.Net;
using System.Net.Http.Json;
using Tapp.Api.Data;
using Tapp.Api.Stock;
using Xunit;

namespace Tapp.Api.Tests;

public class StockTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;
    public StockTests(TestFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAsync(string user, string pass)
    {
        var client = _factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { username = user, password = pass })).EnsureSuccessStatusCode();
        return client;
    }

    private async Task<HttpClient> EmployeeAsync()
    {
        var admin = await LoginAsync("admin", "admin-pass");
        var name = "e" + Guid.NewGuid().ToString("N")[..8];
        (await admin.PostAsJsonAsync("/api/users", new { username = name, password = "secret1", role = "employee", is18Plus = true })).EnsureSuccessStatusCode();
        return await LoginAsync(name, "secret1");
    }

    private static async Task<StockItemDto> CreateAsync(HttpClient admin) =>
        (await (await admin.PostAsJsonAsync("/api/stock", new StockItemRequest("Beer", "bottle"))).Content.ReadFromJsonAsync<StockItemDto>())!;

    [Fact]
    public async Task Corrections_change_quantity_and_are_logged()
    {
        var admin = await LoginAsync("admin", "admin-pass");
        var item = await CreateAsync(admin);

        await admin.PostAsJsonAsync($"/api/stock/{item.Id}/corrections", new CorrectionRequest(10, "restock"));
        var after = (await (await admin.PostAsJsonAsync($"/api/stock/{item.Id}/corrections", new CorrectionRequest(-3, "breakage"))).Content.ReadFromJsonAsync<StockItemDto>())!;
        Assert.Equal(7, after.Quantity);

        var log = (await admin.GetFromJsonAsync<List<StockLog>>($"/api/stock/{item.Id}/log"))!;
        Assert.Equal([-3, 10], log.Select(l => l.Delta));
        Assert.Equal("admin", log[0].User);
    }

    [Fact]
    public async Task Correction_below_zero_or_without_reason_is_rejected_and_changes_nothing()
    {
        var admin = await LoginAsync("admin", "admin-pass");
        var item = await CreateAsync(admin);
        await admin.PostAsJsonAsync($"/api/stock/{item.Id}/corrections", new CorrectionRequest(2, "restock"));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/stock/{item.Id}/corrections", new CorrectionRequest(-3, "waste"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/stock/{item.Id}/corrections", new CorrectionRequest(1, " "))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/stock/99999/corrections", new CorrectionRequest(1, "x"))).StatusCode);

        var list = (await admin.GetFromJsonAsync<List<StockItemDto>>("/api/stock"))!;
        Assert.Equal(2, list.Single(s => s.Id == item.Id).Quantity);
    }

    [Fact]
    public async Task Employee_can_view_but_not_change_stock()
    {
        var admin = await LoginAsync("admin", "admin-pass");
        var item = await CreateAsync(admin);
        var employee = await EmployeeAsync();

        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/stock")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/stock", new StockItemRequest("Wine", "bottle"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync($"/api/stock/{item.Id}/corrections", new CorrectionRequest(5, "x"))).StatusCode);
    }
}
