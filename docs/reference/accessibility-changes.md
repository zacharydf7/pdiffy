# Changes that affect accessibility

In a PDF snapshot, a change's [`AffectsAccessibility`](xref:PDiffy.SnapshotChange.AffectsAccessibility) is true, and a
failure lists it first tagged `[accessibility]`, when it is one of these:

| Change | Shows as |
|---|---|
| A structure element's type changed, such as a heading becoming a paragraph. A header cell's generated name, such as `TH1` becoming `TH2`, doesn't count | `type: H2 -> P` |
| A structure element moved in reading order | `moved` in `structure` |
| Alt text, replacement text or an expansion (on an element or on marked content inside it), a table header's scope, a cell's headers, or a form field's or annotation's accessible name was removed or emptied, or a cell's headers point at a header that isn't there | `alt: "Logo" -> (missing)`, `actual: "fi" -> (missing)`, `span-expansion: ["Mount":"Mt."] -> (missing)`, `scope: Column -> (missing)`, `tu: "Name" -> ""`, `headers: [TH1] -> [?h2]` |
| The document's or an element's language changed or was removed, or one set on marked content inside an element (a change to only the words such marked content covers isn't flagged) | `lang: en-US -> en-GB`, `span-lang: [fr-FR:"bonjour"] -> [de-DE:"bonjour"]` |
| Something was added to the untagged content: a line, words in a line, or more images or paths | `added`, `"Page 1 {+Draft+}"` or `images: 1 -> 2` in `untagged` |
| An annotation became untagged | `... tagged ... -> ... untagged ...` in `annotations` |
| Text, an image or a path that was tagged is now an artifact (decoration) or untagged, so a screen reader skips it | `p1 text tagged ... -> p1 text artifact ...` in `layout` |
| The document is no longer marked as tagged, or the viewer no longer shows its title | `marked: yes -> no`, `display-doc-title: yes -> no` |
| The document's title was removed or emptied | `title: "Trail Guide" -> (missing)`, `xmp-title: "Trail Guide" -> ""` |
| A page's tab order is no longer the structure order | `tabs: S -> R` |
| A font's text no longer maps to Unicode, or more of its character codes don't | `to-unicode: yes -> no`, `unmapped: [41] -> [41-43]` |
| A document is newly encrypted, or its permissions changed, so screen readers can no longer extract its text | `permissions: (missing) -> print`, `permissions: print,extract-for-accessibility -> print` |

Less untagged content doesn't count, except that a text line that loses more than half its words shows as removed and
added, and the added line counts.

## How the flag is used

- The rules are the same for every project. A release can add rules, so an upgrade can flag a change that wasn't
  flagged before; [Format changes](format-changes.md) lists each one.
- Content made an artifact is found through the `layout` section. With layout left out, as the `Content` profile
  does, it shows as words leaving its structure element and, for text, a line added to `artifacts`, neither of
  which is flagged.
- A snapshot test passes or fails on the whole snapshot. The flag only orders and marks the explanation, in the test's
  message, the [HTML report](../guide/pdf-snapshots.md#the-html-report) ("Only accessibility" filter), the
  [JSON output](../guide/comparing.md#json-output) and `pdiffy report`.
- To fail only on these changes, compare with [`CompareTo`](../guide/comparing.md) and assert on
  `AffectsAccessibility`.
- Word and text snapshots have no flags.
