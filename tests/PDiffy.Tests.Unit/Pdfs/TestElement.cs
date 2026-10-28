namespace PDiffy.Tests.Unit.Pdfs;

/// <summary>
/// A structure element for a <see cref="TestPdf"/>. Kids can be MCIDs (int), child elements, MCID references or links.
/// <see cref="Entries"/> holds raw dictionary entries such as "/Alt (Logo)". A null <see cref="Page"/> leaves out /Pg,
/// so the element's MCIDs use its parent's page.
/// </summary>
internal sealed record TestElement(string Type, params object[] Kids)
{
    public int? Page { get; init; } = 1;
    public string Entries { get; init; } = "";
}

/// <summary>A marked-content reference: an MCID on a page that can differ from the element's own.</summary>
internal sealed record TestMcr(int Mcid, int Page);

/// <summary>A link annotation on the element's page, added to the element as an object reference.</summary>
internal sealed record TestLink(string Uri, string Contents);
