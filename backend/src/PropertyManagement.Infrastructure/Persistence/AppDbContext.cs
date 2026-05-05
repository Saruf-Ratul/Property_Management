using System.Linq.Expressions;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PropertyManagement.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant, ICurrentUser currentUser)
        : base(options)
    {
        _tenant = tenant;
        _currentUser = currentUser;
    }

    public DbSet<LawFirm> LawFirms => Set<LawFirm>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Client> Clients => Set<Client>();

    public DbSet<PmsIntegration> PmsIntegrations => Set<PmsIntegration>();
    public DbSet<PmsProperty> PmsProperties => Set<PmsProperty>();
    public DbSet<PmsUnit> PmsUnits => Set<PmsUnit>();
    public DbSet<PmsTenant> PmsTenants => Set<PmsTenant>();
    public DbSet<PmsLease> PmsLeases => Set<PmsLease>();
    public DbSet<PmsLedgerItem> PmsLedgerItems => Set<PmsLedgerItem>();

    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseStage> CaseStages => Set<CaseStage>();
    public DbSet<CaseStatus> CaseStatuses => Set<CaseStatus>();
    public DbSet<CaseDocument> CaseDocuments => Set<CaseDocument>();
    public DbSet<CaseComment> CaseComments => Set<CaseComment>();
    public DbSet<CasePayment> CasePayments => Set<CasePayment>();
    public DbSet<CaseActivity> CaseActivities => Set<CaseActivity>();

    public DbSet<LtCase> LtCases => Set<LtCase>();
    public DbSet<LtCaseFormData> LtCaseFormData => Set<LtCaseFormData>();
    public DbSet<LtCaseDocument> LtCaseDocuments => Set<LtCaseDocument>();

    public DbSet<AttorneySetting> AttorneySettings => Set<AttorneySetting>();
    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Compose query filters: soft-delete + (optional) tenant scope.
        foreach (var et in b.Model.GetEntityTypes())
        {
            var clr = et.ClrType;
            if (typeof(TenantEntity).IsAssignableFrom(clr))
            {
                ApplyFilter(b, clr, isTenant: true);
            }
            else if (typeof(Entity).IsAssignableFrom(clr))
            {
                ApplyFilter(b, clr, isTenant: false);
            }
        }
    }

    private void ApplyFilter(ModelBuilder b, Type clrType, bool isTenant)
    {
        var method = typeof(AppDbContext)
            .GetMethod(isTenant ? nameof(SetTenantFilter) : nameof(SetSoftDeleteFilter),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(clrType);
        method.Invoke(this, new object[] { b });
    }

    private void SetTenantFilter<TEntity>(ModelBuilder b) where TEntity : TenantEntity
    {
        // Soft-deleted rows are hidden, AND rows belonging to a different tenant are hidden.
        Expression<Func<TEntity, bool>> filter = e =>
            !e.IsDeleted && (_tenant.BypassFilter || e.LawFirmId == _tenant.LawFirmId);
        b.Entity<TEntity>().HasQueryFilter(filter);
    }

    private void SetSoftDeleteFilter<TEntity>(ModelBuilder b) where TEntity : Entity
    {
        b.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditAndSoftDelete()
    {
        var now = DateTime.UtcNow;
        var actor = _currentUser.Email;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedBy ??= actor;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedBy = actor;
                    break;
                case EntityState.Deleted:
                    // Convert hard delete -> soft delete for any Entity-derived row.
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAtUtc = now;
                    entry.Entity.DeletedBy = actor;
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedBy = actor;
                    break;
            }
        }

        // Auto-stamp tenant id on newly added TenantEntity rows.
        if (!_tenant.BypassFilter && _tenant.LawFirmId.HasValue)
        {
            foreach (var entry in ChangeTracker.Entries<TenantEntity>())
            {
                if (entry.State == EntityState.Added && entry.Entity.LawFirmId == Guid.Empty)
                    entry.Entity.LawFirmId = _tenant.LawFirmId.Value;
            }
        }
    }
}
