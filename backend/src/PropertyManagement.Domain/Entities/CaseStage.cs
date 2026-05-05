using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities;

public class CaseStage : Entity
{
    public CaseStageCode Code { get; set; }
    public string Name { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsTerminal { get; set; }
    public string? Description { get; set; }
}

public class CaseStatus : Entity
{
    public CaseStatusCode Code { get; set; }
    public string Name { get; set; } = null!;
    public bool IsTerminal { get; set; }
    public string? Description { get; set; }
}
