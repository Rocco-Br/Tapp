using Microsoft.EntityFrameworkCore;
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

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => "ok");
app.MapAuth();

// SPA fallback for everything except /api, so unknown API paths stay 404.
app.MapFallbackToFile("{*path:regex(^(?!api(/|$)).*$)}", "index.html");
app.Run();

public partial class Program;
