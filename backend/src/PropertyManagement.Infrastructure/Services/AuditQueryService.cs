using System.Globalization;
using System.Text;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PropertyManagement.Infrastructure.Services;

public class AuditQueryService : IAuditQueryService
{
    private const int CsvMaxRows = 50_000;

    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    public AuditQueryService(AppDbContext db, ICurrentUser user) { _db = db; _user = user; }

    private IQueryable<Domain.Entities.AuditLog> ScopedQuery()
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        // Audit logs are scoped to the caller's law firm. FirmAdmin / Auditor only see their firm's logs.
        if (_user.LawFirmId.HasValue)
            q = q.Where(x => x.LawFirmId == _user.LawFirmId);
        return q;
    }

    public async Task<PagedResult<AuditLogDto>> ListAsync(PageRequest req, AuditAction? action, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var q = ApplyFilters(ScopedQuery(), req.Search, action, from, to);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.OccurredAtUtc).Skip(req.Skip).Take(req.Take)
            .Select(x => new AuditLogDto(x.Id, x.OccurredAtUtc, x.Action, x.EntityType, x.EntityId, x.Summary, x.UserEmail, x.IpAddress))
            .ToListAsync(ct);
        return new PagedResult<AuditLogDto> { Items = items, Page = req.Page, PageSize = req.Take, TotalCount = total };
    }

    public async Task<AuditLogDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return await ScopedQuery().Where(x => x.Id == id)
            .Select(x => new AuditLogDetailDto(
                x.Id, x.OccurredAtUtc, x.Action, x.EntityType, x.EntityId, x.Summary,
                x.UserEmail, x.UserId, x.LawFirmId,
                x.IpAddress, x.UserAgent,
                x.PayloadJson, x.OldValueJson, x.NewValueJson))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(byte[] Bytes, string FileName)> ExportCsvAsync(string? search, AuditAction? action, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var q = ApplyFilters(ScopedQuery(), search, action, from, to);
        var rows = await q.OrderByDescending(x => x.OccurredAtUtc).Take(CsvMaxRows)
            .Select(x => new
            {
                x.Id, x.OccurredAtUtc, x.Action, x.EntityType, x.EntityId,
                x.Summary, x.UserEmail, x.UserId, x.LawFirmId,
                x.IpAddress, x.UserAgent, x.PayloadJson, x.OldValueJson, x.NewValueJson
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Id,OccurredAtUtc,Action,EntityType,EntityId,Summary,UserEmail,UserId,LawFirmId,IpAddress,UserAgent,Payload,OldValue,NewValue");
        foreach (var r in rows)
        {
            sb.Append(Csv(r.Id.ToString())).Append(',');
            sb.Append(Csv(r.OccurredAtUtc.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Csv(r.Action.ToString())).Append(',');
            sb.Append(Csv(r.EntityType)).Append(',');
            sb.Append(Csv(r.EntityId)).Append(',');
            sb.Append(Csv(r.Summary)).Append(',');
            sb.Append(Csv(r.UserEmail)).Append(',');
            sb.Append(Csv(r.UserId?.ToString())).Append(',');
            sb.Append(Csv(r.LawFirmId?.ToString())).Append(',');
            sb.Append(Csv(r.IpAddress)).Append(',');
            sb.Append(Csv(r.UserAgent)).Append(',');
            sb.Append(Csv(r.PayloadJson)).Append(',');
            sb.Append(Csv(r.OldValueJson)).Append(',');
            sb.Append(Csv(r.NewValueJson));
            sb.AppendLine();
        }
        var fileName = $"audit-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        // Prepend a UTF-8 BOM so Excel opens it cleanly with non-ASCII content.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return (bytes, fileName);
    }

    private static IQueryable<Domain.Entities.AuditLog> ApplyFilters(
        IQueryable<Domain.Entities.AuditLog> q, string? search, AuditAction? action, DateTime? from, DateTime? to)
    {
        if (action.HasValue) q = q.Where(x => x.Action == action.Value);
        if (from.HasValue) q = q.Where(x => x.OccurredAtUtc >= from.Value);
        if (to.HasValue) q = q.Where(x => x.OccurredAtUtc <= to.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x =>
                EF.Functions.Like(x.Summary!, $"%{s}%") ||
                EF.Functions.Like(x.UserEmail!, $"%{s}%") ||
                EF.Functions.Like(x.EntityType, $"%{s}%") ||
                EF.Functions.Like(x.EntityId!, $"%{s}%"));
        }
        return q;
    }

    /// <summary>RFC 4180 minimal CSV escaping — wraps any field containing comma/quote/CR/LF in quotes.</summary>
    private static string Csv(string? s)
    {
        if (s is null) return string.Empty;
        var needsQuotes = s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
        var escaped = s.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }
}
