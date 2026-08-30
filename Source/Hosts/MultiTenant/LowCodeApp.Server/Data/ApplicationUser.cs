using Microsoft.AspNetCore.Identity;

namespace LowCodeApp.Server.Data
{
    public class ApplicationUser : IdentityUser
    {
        public string? Name { get; set; }
        public string? TenantKey { get; set; }
    }
}
