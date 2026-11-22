using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tapp.Api.Tests;

// One temp database and picture folder per test class, admin seeded as admin / admin-pass.
public class TestFactory : WebApplicationFactory<Program>
{
    private readonly string _dir = Directory.CreateTempSubdirectory("tapp-test-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={Path.Combine(_dir, "test.db")}");
        builder.UseSetting("Pictures:Path", Path.Combine(_dir, "pictures"));
        builder.UseSetting("Admin:Password", "admin-pass");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(_dir, true); } catch (IOException) { /* sqlite may still hold the file */ }
    }
}
