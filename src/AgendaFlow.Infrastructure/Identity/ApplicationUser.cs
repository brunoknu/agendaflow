// Infrastructure layer - requires NuGet packages
// Install: Microsoft.AspNetCore.Identity.EntityFrameworkCore

using Microsoft.AspNetCore.Identity;

namespace AgendaFlow.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
