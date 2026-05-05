using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.DTOs;

/// <summary>Dashboard payload for the client portal — KPIs, upcoming court dates, recent activity.</summary>
public record ClientPortalDashboardDto(
    Guid ClientId,
    string ClientName,
    int TotalCases,
    int ActiveCases,
    int ClosedCases,
    int CasesInFiling,
    int CasesInTrialOrJudgment,
    int CasesAwaitingWarrant,
    decimal TotalAmountInControversy,
    int DocumentsAvailableCount,
    int UnreadNotificationCount,
    DateTime? NextCourtDateUtc,
    IReadOnlyList<UpcomingCourtDateDto> UpcomingCourtDates,
    IReadOnlyList<ClientPortalNotificationDto> RecentActivity);

public record UpcomingCourtDateDto(
    Guid CaseId,
    string CaseNumber,
    string CaseTitle,
    DateTime CourtDateUtc,
    string? CourtVenue,
    string? CourtDocketNumber);

/// <summary>A client-facing notification (significant events from across the client's cases).</summary>
public record ClientPortalNotificationDto(
    Guid Id,
    Guid CaseId,
    string CaseNumber,
    string CaseTitle,
    DateTime OccurredAtUtc,
    string ActivityType,
    string Summary,
    string Severity,
    bool IsHighlighted);

public record ClientPortalCommentRequest(string Body);
