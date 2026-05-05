using System.Text.RegularExpressions;
using PropertyManagement.Application.Abstractions;

namespace PropertyManagement.Infrastructure.Services;

public partial class RedactionValidator : IRedactionValidator
{
    private static readonly (string Name, Regex Pattern)[] Patterns =
    {
        ("SSN",            new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)),
        ("SSN-Plain",      new Regex(@"\b\d{9}\b", RegexOptions.Compiled)),
        ("CreditCard",     new Regex(@"\b(?:\d[ -]*?){13,16}\b", RegexOptions.Compiled)),
        ("DriversLicense", new Regex(@"\b[A-Z]{1,2}\d{6,12}\b", RegexOptions.Compiled)),
        ("BankAccount",    new Regex(@"\b\d{8,17}\b", RegexOptions.Compiled)),
        // Military-status keywords — NJ rules forbid these on public LT forms because they could
        // run afoul of the SCRA. We treat any case-insensitive match in a non-allowed field as a finding.
        ("MilitaryStatus", new Regex(
            @"\b(active duty|servicemember|service[\s-]?member|reservist|deployed|National Guard|Air Force|Army|Navy|Marine Corps|Marines|Coast Guard|veteran|military)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase))
    };

    private static readonly HashSet<string> AllowedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Phone", "PhoneNumber", "ZipCode", "PostalCode", "PropertyId",
        "CaseNumber", "DocketNumber", "FirmName", "AttorneyName", "CertifierName", "CertifierTitle"
    };

    public IReadOnlyList<RedactionFinding> Validate(IReadOnlyDictionary<string, string?> fields)
    {
        var findings = new List<RedactionFinding>();
        foreach (var (key, raw) in fields)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (AllowedFields.Contains(key)) continue;
            foreach (var (name, pattern) in Patterns)
            {
                var m = pattern.Match(raw);
                if (m.Success)
                {
                    var sample = m.Value.Length > 4 ? m.Value[..2] + new string('*', m.Value.Length - 4) + m.Value[^2..] : "****";
                    findings.Add(new RedactionFinding(key, name, sample));
                    break;
                }
            }
        }
        return findings;
    }
}
