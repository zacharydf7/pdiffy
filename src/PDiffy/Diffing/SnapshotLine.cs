using System.Text;

namespace PDiffy.Diffing;

/// <summary>
/// One line of a snapshot, split into its parts: leading words (such as "TD p2"), key=value attributes and the quoted
/// text at the end, plus the lines nested under it.
/// </summary>
internal sealed class SnapshotLine(string text, int depth)
{
    // Worked out on first use and kept, since matching compares each line with many others.
    private HashSet<string>? _textWords;
    private HashSet<string>? _allWords;

    public string Text { get; } = text;
    public int Depth { get; } = depth;
    public List<SnapshotLine> Children { get; } = [];
    public List<string> Words { get; } = [];
    public List<(string Key, string Value)> Attributes { get; } = [];
    public string? QuotedText { get; private set; }

    /// <summary>
    /// What identifies the line among its siblings: the text a line starts with (a bookmark's title), else its first
    /// word, else its first key for a line of keys.
    /// </summary>
    public string Key => Text.TrimStart().StartsWith('"') ? QuotedText ?? ""
        : Words.Count > 0 ? Words[0] : Attributes.Count > 0 ? Attributes[0].Key : "";

    /// <summary>The page the line is on, from a word like p3 or p2-4.</summary>
    public int? Page => Words.Select(PageOf).FirstOrDefault(page => page is not null);

    /// <summary>The words of the quoted text, for telling an edited line from a different one.</summary>
    public HashSet<string> TextWords => _textWords ??= [.. (QuotedText ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>The leading words, the key=value attributes and the text's words.</summary>
    public HashSet<string> AllWords =>
        _allWords ??= [.. Words, .. Attributes.Select(pair => $"{pair.Key}={pair.Value}"), .. TextWords];

    /// <summary>The line and everything nested under it, used to recognize a subtree that didn't change or moved.</summary>
    public string Signature =>
        Children.Count == 0 ? Text : Text + "\n" + string.Join("\n", Children.Select(child => child.Signature));

    public static SnapshotLine Parse(string text, int depth)
    {
        var line = new SnapshotLine(text, depth);
        foreach (var token in Tokens(text.Trim()))
        {
            var equals = token.IndexOf('=');
            var isAttribute = equals > 0 && token[..equals].All(c => char.IsAsciiLetterLower(c) || c == '-');
            if (token.StartsWith('"')) line.QuotedText = Unquote(token);
            else if (isAttribute) line.Attributes.Add((token[..equals], token[(equals + 1)..]));
            else line.Words.Add(token);
        }

        return line;
    }

    // Splits on spaces outside quotes; a quoted value keeps its quotes and escapes.
    private static IEnumerable<string> Tokens(string text)
    {
        var token = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == ' ' && !quoted)
            {
                if (token.Length > 0) yield return token.ToString();
                token.Clear();
                continue;
            }

            token.Append(c);
            if (c == '\\' && quoted && i + 1 < text.Length) token.Append(text[++i]);
            else if (c == '"') quoted = !quoted;
        }

        if (token.Length > 0) yield return token.ToString();
    }

    // Drops the surrounding quotes and undoes the writer's \" and \\ escapes.
    private static string Unquote(string token)
    {
        var text = new StringBuilder();
        for (var i = 1; i < token.Length - 1; i++)
        {
            if (token[i] == '\\' && i + 1 < token.Length - 1 && token[i + 1] is '"' or '\\') i++;
            text.Append(token[i]);
        }

        return text.ToString();
    }

    private static int? PageOf(string word)
    {
        if (word.Length < 2 || word[0] != 'p' || !char.IsAsciiDigit(word[1])) return null;
        var digits = new string(word.Skip(1).TakeWhile(char.IsAsciiDigit).ToArray());
        return int.Parse(digits);
    }
}
