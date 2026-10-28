using System.Text.RegularExpressions;

namespace PDiffy;

/// <summary>
/// What to change in a text or JSON snapshot before it's compared, for values that differ on every run. Each scrub
/// applies to the whole text.
/// </summary>
public sealed class TextSnapshotOptions
{
    private readonly List<Scrubber> _scrubbers = [];

    /// <summary>Replaces matches anywhere in the text. The replacement can refer to groups, such as "Order $1".</summary>
    public TextSnapshotOptions ScrubText(Regex pattern, string replacement)
    {
        _scrubbers.Add(new Scrubber(pattern, replacement, Numbered: false));
        return this;
    }

    /// <summary>
    /// Replaces dates with Date_1, Date_2 and so on. The same text always gets the same token. Recognizes the same dates
    /// as <see cref="SnapshotOptions.ScrubDates"/>.
    /// </summary>
    public TextSnapshotOptions ScrubDates()
    {
        _scrubbers.Add(Scrubber.Dates);
        return this;
    }

    /// <summary>Replaces GUIDs with Guid_1, Guid_2 and so on. The same text always gets the same token.</summary>
    public TextSnapshotOptions ScrubGuids()
    {
        _scrubbers.Add(Scrubber.Guids);
        return this;
    }

    /// <summary>Leaves dates as they are. JSON snapshots scrub them unless told to keep them.</summary>
    public TextSnapshotOptions KeepDates()
    {
        _scrubbers.RemoveAll(scrubber => scrubber == Scrubber.Dates);
        return this;
    }

    /// <summary>Leaves GUIDs as they are. JSON snapshots scrub them unless told to keep them.</summary>
    public TextSnapshotOptions KeepGuids()
    {
        _scrubbers.RemoveAll(scrubber => scrubber == Scrubber.Guids);
        return this;
    }

    internal IReadOnlyList<Scrubber> Scrubbers => _scrubbers;
}
