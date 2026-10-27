#nullable enable

namespace Nullscent.Skin
{
    public enum SkinDiagnosticKind
    {
        MalformedProperty,
        UnsupportedProperty,
        IgnoredByDesign,
        AdaptedProperty,
        VersionInapplicable,
        MissingAsset,
        IntentionalBlank,
        FallbackUsed,
        MalformedHighResolutionPair,
        AssetLoadFailure,
        InvalidConfiguration
    }

    public enum SkinDiagnosticSeverity
    {
        Information,
        Warning,
        Error
    }

    /// <summary>A parse, catalog, resolution, or activation diagnostic tied to its source.</summary>
    public sealed record SkinDiagnostic(
        SkinDiagnosticKind Kind,
        SkinDiagnosticSeverity Severity,
        string Message,
        string? Section = null,
        string? Property = null,
        int? Line = null,
        string? Asset = null);
}
