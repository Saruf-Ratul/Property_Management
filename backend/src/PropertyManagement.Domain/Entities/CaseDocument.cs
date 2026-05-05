using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities;

public class CaseDocument : TenantEntity
{
    public Guid CaseId { get; set; }
    public Case Case { get; set; } = null!;

    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = null!;
    public DocumentType DocumentType { get; set; } = DocumentType.Other;
    public string? Description { get; set; }
    public bool IsClientVisible { get; set; } = true;
}

public class CaseComment : TenantEntity
{
    public Guid CaseId { get; set; }
    public Case Case { get; set; } = null!;

    public Guid AuthorUserId { get; set; }
    public UserProfile Author { get; set; } = null!;

    public string Body { get; set; } = null!;

    /// <summary>Internal comments are hidden from ClientUser/ClientAdmin.</summary>
    public bool IsInternal { get; set; }
}

public class CasePayment : TenantEntity
{
    public Guid CaseId { get; set; }
    public Case Case { get; set; } = null!;

    public DateTime ReceivedOnUtc { get; set; }
    public decimal Amount { get; set; }
    public string? Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public class CaseActivity : TenantEntity
{
    public Guid CaseId { get; set; }
    public Case Case { get; set; } = null!;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string ActivityType { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public string? Details { get; set; }

    public Guid? ActorUserId { get; set; }
    public UserProfile? Actor { get; set; }
}
