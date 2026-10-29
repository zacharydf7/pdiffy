# Performance

How long PDiffy takes to read documents, write their snapshots and compare them, measured with the benchmarks in
`benchmarks/PDiffy.Benchmarks`. The documents are built in code, as the tests' are.

Measured on an Intel Core Ultra 9 285H with Windows 11 and .NET 10.0.14, using BenchmarkDotNet 0.15.8's short run
(three iterations). Treat the numbers as a guide to how things scale, not as exact figures for your machine.

## Reading a PDF

Each page has 30 tagged lines of text, a table of 15 rows, or 10 figures with alt text nested a few sections deep.

| Pages | Content | Load | Write the snapshot | Allocated by load |
|---|---|---|---|---|
| 1 | Text | 0.32 ms | 0.01 ms | 0.8 MB |
| 10 | Text | 3.7 ms | 0.12 ms | 7.4 MB |
| 100 | Text | 97 ms | 2.5 ms | 74 MB |
| 1 | Table | 0.23 ms | 0.01 ms | 0.6 MB |
| 10 | Table | 2.5 ms | 0.11 ms | 5.3 MB |
| 100 | Table | 71 ms | 2.1 ms | 53 MB |
| 1 | Figures | 0.15 ms | 0.01 ms | 0.3 MB |
| 10 | Figures | 1.4 ms | 0.04 ms | 2.7 MB |
| 100 | Figures | 27 ms | 0.9 ms | 26 MB |

What's allocated grows in step with the number of pages. Time does up to 10 pages, then grows faster: 10 times the
pages takes about 20 to 30 times as long to load, and about 20 times as long to write the snapshot. Most of the extra
load time is garbage collection: a load keeps every page's content in memory until it's done, so a large document's
objects live long enough to be collected the slow way. That's tracked in
[issue #46](https://github.com/zacharydf7/pdiffy/issues/46).

## Reading a Word document

Styled paragraphs, plus a table with a row for every ten.

| Paragraphs | Load | Write the snapshot | Allocated by load |
|---|---|---|---|
| 100 | 0.23 ms | 0.07 ms | 0.7 MB |
| 1,000 | 2.5 ms | 1.1 ms | 5.9 MB |

## Comparing snapshots

Two snapshots of the text document above: one with a single paragraph edited, and one of a document whose text shares
no words with it but numbers, so almost no line pairs with another.

| Pages | One paragraph edited | Allocated | Nothing in common | Allocated |
|---|---|---|---|---|
| 10 | 0.7 ms | 2.7 MB | 8 ms | 6.5 MB |
| 100 | 32 ms | 26 MB | 0.64 s | 66 MB |

An edit, the usual case for a failing test, is cheap. When nothing matches, each unmatched line is compared with every
other in its section, so the time grows with the square of the lines that differ, while memory stays modest.

## Running the benchmarks

```text
dotnet run -c Release --project benchmarks/PDiffy.Benchmarks -- --filter '*'
```

`--filter '*Pdf*'`, `'*Docx*'` or `'*Diff*'` runs one group. The results are written to `BenchmarkDotNet.Artifacts`.
The benchmarks aren't run in CI; refresh this page when a change moves the numbers a lot.
