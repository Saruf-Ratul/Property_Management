using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities;

/// <summary>
/// Application-side profile that mirrors the ASP.NET Identity user.
/// IdentityUserId is the FK to AspNetUsers.Id.
/// LawFirmId is the tenant scope. ClientId is non-null for ClientAdmin / ClientUser.
/// </summary>
public class UserProfile : TenantEntity
{
    public string IdentityUserId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Title { get; set; }
    public string? BarNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    public LawFirm LawFirm { get; set; } = null!;

    public string FullName => $"{FirstName} {LastName}".Trim();
}
