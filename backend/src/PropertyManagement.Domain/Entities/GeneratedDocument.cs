using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities;

public class GeneratedDocument : TenantEntity
{
    public Guid CaseId { get; set; }
    public Case Case { get; set; } = null!;

    public LtFormType? FormType { get; set; }
    public string FileName { get; set; } = null!;
    public string StoragePath { get; set; } = null!;
    public int Version { get; set; } = 1;
    public bool IsMergedPacket { get; set; }
    public bool IsCurrent { get; set; } = true;
    public string? Sha256 { get; set; }
    public long SizeBytes { get; set; }
    public string? GeneratedBy { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}
