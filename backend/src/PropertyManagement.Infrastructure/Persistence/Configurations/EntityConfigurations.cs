using PropertyManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public class LawFirmConfig : IEntityTypeConfiguration<LawFirm>
{
    public void Configure(EntityTypeBuilder<LawFirm> b)
    {
        b.ToTable("LawFirms");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.HasIndex(x => x.Name);
        b.HasOne(x => x.AttorneySetting).WithOne(x => x.LawFirm)
            .HasForeignKey<AttorneySetting>(x => x.LawFirmId);
    }
}

public class UserProfileConfig : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("UserProfiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.IdentityUserId).IsRequired().HasMaxLength(450);
        b.HasIndex(x => x.IdentityUserId).IsUnique();
        b.Property(x => x.Email).IsRequired().HasMaxLength(200);
        b.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        b.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.LawFirmId);
        b.HasOne(x => x.LawFirm).WithMany(x => x.Users).HasForeignKey(x => x.LawFirmId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Client).WithMany(x => x.Users).HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ClientConfig : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("Clients");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.HasIndex(x => new { x.LawFirmId, x.Name });
        b.HasOne(x => x.LawFirm).WithMany(x => x.Clients).HasForeignKey(x => x.LawFirmId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PmsIntegrationConfig : IEntityTypeConfiguration<PmsIntegration>
{
    public void Configure(EntityTypeBuilder<PmsIntegration> b)
    {
        b.ToTable("PmsIntegrations");
        b.HasKey(x => x.Id);
        b.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        b.Property(x => x.BaseUrl).HasMaxLength(500);
        b.Property(x => x.Username).HasMaxLength(200);
        b.Property(x => x.CompanyCode).HasMaxLength(100);
        b.Property(x => x.LocationId).HasMaxLength(100);
        b.Property(x => x.CredentialsCipher).HasMaxLength(4000);
        b.HasIndex(x => new { x.LawFirmId, x.ClientId });
        // All Pms* relationships use Restrict so SQL Server doesn't see multiple cascade paths
        // converging on Cases (which references PmsLease/PmsTenant/PmsProperty/PmsUnit). Deleting
        // a Client / Integration / Property therefore requires explicitly clearing dependents
        // first — that's also the safer behavior; we don't silently wipe years of synced data.
        b.HasOne(x => x.Client).WithMany(x => x.PmsIntegrations).HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PmsPropertyConfig : IEntityTypeConfiguration<PmsProperty>
{
    public void Configure(EntityTypeBuilder<PmsProperty> b)
    {
        b.ToTable("PmsProperties");
        b.HasKey(x => x.Id);
        b.Property(x => x.ExternalId).IsRequired().HasMaxLength(100);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.HasIndex(x => new { x.IntegrationId, x.ExternalId }).IsUnique();
        b.HasOne(x => x.Integration).WithMany(x => x.Properties).HasForeignKey(x => x.IntegrationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PmsUnitConfig : IEntityTypeConfiguration<PmsUnit>
{
    public void Configure(EntityTypeBuilder<PmsUnit> b)
    {
        b.ToTable("PmsUnits");
        b.HasKey(x => x.Id);
        b.Property(x => x.ExternalId).IsRequired().HasMaxLength(100);
        b.Property(x => x.UnitNumber).IsRequired().HasMaxLength(100);
        b.Property(x => x.MarketRent).HasPrecision(18, 2);
        b.Property(x => x.SquareFeet).HasPrecision(18, 2);
        b.HasIndex(x => new { x.PropertyId, x.ExternalId }).IsUnique();
        b.HasOne(x => x.Property).WithMany(x => x.Units).HasForeignKey(x => x.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PmsTenantConfig : IEntityTypeConfiguration<PmsTenant>
{
    public void Configure(EntityTypeBuilder<PmsTenant> b)
    {
        b.ToTable("PmsTenants");
        b.HasKey(x => x.Id);
        b.Property(x => x.ExternalId).IsRequired().HasMaxLength(100);
        b.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        b.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Ignore(x => x.FullName);
        b.HasIndex(x => new { x.IntegrationId, x.ExternalId }).IsUnique();
        b.HasOne(x => x.Integration).WithMany().HasForeignKey(x => x.IntegrationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PmsLeaseConfig : IEntityTypeConfiguration<PmsLease>
{
    public void Configure(EntityTypeBuilder<PmsLease> b)
    {
        b.ToTable("PmsLeases");
        b.HasKey(x => x.Id);
        b.Property(x => x.ExternalId).IsRequired().HasMaxLength(100);
        b.Property(x => x.MonthlyRent).HasPrecision(18, 2);
        b.Property(x => x.SecurityDeposit).HasPrecision(18, 2);
        b.Property(x => x.CurrentBalance).HasPrecision(18, 2);
        b.HasIndex(x => new { x.IntegrationId, x.ExternalId }).IsUnique();
        b.HasOne(x => x.Integration).WithMany().HasForeignKey(x => x.IntegrationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Unit).WithMany(x => x.Leases).HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Tenant).WithMany(x => x.Leases).HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PmsLedgerItemConfig : IEntityTypeConfiguration<PmsLedgerItem>
{
    public void Configure(EntityTypeBuilder<PmsLedgerItem> b)
    {
        b.ToTable("PmsLedgerItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.ExternalId).IsRequired().HasMaxLength(100);
        b.Property(x => x.Category).IsRequired().HasMaxLength(100);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Balance).HasPrecision(18, 2);
        b.HasIndex(x => new { x.LeaseId, x.PostedDate });
        b.HasIndex(x => new { x.LeaseId, x.ExternalId }).IsUnique();
        // Ledger is leaf data; cascading from lease is safe (no other path leads to it).
        b.HasOne(x => x.Lease).WithMany(x => x.LedgerItems).HasForeignKey(x => x.LeaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CaseStageConfig : IEntityTypeConfiguration<CaseStage>
{
    public void Configure(EntityTypeBuilder<CaseStage> b)
    {
        b.ToTable("CaseStages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class CaseStatusConfig : IEntityTypeConfiguration<CaseStatus>
{
    public void Configure(EntityTypeBuilder<CaseStatus> b)
    {
        b.ToTable("CaseStatuses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class CaseConfig : IEntityTypeConfiguration<Case>
{
    public void Configure(EntityTypeBuilder<Case> b)
    {
        b.ToTable("Cases");
        b.HasKey(x => x.Id);
        b.Property(x => x.CaseNumber).IsRequired().HasMaxLength(50);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.AmountInControversy).HasPrecision(18, 2);
        b.Property(x => x.PmsSnapshotJson).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.LawFirmId, x.CaseNumber }).IsUnique();
        b.HasIndex(x => new { x.LawFirmId, x.ClientId });

        b.HasOne(x => x.LawFirm).WithMany(x => x.Cases).HasForeignKey(x => x.LawFirmId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Client).WithMany(x => x.Cases).HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CaseStage).WithMany().HasForeignKey(x => x.CaseStageId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CaseStatus).WithMany().HasForeignKey(x => x.CaseStatusId)
            .OnDelete(DeleteBehavior.Restrict);
        // Attorney/Paralegal both reference UserProfile — SQL Server rejects two SetNull
        // FKs to the same target table, so we Restrict deletes. Application code must
        // re-assign or null these out manually before removing a user.
        b.HasOne(x => x.AssignedAttorney).WithMany().HasForeignKey(x => x.AssignedAttorneyId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AssignedParalegal).WithMany().HasForeignKey(x => x.AssignedParalegalId)
            .OnDelete(DeleteBehavior.Restrict);
        // PMS snapshot FKs are also Restrict for the same reason — multiple paths from
        // PmsIntegration → property/unit/lease/tenant/case would otherwise form cycles.
        b.HasOne(x => x.PmsLease).WithMany().HasForeignKey(x => x.PmsLeaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PmsTenant).WithMany().HasForeignKey(x => x.PmsTenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PmsProperty).WithMany().HasForeignKey(x => x.PmsPropertyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PmsUnit).WithMany().HasForeignKey(x => x.PmsUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LtCase).WithOne(x => x.Case).HasForeignKey<LtCase>(x => x.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CaseDocumentConfig : IEntityTypeConfiguration<CaseDocument>
{
    public void Configure(EntityTypeBuilder<CaseDocument> b)
    {
        b.ToTable("CaseDocuments");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).IsRequired().HasMaxLength(300);
        b.Property(x => x.ContentType).IsRequired().HasMaxLength(150);
        b.Property(x => x.StoragePath).IsRequired().HasMaxLength(1000);
        b.HasOne(x => x.Case).WithMany(x => x.Documents).HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CaseCommentConfig : IEntityTypeConfiguration<CaseComment>
{
    public void Configure(EntityTypeBuilder<CaseComment> b)
    {
        b.ToTable("CaseComments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Body).IsRequired().HasColumnType("nvarchar(max)");
        b.HasOne(x => x.Case).WithMany(x => x.Comments).HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CasePaymentConfig : IEntityTypeConfiguration<CasePayment>
{
    public void Configure(EntityTypeBuilder<CasePayment> b)
    {
        b.ToTable("CasePayments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Method).HasMaxLength(100);
        b.Property(x => x.Reference).HasMaxLength(200);
        b.HasOne(x => x.Case).WithMany(x => x.Payments).HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CaseActivityConfig : IEntityTypeConfiguration<CaseActivity>
{
    public void Configure(EntityTypeBuilder<CaseActivity> b)
    {
        b.ToTable("CaseActivities");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActivityType).IsRequired().HasMaxLength(100);
        b.Property(x => x.Summary).IsRequired().HasMaxLength(500);
        b.HasIndex(x => new { x.CaseId, x.OccurredAtUtc });
        b.HasOne(x => x.Case).WithMany(x => x.Activities).HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class LtCaseConfig : IEntityTypeConfiguration<LtCase>
{
    public void Configure(EntityTypeBuilder<LtCase> b)
    {
        b.ToTable("LtCases");
        b.HasKey(x => x.Id);
        b.Property(x => x.RentDue).HasPrecision(18, 2);
        b.Property(x => x.LateFees).HasPrecision(18, 2);
        b.Property(x => x.OtherCharges).HasPrecision(18, 2);
        b.Property(x => x.TotalDue).HasPrecision(18, 2);
        b.HasIndex(x => x.CaseId).IsUnique();
        b.HasOne(x => x.AttorneyReviewedBy).WithMany().HasForeignKey(x => x.AttorneyReviewedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class LtCaseFormDataConfig : IEntityTypeConfiguration<LtCaseFormData>
{
    public void Configure(EntityTypeBuilder<LtCaseFormData> b)
    {
        b.ToTable("LtCaseFormData");
        b.HasKey(x => x.Id);
        b.Property(x => x.DataJson).IsRequired().HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.LtCaseId, x.FormType }).IsUnique();
        b.HasOne(x => x.LtCase).WithMany(x => x.FormData).HasForeignKey(x => x.LtCaseId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class LtCaseDocumentConfig : IEntityTypeConfiguration<LtCaseDocument>
{
    public void Configure(EntityTypeBuilder<LtCaseDocument> b)
    {
        b.ToTable("LtCaseDocuments");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).IsRequired().HasMaxLength(300);
        b.Property(x => x.StoragePath).IsRequired().HasMaxLength(1000);
        b.HasIndex(x => new { x.LtCaseId, x.FormType, x.Version });
        b.HasOne(x => x.LtCase).WithMany(x => x.Documents).HasForeignKey(x => x.LtCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AttorneySettingConfig : IEntityTypeConfiguration<AttorneySetting>
{
    public void Configure(EntityTypeBuilder<AttorneySetting> b)
    {
        b.ToTable("AttorneySettings");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.LawFirmId).IsUnique();
    }
}

public class GeneratedDocumentConfig : IEntityTypeConfiguration<GeneratedDocument>
{
    public void Configure(EntityTypeBuilder<GeneratedDocument> b)
    {
        b.ToTable("GeneratedDocuments");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).IsRequired().HasMaxLength(300);
        b.Property(x => x.StoragePath).IsRequired().HasMaxLength(1000);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.HasIndex(x => new { x.CaseId, x.FormType, x.Version });
        b.HasOne(x => x.Case).WithMany(x => x.GeneratedDocuments).HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntityType).IsRequired().HasMaxLength(200);
        b.Property(x => x.EntityId).HasMaxLength(200);
        b.Property(x => x.Summary).HasMaxLength(1000);
        b.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.OldValueJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.NewValueJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.HasIndex(x => x.OccurredAtUtc);
        b.HasIndex(x => new { x.LawFirmId, x.OccurredAtUtc });
        b.HasIndex(x => x.Action);
    }
}

public class SyncLogConfig : IEntityTypeConfiguration<SyncLog>
{
    public void Configure(EntityTypeBuilder<SyncLog> b)
    {
        b.ToTable("SyncLogs");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.IntegrationId, x.StartedAtUtc });
        b.HasOne(x => x.Integration).WithMany(x => x.SyncLogs).HasForeignKey(x => x.IntegrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
