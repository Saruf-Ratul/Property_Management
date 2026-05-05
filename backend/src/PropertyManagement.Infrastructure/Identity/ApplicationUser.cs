using Microsoft.AspNetCore.Identity;

namespace PropertyManagement.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public Guid LawFirmId { get; set; }
    public Guid? ClientId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ApplicationRole : IdentityRole { }
