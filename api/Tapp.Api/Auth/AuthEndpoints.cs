using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Tapp.Api.Data;

namespace Tapp.Api.Auth;

public record LoginRequest(string Username, string Password);
public record CreateUserRequest(string Username, string Password, string Role, bool Is18Plus);

public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (LoginRequest r, SignInManager<AppUser> signIn) =>
        {
            // Persistent cookie so guests stay logged in for the whole party.
            var result = await signIn.PasswordSignInAsync(r.Username, r.Password, isPersistent: true, lockoutOnFailure: true);
            return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
        });

        app.MapPost("/api/auth/logout", async (SignInManager<AppUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, UserManager<AppUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            return Results.Ok(new { user.UserName, Roles = await users.GetRolesAsync(user), user.Is18Plus });
        }).RequireAuthorization();

        app.MapPost("/api/users", async (CreateUserRequest r, UserManager<AppUser> users) =>
        {
            if (!Roles.All.Contains(r.Role))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = [$"Must be one of: {string.Join(", ", Roles.All)}"] });

            var user = new AppUser { UserName = r.Username, Is18Plus = r.Is18Plus };
            var result = await users.CreateAsync(user, r.Password);
            if (!result.Succeeded)
                return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

            await users.AddToRoleAsync(user, r.Role);
            return Results.Created($"/api/users/{user.Id}", new { user.Id, user.UserName, r.Role, user.Is18Plus });
        }).RequireAuthorization(Roles.Admin);
    }
}
