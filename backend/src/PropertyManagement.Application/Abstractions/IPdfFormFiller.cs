using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions;

/// <summary>
/// Fills a PDF AcroForm template with a flat field-name → value dictionary, merges multiple PDFs into
/// a single filing packet, and supports a preview mode that bypasses any persistence side-effects.
/// </summary>
public interface IPdfFormFillerService
{
    /// <summary>Fill a single form template with a flat field-name → value map.</summary>
    Task<byte[]> FillAsync(LtFormType formType, IReadOnlyDictionary<string, string?> fields, CancellationToken ct = default);

    /// <summary>
    /// Render a preview without any persistence side-effects. Identical bytes to <see cref="FillAsync"/>;
    /// this overload exists so that callers can express intent at the type level.
    /// </summary>
    Task<byte[]> PreviewAsync(LtFormType formType, IReadOnlyDictionary<string, string?> fields, CancellationToken ct = default);

    /// <summary>Concatenate multiple PDFs into a single packet.</summary>
    Task<byte[]> MergeAsync(IEnumerable<byte[]> pdfs, CancellationToken ct = default);
}

/// <summary>Legacy alias retained for existing callers.</summary>
public interface IPdfFormFiller : IPdfFormFillerService { }

/// <summary>
/// Maps the structured <see cref="LtFormDataSections"/> bundle plus optional overrides to the flat
/// PDF field dictionary that the AcroForm template expects. Each form has its own mapper because
/// different NJ templates expose different field name sets.
/// </summary>
public interface IPdfFieldMappingService
{
    /// <summary>Get the metadata for a single form (display name, phase, sections used, public flag).</summary>
    LtFormSchemaDto GetSchema(LtFormType formType);

    /// <summary>Get all 7 form schemas.</summary>
    IReadOnlyList<LtFormSchemaDto> AllSchemas { get; }

    /// <summary>
    /// Build the AcroForm field dictionary for the given form by reading the applicable sections
    /// from the bundle and applying optional one-off overrides.
    /// </summary>
    IReadOnlyDictionary<string, string?> BuildFields(
        LtFormType formType,
        LtFormDataSections sections,
        IReadOnlyDictionary<string, string?>? overrides = null);
}

public interface IRedactionValidator
{
    /// <summary>Returns the list of detected sensitive values. Empty list = pass.</summary>
    IReadOnlyList<RedactionFinding> Validate(IReadOnlyDictionary<string, string?> fields);
}

public record RedactionFinding(string FieldName, string Pattern, string Sample);
