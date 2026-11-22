using Microsoft.EntityFrameworkCore;
using Tapp.Api.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

// shortcut: migrate on startup, fine for one SQLite file in one container, move to a deploy step if that changes
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

app.MapGet("/api/health", () => "ok");
app.Run();
