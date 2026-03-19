namespace AIReviewer.Domain.Entities;

/// <summary>
/// Classifies the scope of an auto-fix suggestion.
/// </summary>
public enum FixType
{
    /// <summary>1–5 lines, localized single-statement fix.</summary>
    SmallSnippet,

    /// <summary>5–20 lines, small method-level refactor.</summary>
    MultiLineSnippet,

    /// <summary>20+ lines, architectural change — requires justification.</summary>
    FullFileRefactor
}
