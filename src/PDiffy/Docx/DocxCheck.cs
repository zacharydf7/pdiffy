namespace PDiffy;

/// <summary>
/// A rule <see cref="DocxDocument.Check(DocxCheck[])"/> can apply: the mistakes generated Word documents make that keep
/// them from being accessible. These are fast regression checks, not a full accessibility review.
/// </summary>
public enum DocxCheck
{
    /// <summary>
    /// Every image, chart, diagram, shape and embedded object has alt text or is marked as decoration. A shape with
    /// text in it and an ActiveX control are left out, since a screen reader reads the text and the control's own
    /// label.
    /// </summary>
    ImageAltText,
}

/// <summary>The family a <see cref="DocxCheck"/> belongs to, so findings can be grouped by family.</summary>
public enum DocxCheckCategory
{
    /// <summary>Alt text for images and other graphics.</summary>
    AltText,
}

/// <summary>
/// Something a <see cref="DocxCheck"/> found: where it is and what's wrong. The path is the one a snapshot diff shows
/// for the item's line, such as "body > Table > Row[2] > Cell > P > image".
/// </summary>
public sealed record DocxCheckFinding
{
    internal DocxCheckFinding()
    {
    }

    /// <summary>The rule that found it.</summary>
    public required DocxCheck Check { get; init; }

    /// <summary>The family the rule belongs to, such as alt text.</summary>
    public required DocxCheckCategory Category { get; init; }

    /// <summary>
    /// How much it matters: an error for a rule that breaks a WCAG 2.2 success criterion, a warning otherwise.
    /// </summary>
    public required CheckSeverity Severity { get; init; }

    /// <summary>
    /// The WCAG 2.2 success criterion the rule comes from, such as "WCAG 2.2 1.1.1". It says where the rule comes from;
    /// it doesn't say the document conforms to WCAG.
    /// </summary>
    public required string Reference { get; init; }

    /// <summary>
    /// Where it is, as a snapshot diff shows the item's line, such as "headers > s1 > P > image".
    /// </summary>
    public required string Path { get; init; }

    /// <summary>What's wrong, as a sentence with no closing period, such as "Image has no alt text".</summary>
    public required string Message { get; init; }

    /// <summary>
    /// The rule's entry on the docs site: what it finds, why it matters to someone using assistive technology, and how
    /// to fix it.
    /// </summary>
    public string HelpUrl => HelpUrlOf(Check);

    internal static string HelpUrlOf(DocxCheck check) =>
        $"https://zacharydf7.github.io/pdiffy/reference/checks.html#word-{check.ToString().ToLowerInvariant()}";

    /// <summary>Such as "ImageAltText: Image has no alt text (body > P[3] > image)".</summary>
    public override string ToString() => $"{Check}: {Message} ({Path})";
}
