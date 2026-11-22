namespace Tapp.Api.Auth;

public static class Roles
{
    public const string Admin = "admin";
    public const string Employee = "employee";
    public const string Guest = "guest";

    public static readonly string[] All = [Admin, Employee, Guest];
}
