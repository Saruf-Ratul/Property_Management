using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities;

public class PmsLedgerItem : TenantEntity
{
    public Guid LeaseId { get; set; }
    public PmsLease Lease { get; set; } = null!;

    public string ExternalId { get; set; } = null!;
    public DateTime PostedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Category { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public bool IsCharge { get; set; }
    public bool IsPayment { get; set; }
}
