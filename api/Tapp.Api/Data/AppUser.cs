using Microsoft.AspNetCore.Identity;

namespace Tapp.Api.Data;

public class AppUser : IdentityUser
{
    public bool Is18Plus { get; set; }
}
