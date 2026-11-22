using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Tapp.Api.Catalog;
using Tapp.Api.Auth;
using Tapp.Api.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddAuth();

var app = builder.Build();

// shortcut: migrate on startup, fine for one SQLite file in one container, move to a deploy step if that changes
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    await AuthSetup.SeedAsync(scope.ServiceProvider, app.Configuration);
}

app.UseDefaultFiles();
app.UseStaticFiles();

var pictures = CatalogEndpoints.PicturesDir(app.Configuration);
Directory.CreateDirectory(pictures);
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(pictures), RequestPath = "/pictures" });

// Static files must run before routing: the SPA fallback is an endpoint and static files skip requests that have one.
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => "ok");
app.MapAuth();
app.MapCatalog();

// SPA fallback for everything except /api and /pictures, so unknown API paths and pictures stay 404.
app.MapFallbackToFile("{*path:regex(^(?!(api|pictures)(/|$)).*$)}", "index.html");
app.Run();

public partial class Program;
