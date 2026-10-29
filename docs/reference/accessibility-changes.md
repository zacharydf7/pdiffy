# Changes that affect accessibility

In a PDF snapshot, a change's [`AffectsAccessibility`](xref:PDiffy.SnapshotChange.AffectsAccessibility) is true, and a
failure lists it first tagged `[accessibility]`, when it is one of these:

| Change | Shows as |
|---|---|
| A structure element's type changed, such as a heading becoming a paragraph. A header cell's generated name, such as `TH1` becoming `TH2`, doesn't count | `type: H2 -> P` |
| A structure element moved in reading order | `moved` in `structure` |
| Alt text, a table header's scope, a cell's headers, or a form field's or annotation's accessible name was removed or emptied, or a cell's headers point at a header that isn't there | `alt: "Logo" -> (missing)`, `scope: Column -> (missing)`, `tu: "Name" -> ""`, `headers: [TH1] -> [?h2]` |
| The document's or an element's language changed or was removed | `lang: en-US -> en-GB` |
| Something was added to the untagged content: a line, words in a line, or more images or paths | `added`, `"Page 1 {+Draft+}"` or `images: 1 -> 2` in `untagged` |
| An annotation became untagged | `... tagged ... -> ... untagged ...` in `annotations` |

Less untagged content doesn't count, except that a text line that loses more than half its words shows as removed and
added, and the added line counts.

## How the flag is used

- The rules are fixed, so every project sees the same flags.
- A snapshot test passes or fails on the whole snapshot. The flag only orders and marks the explanation, in the test's
  message, the [HTML report](../guide/pdf-snapshots.md#the-html-report) ("Only accessibility" filter), the
  [JSON output](../guide/comparing.md#json-output) and `pdiffy report`.
- To fail only on these changes, compare with [`CompareTo`](../guide/comparing.md) and assert on
  `AffectsAccessibility`.
- Word and text snapshots have no flags.
