namespace PDiffy;

/// <summary>
/// A set of <see cref="PdfCheck"/> rules for <see cref="PdfUaDocument.Check(PdfCheckSet)"/>. <see cref="Latest"/> grows
/// as PDiffy adds rules, so an upgrade can fail a test that passed; a release's own set, such as
/// <see cref="Version0_3"/>, never changes, so upgrading adds no rules until a test moves to a newer set.
/// </summary>
public sealed class PdfCheckSet
{
    private PdfCheckSet(string name, IEnumerable<PdfCheck> checks)
    {
        Name = name;
        Checks = ValueList<PdfCheck>.Of(checks.Order());
    }

    /// <summary>Every rule in the installed version of PDiffy, as <c>Check()</c> with no arguments applies.</summary>
    public static PdfCheckSet Latest { get; } = new("latest", Enum.GetValues<PdfCheck>());

    /// <summary>The rules PDiffy 0.3 shipped. It never changes.</summary>
    public static PdfCheckSet Version0_3 { get; } = new("0.3",
    [
        PdfCheck.DocumentLanguage, PdfCheck.DocumentTitle, PdfCheck.FigureAltText, PdfCheck.UntaggedContent,
        PdfCheck.LinkDescriptions, PdfCheck.FormFieldNames, PdfCheck.TableHeaders, PdfCheck.HeadingLevels,
        PdfCheck.FontsEmbedded, PdfCheck.FontsMapToUnicode, PdfCheck.NoActions, PdfCheck.NoEmbeddedFiles,
    ]);

    /// <summary>The set's name: "latest", or the release it keeps the rules of, such as "0.3".</summary>
    public string Name { get; }

    /// <summary>The rules in the set, in the order <see cref="PdfCheck"/> declares them.</summary>
    public IReadOnlyList<PdfCheck> Checks { get; }

    /// <summary>
    /// The rules in this set that belong to one of the categories, such as tables, under the same name.
    /// </summary>
    public PdfCheckSet Only(PdfCheckCategory category, params PdfCheckCategory[] more) =>
        new(Name, Checks.Where(check => more.Prepend(category).Contains(PdfChecker.RuleOf(check).Category)));

    /// <summary>The rules in this set other than the ones given, under the same name.</summary>
    public PdfCheckSet Except(params PdfCheck[] checks) => new(Name, Checks.Except(checks));

    /// <summary>The name and how many rules the set has, such as "latest, 12 rules".</summary>
    public override string ToString() => $"{Name}, {Checks.Count} {(Checks.Count == 1 ? "rule" : "rules")}";
}
