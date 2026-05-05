using FluentAssertions;
using FluentValidation.TestHelper;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Application.Validation;

namespace PropertyManagement.UnitTests.Validation;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _v = new();

    [Theory]
    [InlineData("",                "Admin!2345")]   // empty email
    [InlineData("not-an-email",    "Admin!2345")]   // bad email format
    [InlineData("ok@example.com",  "")]             // empty password
    [InlineData("ok@example.com",  "abc")]          // password too short
    public void Rejects_invalid_inputs(string email, string password)
    {
        var result = _v.TestValidate(new LoginRequest(email, password));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Accepts_valid_login()
    {
        var result = _v.TestValidate(new LoginRequest("admin@pm.local", "Admin!2345"));
        result.IsValid.Should().BeTrue();
    }
}

public class CreateCaseRequestValidatorTests
{
    private readonly CreateCaseRequestValidator _v = new();

    [Fact]
    public void Title_is_required()
    {
        var result = _v.TestValidate(new CreateCaseRequest(
            Title: "",
            CaseType: Domain.Enums.CaseType.LandlordTenantEviction,
            ClientId: Guid.NewGuid(),
            AssignedAttorneyId: null, AssignedParalegalId: null,
            PmsLeaseId: null, PmsTenantId: null,
            AmountInControversy: 1000m, Description: null));
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void ClientId_must_not_be_empty_guid()
    {
        var result = _v.TestValidate(new CreateCaseRequest(
            Title: "Test", CaseType: Domain.Enums.CaseType.LandlordTenantEviction,
            ClientId: Guid.Empty,
            AssignedAttorneyId: null, AssignedParalegalId: null,
            PmsLeaseId: null, PmsTenantId: null,
            AmountInControversy: null, Description: null));
        result.ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    [Fact]
    public void Title_over_300_chars_is_rejected()
    {
        var result = _v.TestValidate(new CreateCaseRequest(
            Title: new string('a', 301), CaseType: Domain.Enums.CaseType.LandlordTenantEviction,
            ClientId: Guid.NewGuid(),
            AssignedAttorneyId: null, AssignedParalegalId: null,
            PmsLeaseId: null, PmsTenantId: null,
            AmountInControversy: null, Description: null));
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }
}

public class PmsSyncRequestValidatorTests
{
    private readonly PmsSyncRequestValidator _v = new();

    [Fact]
    public void Rejects_empty_scope()
    {
        var result = _v.TestValidate(new PmsSyncRequest(
            FullSync: false,
            SyncProperties: false, SyncUnits: false, SyncTenants: false,
            SyncLeases: false, SyncLedgerItems: false));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Accepts_full_sync()
    {
        var result = _v.TestValidate(new PmsSyncRequest(FullSync: true));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Accepts_partial_scope()
    {
        var result = _v.TestValidate(new PmsSyncRequest(FullSync: false, SyncTenants: true));
        result.IsValid.Should().BeTrue();
    }
}
