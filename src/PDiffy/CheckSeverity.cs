namespace PDiffy;

/// <summary>How much a <see cref="PdfCheckFinding"/> or a <see cref="DocxCheckFinding"/> matters.</summary>
public enum CheckSeverity
{
    /// <summary>
    /// The document breaks a rule its standard requires: a "shall" of PDF/UA for a PDF, or a WCAG 2.2 success criterion
    /// for a Word document.
    /// </summary>
    Error,

    /// <summary>
    /// The document breaks a practice another standard, such as WCAG, asks for that PDF/UA doesn't check, a practice
    /// that only usually breaks a success criterion, or a house rule that isn't from a standard.
    /// </summary>
    Warning,
}
