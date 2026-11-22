using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Tapp.Api.Catalog;
using Tapp.Api.Data;
using Xunit;

namespace Tapp.Api.Tests;

public class CatalogTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;
    public CatalogTests(TestFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAsync(string user, string pass)
    {
        var client = _factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { username = user, password = pass })).EnsureSuccessStatusCode();
        return client;
    }

    private async Task<HttpClient> GuestAsync()
    {
        var admin = await LoginAsync("admin", "admin-pass");
        var name = "g" + Guid.NewGuid().ToString("N")[..8];
        (await admin.PostAsJsonAsync("/api/users", new { username = name, password = "secret1", role = "guest", is18Plus = false })).EnsureSuccessStatusCode();
        return await LoginAsync(name, "secret1");
    }

    private int AddStockItem(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = new StockItem { Name = name, Unit = "bottle", Quantity = 10 };
        db.StockItems.Add(item);
        db.SaveChanges();
        return item.Id;
    }

    private static ProductDto Cola(int zeroBottle, int normalBottle, bool hidden = false) => new(null, "Cola", null, null, "drinks", 250, false, hidden, [],
        [new(null, "Type", [new(null, "Zero", [new(zeroBottle, 1)]), new(null, "Normal", [new(normalBottle, 1)])])]);

    [Fact]
    public async Task Admin_creates_product_with_option_stock_and_reads_it_back()
    {
        var zero = AddStockItem("cola zero"); var normal = AddStockItem("cola normal");
        var admin = await LoginAsync("admin", "admin-pass");

        var res = await admin.PostAsJsonAsync("/api/products", Cola(zero, normal));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = (await res.Content.ReadFromJsonAsync<ProductDto>())!;

        var list = (await admin.GetFromJsonAsync<List<ProductDto>>("/api/products"))!;
        var back = list.Single(p => p.Id == created.Id);
        Assert.Equal(2, back.OptionGroups[0].Options.Count);
        Assert.Equal(zero, back.OptionGroups[0].Options[0].Stock[0].StockItemId);
    }

    [Fact]
    public async Task Guest_cannot_create_and_does_not_see_hidden_products()
    {
        var s = AddStockItem("x");
        var admin = await LoginAsync("admin", "admin-pass");
        var hidden = (await (await admin.PostAsJsonAsync("/api/products", Cola(s, s, hidden: true))).Content.ReadFromJsonAsync<ProductDto>())!;

        var guest = await GuestAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PostAsJsonAsync("/api/products", Cola(s, s))).StatusCode);
        var seen = (await guest.GetFromJsonAsync<List<ProductDto>>("/api/products"))!;
        Assert.DoesNotContain(seen, p => p.Id == hidden.Id);
    }

    [Fact]
    public async Task Unknown_stock_item_is_rejected()
    {
        var admin = await LoginAsync("admin", "admin-pass");
        var res = await admin.PostAsJsonAsync("/api/products", Cola(99999, 99999));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Update_keeps_option_ids_and_drops_removed_options()
    {
        var s = AddStockItem("y");
        var admin = await LoginAsync("admin", "admin-pass");
        var created = (await (await admin.PostAsJsonAsync("/api/products", Cola(s, s))).Content.ReadFromJsonAsync<ProductDto>())!;
        var group = created.OptionGroups[0];
        var keep = group.Options[0];

        var edit = created with { Name = "Cola 2", OptionGroups = [group with { Options = [keep with { Name = "Zero sugar" }] }] };
        var updated = (await (await admin.PutAsJsonAsync($"/api/products/{created.Id}", edit)).Content.ReadFromJsonAsync<ProductDto>())!;

        Assert.Equal(group.Id, updated.OptionGroups[0].Id);
        Assert.Single(updated.OptionGroups[0].Options);
        Assert.Equal(keep.Id, updated.OptionGroups[0].Options[0].Id);
        Assert.Equal("Zero sugar", updated.OptionGroups[0].Options[0].Name);
    }

    [Fact]
    public async Task Picture_upload_accepts_png_and_rejects_other_types()
    {
        var s = AddStockItem("z");
        var admin = await LoginAsync("admin", "admin-pass");
        var p = (await (await admin.PostAsJsonAsync("/api/products", Cola(s, s))).Content.ReadFromJsonAsync<ProductDto>())!;

        MultipartFormDataContent Form(string type)
        {
            var file = new ByteArrayContent([1, 2, 3]);
            file.Headers.ContentType = new(type);
            return new() { { file, "file", "x.bin" } };
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/products/{p.Id}/picture", Form("text/html"))).StatusCode);
        var ok = await admin.PostAsync($"/api/products/{p.Id}/picture", Form("image/png"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var path = (await ok.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["picturePath"];
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(path)).StatusCode);
    }
}
