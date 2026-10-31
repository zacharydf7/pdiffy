namespace PDiffy;

/// <summary>
/// A set of <see cref="DocxCheck"/> rules for <see cref="DocxDocument.Check(DocxCheckSet)"/>. <see cref="Latest"/>
/// grows as PDiffy adds rules, so an upgrade can fail a test that passed.
/// </summary>
public sealed class DocxCheckSet
{
    private DocxCheckSet(string name, IEnumerable<DocxCheck> checks)
    {
        Name = name;
        Checks = ValueList<DocxCheck>.Of(checks.Order());
    }

    /// <summary>Every rule in the installed version of PDiffy, as <c>Check()</c> with no arguments applies.</summary>
    public static DocxCheckSet Latest { get; } = new("latest", Enum.GetValues<DocxCheck>());

    /// <summary>The set's name, such as "latest".</summary>
    public string Name { get; }

    /// <summary>The rules in the set, in the order <see cref="DocxCheck"/> declares them.</summary>
    public IReadOnlyList<DocxCheck> Checks { get; }

    /// <summary>The rules in this set that belong to one of the categories, under the same name.</summary>
    public DocxCheckSet Only(DocxCheckCategory category, params DocxCheckCategory[] more) =>
        new(Name, Checks.Where(check => more.Prepend(category).Contains(DocxChecker.RuleOf(check).Category)));

    /// <summary>The rules in this set other than the ones given, under the same name.</summary>
    public DocxCheckSet Except(params DocxCheck[] checks) => new(Name, Checks.Except(checks));

    /// <summary>The name and how many rules the set has, such as "latest, 1 rule".</summary>
    public override string ToString() => $"{Name}, {Checks.Count} {(Checks.Count == 1 ? "rule" : "rules")}";
}
