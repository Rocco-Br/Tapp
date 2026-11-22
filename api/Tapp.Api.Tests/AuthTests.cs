using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Tapp.Api.Tests;

public class AuthTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;
    public AuthTests(TestFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAsync(string user, string pass)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { username = user, password = pass });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        return client;
    }

    [Fact]
    public async Task Me_without_login_is_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_401()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "nope" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Admin_creates_guest_who_can_log_in_but_not_create_users()
    {
        var admin = await LoginAsync("admin", "admin-pass");
        var created = await admin.PostAsJsonAsync("/api/users",
            new { username = "anna", password = "secret1", role = "guest", is18Plus = false });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var guest = await LoginAsync("anna", "secret1");
        var me = await guest.GetFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>("/api/auth/me");
        Assert.False(me!["is18Plus"].GetBoolean());

        var forbidden = await guest.PostAsJsonAsync("/api/users",
            new { username = "bob", password = "secret1", role = "admin", is18Plus = true });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Unknown_role_is_rejected()
    {
        var admin = await LoginAsync("admin", "admin-pass");
        var res = await admin.PostAsJsonAsync("/api/users",
            new { username = "carl", password = "secret1", role = "king", is18Plus = true });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
