namespace Tapp.Api.Auth;

public static class Roles
{
    public const string Admin = "admin";
    public const string Employee = "employee";
    public const string Guest = "guest";

    // Policy name, not a role: admin or employee.
    public const string Staff = "staff";

    public static readonly string[] All = [Admin, Employee, Guest];
}
