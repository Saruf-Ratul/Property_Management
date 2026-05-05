using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities;

/// <summary>
/// A live PMS connection for a client. Credentials are stored as encrypted ciphertext.
/// </summary>
public class PmsIntegration : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public PmsProvider Provider { get; set; }
    public string DisplayName { get; set; } = null!;

    public string? BaseUrl { get; set; }
    public string? Username { get; set; }
    public string? CompanyCode { get; set; }
    public string? LocationId { get; set; }

    /// <summary>Encrypted (DataProtection-wrapped) credential blob.</summary>
    public string? CredentialsCipher { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncAtUtc { get; set; }
    public SyncStatus? LastSyncStatus { get; set; }
    public string? LastSyncMessage { get; set; }

    public int SyncIntervalMinutes { get; set; } = 1440;

    public ICollection<PmsProperty> Properties { get; set; } = new List<PmsProperty>();
    public ICollection<SyncLog> SyncLogs { get; set; } = new List<SyncLog>();
}
