# Text and JSON

[`Snapshot.VerifyTextAsync`](xref:PDiffy.Snapshot.VerifyTextAsync*) and
[`Snapshot.VerifyJsonAsync`](xref:PDiffy.Snapshot.VerifyJsonAsync*) check other output the same way as PDFs, with the
same received and verified files, accept mode and report. A test project can keep every kind of snapshot in one place.
`Snapshot.VerifyText` and `Snapshot.VerifyJson` are the synchronous versions, for a test that isn't async.

## Text

```csharp
[Fact]
public async Task Export_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    string csv = await TrailExporter.CreateCsvAsync();

    await Snapshot.VerifyTextAsync(csv);
}
```

- Compared exactly, spaces included. Only CRLF line endings are read as LF.
- Saved as `{name}.verified.txt`. Pass `extension: "csv"` to save as `{name}.verified.csv` instead.
- A verified file that starts with a UTF-8 byte-order mark matches as if it didn't, so files approved by other tools
  carry over.

## JSON

```csharp
[Fact]
public async Task Job_ShouldMatchApprovedSnapshot_WhenSubmitted()
{
    string json = await client.GetStringAsync("/jobs/latest");

    await Snapshot.VerifyJsonAsync(json);
}
```

The JSON is written the same way every time before it's compared:

- Indented with two spaces, keys in the order written, numbers exactly as written.
- Strings (values or keys) that are a GUID or an ISO 8601 timestamp, such as `"2026-10-28T09:15:00Z"`, become `Guid_1`
  and `Date_1`. The same value always gets the same token, so the snapshot still shows that two ids match.
- Dates written for people, such as a guide's "November 14, 2020", and anything inside longer text are kept, because
  that is content.
- Invalid JSON fails the test with the parser's message.

Saved as `{name}.verified.json`.

## Options

| Option | Text | JSON |
|---|---|---|
| [`ScrubText(regex, replacement)`](xref:PDiffy.TextSnapshotOptions.ScrubText*) | Replaces your pattern anywhere | Replaces your pattern anywhere |
| [`ScrubDates()`](xref:PDiffy.TextSnapshotOptions.ScrubDates) | Replaces dates anywhere with `Date_1`, `Date_2`, ... | Also scrubs dates inside longer text |
| [`ScrubGuids()`](xref:PDiffy.TextSnapshotOptions.ScrubGuids) | Replaces GUIDs anywhere with `Guid_1`, `Guid_2`, ... | Also scrubs GUIDs inside longer text |
| [`KeepDates()`](xref:PDiffy.TextSnapshotOptions.KeepDates) | Undoes an earlier `ScrubDates()` | Keeps timestamp strings as they are |
| [`KeepGuids()`](xref:PDiffy.TextSnapshotOptions.KeepGuids) | Undoes an earlier `ScrubGuids()` | Keeps GUID strings as they are |

```csharp
await Snapshot.VerifyTextAsync(csv, configure: options => options
    .ScrubText(new Regex(@"Permit \d+"), "Permit N")
    .ScrubDates());

await Snapshot.VerifyJsonAsync(json, configure: options => options.KeepGuids());
```

## Reading a failure

Text and JSON are compared line by line. A failure lists each changed line by number, with the changed words marked:

```text
2 change(s): 1 removed, 1 text
~ text line 3
    [-"b": 2,-] {+"c": 4+}
- removed line 4
      "c": 3
```

## See also

- [Ignore and scrub](options.md)
- [`TextSnapshotOptions`](xref:PDiffy.TextSnapshotOptions) in the API reference
