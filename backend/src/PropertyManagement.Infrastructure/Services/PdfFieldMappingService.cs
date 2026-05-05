using System.Globalization;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Services;

/// <summary>
/// One mapper per NJ LT form. Each mapper is a simple function that pulls the relevant
/// fields out of the structured bundle and emits the flat dictionary the PDF AcroForm wants.
///
/// Real NJ template field names will vary (e.g. "Court", "DocketNumber", "PltfName" etc).
/// The keys we use here are the *logical* names that our internal renderer (PdfFormFiller)
/// labels on the page; when a real AcroForm template is dropped in, swap in the template's
/// actual field name strings on the right-hand side of each mapper without touching anything else.
/// </summary>
public class PdfFieldMappingService : IPdfFieldMappingService
{
    public IReadOnlyList<LtFormSchemaDto> AllSchemas { get; } = new[]
    {
        new LtFormSchemaDto(
            LtFormType.VerifiedComplaint,
            "Verified Complaint — Residential Landlord/Tenant",
            LtFormPhase.Filing,
            IsPublicCourtForm: true,
            RelevantSections: new[] {
                "Caption", "Attorney", "Plaintiff", "Defendant", "Premises", "Lease",
                "RentOwed", "AdditionalRent", "FilingFee", "Subsidy", "Notices",
                "Registration", "Certification"
            }),
        new LtFormSchemaDto(
            LtFormType.Summons, "Summons — Landlord/Tenant",
            LtFormPhase.Filing, IsPublicCourtForm: true,
            RelevantSections: new[] { "Caption", "Attorney", "Plaintiff", "Defendant", "Premises" }),
        new LtFormSchemaDto(
            LtFormType.CertificationByLandlord, "Certification by Landlord",
            LtFormPhase.Filing, IsPublicCourtForm: false,
            RelevantSections: new[] { "Caption", "Attorney", "Plaintiff", "Premises", "Lease", "RentOwed", "Notices", "Registration", "Certification" }),
        new LtFormSchemaDto(
            LtFormType.CertificationByAttorney, "Certification by Landlord's Attorney",
            LtFormPhase.Filing, IsPublicCourtForm: false,
            RelevantSections: new[] { "Caption", "Attorney", "Plaintiff", "Defendant", "Certification" }),
        new LtFormSchemaDto(
            LtFormType.CertificationOfLeaseAndRegistration, "Certification of Lease and Registration",
            LtFormPhase.Filing, IsPublicCourtForm: false,
            RelevantSections: new[] { "Caption", "Attorney", "Plaintiff", "Premises", "Lease", "Registration", "Certification" }),
        new LtFormSchemaDto(
            LtFormType.LandlordCaseInformationStatement, "Landlord Case Information Statement (LCIS)",
            LtFormPhase.Filing, IsPublicCourtForm: true,
            RelevantSections: new[] { "Caption", "Attorney", "Plaintiff", "Defendant", "Premises", "Lease", "RentOwed", "AdditionalRent", "Subsidy", "Notices", "Registration" }),
        new LtFormSchemaDto(
            LtFormType.RequestForResidentialWarrantOfRemoval, "Request for Residential Warrant of Removal",
            LtFormPhase.Warrant, IsPublicCourtForm: true,
            RelevantSections: new[] { "Caption", "Attorney", "Plaintiff", "Defendant", "Premises", "Warrant", "Certification" }),
    };

    public LtFormSchemaDto GetSchema(LtFormType formType) =>
        AllSchemas.First(s => s.FormType == formType);

    public IReadOnlyDictionary<string, string?> BuildFields(
        LtFormType formType,
        LtFormDataSections s,
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var d = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // Captions / attorney / plaintiff / defendant / premises are common to most forms.
        WriteCaption(d, s);
        WriteAttorney(d, s);
        WritePlaintiff(d, s);
        WriteDefendant(d, s);
        WritePremises(d, s);

        switch (formType)
        {
            case LtFormType.VerifiedComplaint:
                WriteLease(d, s); WriteRentOwed(d, s); WriteAdditionalRent(d, s);
                WriteFilingFee(d, s); WriteSubsidy(d, s); WriteNotices(d, s);
                WriteRegistration(d, s); WriteCertification(d, s);
                break;
            case LtFormType.Summons:
                d["AppearByDate"] = (DateTime.UtcNow.AddDays(20)).ToString("yyyy-MM-dd");
                break;
            case LtFormType.CertificationByLandlord:
                WriteLease(d, s); WriteRentOwed(d, s); WriteNotices(d, s);
                WriteRegistration(d, s); WriteCertification(d, s);
                break;
            case LtFormType.CertificationByAttorney:
                WriteCertification(d, s);
                d["AttorneyCertificationDate"] = ToDate(s.Certification.CertificationDate);
                break;
            case LtFormType.CertificationOfLeaseAndRegistration:
                WriteLease(d, s); WriteRegistration(d, s); WriteCertification(d, s);
                break;
            case LtFormType.LandlordCaseInformationStatement:
                WriteLease(d, s); WriteRentOwed(d, s); WriteAdditionalRent(d, s);
                WriteSubsidy(d, s); WriteNotices(d, s); WriteRegistration(d, s);
                break;
            case LtFormType.RequestForResidentialWarrantOfRemoval:
                WriteWarrant(d, s); WriteCertification(d, s);
                break;
        }

        // Apply one-off overrides last.
        if (overrides is not null)
        {
            foreach (var (k, v) in overrides) d[k] = v;
        }

        return d;
    }

    // ─── Section writers ────────────────────────────────────────────────────
    private static void WriteCaption(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["CourtName"]    = s.Caption.CourtName ?? "Superior Court of New Jersey";
        d["CourtVenue"]   = s.Caption.CourtVenue;
        d["County"]       = s.Caption.CountyName ?? s.Premises.County;
        d["DocketNumber"] = s.Caption.DocketNumber;
        d["CaseNumber"]   = s.Caption.CaseNumber;
        d["FilingDate"]   = ToDate(s.Caption.FilingDate ?? DateTime.UtcNow);
    }
    private static void WriteAttorney(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["FirmName"]            = s.Attorney.FirmName;
        d["AttorneyName"]        = s.Attorney.AttorneyName;
        d["AttorneyBarNumber"]   = s.Attorney.BarNumber;
        d["AttorneyEmail"]       = s.Attorney.Email;
        d["AttorneyPhone"]       = s.Attorney.Phone;
        d["AttorneyAddressLine1"]= s.Attorney.OfficeAddressLine1;
        d["AttorneyAddressLine2"]= s.Attorney.OfficeAddressLine2;
        d["AttorneyCity"]        = s.Attorney.OfficeCity;
        d["AttorneyState"]       = s.Attorney.OfficeState;
        d["AttorneyZipCode"]     = s.Attorney.OfficePostalCode;
    }
    private static void WritePlaintiff(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["LandlordName"]   = s.Plaintiff.Name;
        d["PlaintiffName"]  = s.Plaintiff.Name;
        d["LandlordAddress"]= JoinAddress(s.Plaintiff.AddressLine1, s.Plaintiff.AddressLine2,
                                          s.Plaintiff.City, s.Plaintiff.State, s.Plaintiff.PostalCode);
        d["LandlordPhone"]  = s.Plaintiff.Phone;
        d["LandlordIsCorporate"] = s.Plaintiff.IsCorporate ? "Yes" : "No";
    }
    private static void WriteDefendant(IDictionary<string, string?> d, LtFormDataSections s)
    {
        var name = string.Join(' ', new[] { s.Defendant.FirstName, s.Defendant.LastName }
                                    .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        d["TenantName"]        = name;
        d["DefendantName"]     = name;
        d["TenantPhone"]       = s.Defendant.Phone;
        d["TenantEmail"]       = s.Defendant.Email;
        d["AdditionalOccupants"] = s.Defendant.AdditionalOccupants;
    }
    private static void WritePremises(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["PremisesAddress"]  = s.Premises.AddressLine1;
        d["PremisesAddress2"] = s.Premises.AddressLine2;
        d["PremisesCity"]     = s.Premises.City;
        d["PremisesCounty"]   = s.Premises.County;
        d["PremisesState"]    = s.Premises.State ?? "NJ";
        d["PremisesZipCode"]  = s.Premises.PostalCode;
        d["PremisesUnitNumber"] = s.Premises.UnitNumber;
    }
    private static void WriteLease(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["LeaseStartDate"]    = ToDate(s.Lease.StartDate);
        d["LeaseEndDate"]      = ToDate(s.Lease.EndDate);
        d["IsMonthToMonth"]    = s.Lease.IsMonthToMonth ? "Yes" : "No";
        d["LeaseIsWritten"]    = s.Lease.IsWritten ? "Yes" : "No";
        d["MonthlyRent"]       = ToMoney(s.Lease.MonthlyRent);
        d["SecurityDeposit"]   = ToMoney(s.Lease.SecurityDeposit);
        d["RentDueDay"]        = s.Lease.RentDueDay?.Day.ToString();
    }
    private static void WriteRentOwed(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["RentDueAsOf"]      = ToDate(s.RentOwed.AsOfDate);
        d["RentPriorBalance"] = ToMoney(s.RentOwed.PriorBalance);
        d["RentCurrentMonth"] = ToMoney(s.RentOwed.CurrentMonthRent);
        d["RentDue"]          = ToMoney(s.RentOwed.Total ?? ((s.RentOwed.PriorBalance ?? 0) + (s.RentOwed.CurrentMonthRent ?? 0)));
    }
    private static void WriteAdditionalRent(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["LateFees"]              = ToMoney(s.AdditionalRent.LateFees);
        d["AttorneyFees"]          = ToMoney(s.AdditionalRent.AttorneyFees);
        d["OtherCharges"]          = ToMoney(s.AdditionalRent.OtherCharges);
        d["OtherChargesDescription"] = s.AdditionalRent.OtherChargesDescription;
        var grand = (s.AdditionalRent.LateFees ?? 0)
                  + (s.AdditionalRent.AttorneyFees ?? 0)
                  + (s.AdditionalRent.OtherCharges ?? 0)
                  + (s.RentOwed.Total ?? 0);
        d["TotalDue"] = ToMoney(grand);
    }
    private static void WriteFilingFee(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["AmountClaimed"]     = ToMoney(s.FilingFee.AmountClaimed);
        d["FilingFee"]         = ToMoney(s.FilingFee.FilingFee);
        d["ApplyForFeeWaiver"] = s.FilingFee.ApplyForFeeWaiver ? "Yes" : "No";
    }
    private static void WriteSubsidy(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["IsRentControlled"] = s.Subsidy.IsRentControlled ? "Yes" : "No";
        d["IsSubsidized"]     = s.Subsidy.IsSubsidized ? "Yes" : "No";
        d["SubsidyProgram"]   = s.Subsidy.SubsidyProgram;
    }
    private static void WriteNotices(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["NoticeToCeaseServed"] = s.Notices.NoticeToCeaseServed ? "Yes" : "No";
        d["NoticeToCeaseDate"]   = ToDate(s.Notices.NoticeToCeaseDate);
        d["NoticeToQuitServed"]  = s.Notices.NoticeToQuitServed ? "Yes" : "No";
        d["NoticeToQuitDate"]    = ToDate(s.Notices.NoticeToQuitDate);
        d["ServiceMethod"]       = s.Notices.ServiceMethod;
    }
    private static void WriteRegistration(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["IsRegisteredMultipleDwelling"] = s.Registration.IsRegisteredMultipleDwelling ? "Yes" : "No";
        d["RegistrationNumber"]           = s.Registration.RegistrationNumber;
        d["RegistrationDate"]             = ToDate(s.Registration.RegistrationDate);
        d["IsOwnerOccupied"]              = s.Registration.IsOwnerOccupied ? "Yes" : "No";
        d["UnitCountInBuilding"]          = s.Registration.UnitCountInBuilding?.ToString();
    }
    private static void WriteCertification(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["CertifierName"]      = s.Certification.CertifierName ?? s.Plaintiff.Name;
        d["CertifierTitle"]     = s.Certification.CertifierTitle;
        d["CertificationDate"]  = ToDate(s.Certification.CertificationDate ?? DateTime.UtcNow);
        d["AttorneyReviewed"]   = s.Certification.AttorneyReviewed ? "Yes" : "No";
    }
    private static void WriteWarrant(IDictionary<string, string?> d, LtFormDataSections s)
    {
        d["JudgmentDate"]                = ToDate(s.Warrant.JudgmentDate);
        d["JudgmentDocketNumber"]        = s.Warrant.JudgmentDocketNumber;
        d["RequestedExecutionDate"]      = ToDate(s.Warrant.RequestedExecutionDate);
        d["TenantStillInPossession"]     = s.Warrant.TenantStillInPossession ? "Yes" : "No";
        d["PaymentReceivedSinceJudgment"]= s.Warrant.PaymentReceivedSinceJudgment ? "Yes" : "No";
        d["AmountPaidSinceJudgment"]     = ToMoney(s.Warrant.AmountPaidSinceJudgment);
    }

    // ─── helpers ────────────────────────────────────────────────────────────
    private static string? ToDate(DateTime? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string? ToMoney(decimal? d) => d?.ToString("0.00", CultureInfo.InvariantCulture);
    private static string? JoinAddress(params string?[] parts) =>
        parts.Any(p => !string.IsNullOrWhiteSpace(p))
            ? string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)))
            : null;
}
