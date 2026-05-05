using FluentAssertions;
using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.UnitTests.Services;

/// <summary>
/// The redaction validator is what stops sensitive identifiers from making it onto a
/// public NJ LT court form. These tests are non-negotiable — they protect tenants from
/// having SSNs / DLs / military status published in public court filings.
/// </summary>
public class RedactionValidatorTests
{
    private readonly RedactionValidator _r = new();

    [Theory]
    [InlineData("SSN-Plain", "DefendantName", "Tenant has SSN 123456789")]
    [InlineData("SSN",       "DefendantName", "Tenant SSN: 123-45-6789")]
    [InlineData("CreditCard","Reference",     "Card 4242 4242 4242 4242")]
    [InlineData("DriversLicense", "DefendantName", "DL AB1234567")]
    public void Detects_sensitive_identifiers(string expectedPattern, string field, string value)
    {
        var findings = _r.Validate(new Dictionary<string, string?> { [field] = value });
        findings.Should().NotBeEmpty();
        findings.Should().Contain(f => f.Pattern == expectedPattern && f.FieldName == field);
    }

    [Theory]
    [InlineData("active duty Army")]
    [InlineData("servicemember spouse")]
    [InlineData("veteran")]
    [InlineData("National Guard")]
    public void Detects_military_status(string sample)
    {
        var findings = _r.Validate(new Dictionary<string, string?>
        {
            ["AdditionalOccupants"] = sample
        });
        findings.Should().Contain(f => f.Pattern == "MilitaryStatus");
    }

    [Fact]
    public void Allowed_fields_are_skipped()
    {
        // Phone numbers can look like 9-digit blocks; we explicitly whitelist Phone fields.
        var findings = _r.Validate(new Dictionary<string, string?>
        {
            ["Phone"]      = "973 555 0102",
            ["ZipCode"]    = "07310",
            ["DocketNumber"] = "LT-001234",
        });
        findings.Should().BeEmpty();
    }

    [Fact]
    public void Mask_preserves_first_and_last_two_chars()
    {
        var findings = _r.Validate(new Dictionary<string, string?>
        {
            ["DefendantName"] = "Jane Doe SSN 123456789"
        });
        findings.Should().HaveCount(1);
        findings[0].Sample.Should().StartWith("12").And.EndWith("89");
        findings[0].Sample.Should().NotContain("345"); // middle digits masked
    }

    [Fact]
    public void Empty_input_returns_empty()
    {
        var findings = _r.Validate(new Dictionary<string, string?>());
        findings.Should().BeEmpty();
    }
}
