using FluentAssertions;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.UnitTests.Services;

public class PdfFieldMappingServiceTests
{
    private readonly PdfFieldMappingService _m = new();

    [Fact]
    public void All_seven_NJ_forms_have_a_schema()
    {
        _m.AllSchemas.Should().HaveCount(7);
        _m.AllSchemas.Select(s => s.FormType).Should().BeEquivalentTo(new[]
        {
            LtFormType.VerifiedComplaint,
            LtFormType.Summons,
            LtFormType.CertificationByLandlord,
            LtFormType.CertificationByAttorney,
            LtFormType.CertificationOfLeaseAndRegistration,
            LtFormType.LandlordCaseInformationStatement,
            LtFormType.RequestForResidentialWarrantOfRemoval,
        });
    }

    [Fact]
    public void Phase_assignment_is_correct()
    {
        _m.GetSchema(LtFormType.VerifiedComplaint).Phase.Should().Be(LtFormPhase.Filing);
        _m.GetSchema(LtFormType.Summons).Phase.Should().Be(LtFormPhase.Filing);
        _m.GetSchema(LtFormType.RequestForResidentialWarrantOfRemoval).Phase.Should().Be(LtFormPhase.Warrant);
    }

    [Fact]
    public void Verified_Complaint_includes_attorney_plaintiff_premises_lease()
    {
        var bundle = SampleBundle();
        var fields = _m.BuildFields(LtFormType.VerifiedComplaint, bundle);

        fields["LandlordName"].Should().Be("Acme Property Management");
        fields["TenantName"].Should().Be("David Lee");
        fields["PremisesAddress"].Should().Be("555 River Rd");
        fields["MonthlyRent"].Should().Be("2100.00");
        fields["RentDue"].Should().Be("6300.00");
        fields["AttorneyName"].Should().Be("Jane Q. Counselor, Esq.");
    }

    [Fact]
    public void Overrides_take_precedence()
    {
        var bundle = SampleBundle();
        var fields = _m.BuildFields(LtFormType.Summons, bundle,
            overrides: new Dictionary<string, string?> { ["AppearByDate"] = "2026-12-31" });
        fields["AppearByDate"].Should().Be("2026-12-31");
    }

    [Fact]
    public void Warrant_form_writes_warrant_section()
    {
        var bundle = SampleBundle();
        bundle.Warrant.JudgmentDate = new DateTime(2026, 1, 15);
        bundle.Warrant.TenantStillInPossession = true;

        var fields = _m.BuildFields(LtFormType.RequestForResidentialWarrantOfRemoval, bundle);

        fields["JudgmentDate"].Should().Be("2026-01-15");
        fields["TenantStillInPossession"].Should().Be("Yes");
    }

    private static LtFormDataSections SampleBundle() => new()
    {
        Caption = { CourtVenue = "Essex County Special Civil Part", FilingDate = new DateTime(2026, 4, 27) },
        Attorney = { AttorneyName = "Jane Q. Counselor, Esq.", BarNumber = "NJ-DEMO-0001" },
        Plaintiff = { Name = "Acme Property Management" },
        Defendant = { FirstName = "David", LastName = "Lee" },
        Premises = { AddressLine1 = "555 River Rd", City = "Jersey City", State = "NJ", PostalCode = "07310", County = "Hudson" },
        Lease = { MonthlyRent = 2100m, IsMonthToMonth = true },
        RentOwed = { Total = 6300m },
    };
}
