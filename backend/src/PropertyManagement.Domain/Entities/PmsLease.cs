using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities;

public class PmsLease : TenantEntity
{
    public Guid IntegrationId { get; set; }
    public PmsIntegration Integration { get; set; } = null!;

    public Guid UnitId { get; set; }
    public PmsUnit Unit { get; set; } = null!;

    public Guid TenantId { get; set; }
    public PmsTenant Tenant { get; set; } = null!;

    public string ExternalId { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal? SecurityDeposit { get; set; }
    public bool IsMonthToMonth { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal CurrentBalance { get; set; }

    public ICollection<PmsLedgerItem> LedgerItems { get; set; } = new List<PmsLedgerItem>();
}
