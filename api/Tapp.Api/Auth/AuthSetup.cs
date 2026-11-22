using Microsoft.AspNetCore.Identity;
using Tapp.Api.Data;

namespace Tapp.Api.Auth;

public static class AuthSetup
{
    public static void AddAuth(this IServiceCollection services)
    {
        services.AddIdentity<AppUser, IdentityRole>(o =>
        {
            // Home parties: short guest passwords are fine, the admin hands them out.
            o.Password.RequiredLength = 6;
            o.Password.RequireDigit = false;
            o.Password.RequireLowercase = false;
            o.Password.RequireUppercase = false;
            o.Password.RequireNonAlphanumeric = false;
        }).AddEntityFrameworkStores<AppDbContext>();

        // An API must answer 401/403, not redirect to a login page.
        services.ConfigureApplicationCookie(o =>
        {
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Roles.Admin, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Roles.Staff, p => p.RequireRole(Roles.Admin, Roles.Employee));
    }

    // Creates the roles and, if Admin:Password is configured and no admin exists, the first admin.
    public static async Task SeedAsync(IServiceProvider sp, IConfiguration config)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        var users = sp.GetRequiredService<UserManager<AppUser>>();
        var password = config["Admin:Password"];
        if (string.IsNullOrEmpty(password) || (await users.GetUsersInRoleAsync(Roles.Admin)).Count > 0) return;

        var admin = new AppUser { UserName = config["Admin:Username"] ?? "admin", Is18Plus = true };
        var result = await users.CreateAsync(admin, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Could not seed admin: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(admin, Roles.Admin);
    }
}
