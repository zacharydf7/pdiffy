# Roadmap

The order of work, phase by phase, with where each item stands. Every item is a GitHub issue; the working copy of this
order is [issue #142](https://github.com/zacharydf7/pdiffy/issues/142). [Release notes](release-notes.md) lists what
each release shipped.

| Status | Meaning |
|---|---|
| Done | On `main`; ships in the release it's listed under |
| Partly done | Some of the issue is on `main`; the rest is open |
| Decision needed | Waiting on a design or naming choice before work starts or continues |
| Open | Planned, not started |
| Parked | Kept so the idea isn't lost, not planned |

## At a glance

| Phase | Status |
|---|---|
| [1. What snapshots miss](#1-what-snapshots-miss) | Done |
| [2. API pass](#2-api-pass) | Done, for the next release |
| [3. Adoption traps](#3-adoption-traps) | Done |
| [4. Accessibility checks](#4-accessibility-checks) | Started: #52 to #58 done |
| [5. Double down on the niche](#5-double-down-on-the-niche) | 7 done, 2 partly done, the rest open |

## Decisions needed

| Issue | Decision |
|---|---|
| [#118](https://github.com/zacharydf7/pdiffy/issues/118) | `width=` on text runs prints on every run, so nearly every snapshot changes and the format version goes up |
| [#125](https://github.com/zacharydf7/pdiffy/issues/125) | How a snapshot links structure lines to layout lines, so the report can draw reading order |
| [#145](https://github.com/zacharydf7/pdiffy/issues/145) | Recording East Asian and right-to-left fonts changes many Word snapshots |
| [#146](https://github.com/zacharydf7/pdiffy/issues/146) | A catch-all hash for Word formatting PDiffy doesn't read |

Decided: [#89](https://github.com/zacharydf7/pdiffy/issues/89) gets the full fix, reading each form XObject's marked
content apart from the page's so every element gets its own text. Decided for 0.4.0: the `Accessibility` profile keeps
leaving out `images=` ([#90](https://github.com/zacharydf7/pdiffy/issues/90)), a `span-lang` change is flagged only when
its languages change ([#147](https://github.com/zacharydf7/pdiffy/issues/147)), `pdiffy check` reports untagged text
once per page ([#117](https://github.com/zacharydf7/pdiffy/issues/117)), and the new names stay as they are.

## 1. What snapshots miss

Real changes that gave identical snapshots, or text recorded wrong. Their format changes ship together, so snapshots
are re-approved once.

| Issue | What | Status |
|---|---|---|
| [#82](https://github.com/zacharydf7/pdiffy/issues/82) | Accessibility flag gaps | Done |
| [#83](https://github.com/zacharydf7/pdiffy/issues/83) | Link and bookmark targets | Done |
| [#84](https://github.com/zacharydf7/pdiffy/issues/84) | Word hidden text and skipped formatting | Done |
| [#85](https://github.com/zacharydf7/pdiffy/issues/85) | Rotated text | Done |
| [#86](https://github.com/zacharydf7/pdiffy/issues/86) | Letter-spaced text | Done |
| [#87](https://github.com/zacharydf7/pdiffy/issues/87) | Marked-content replacement text, language and artifact type | Done |
| [#88](https://github.com/zacharydf7/pdiffy/issues/88) | Signatures and form behavior | Done |
| [#89](https://github.com/zacharydf7/pdiffy/issues/89) | MCIDs inside form XObjects | Done |
| [#90](https://github.com/zacharydf7/pdiffy/issues/90) | Figures tied to their images | Done |

Found and fixed along the way: [#143](https://github.com/zacharydf7/pdiffy/issues/143),
[#144](https://github.com/zacharydf7/pdiffy/issues/144), [#147](https://github.com/zacharydf7/pdiffy/issues/147),
[#149](https://github.com/zacharydf7/pdiffy/issues/149), [#151](https://github.com/zacharydf7/pdiffy/issues/151),
[#152](https://github.com/zacharydf7/pdiffy/issues/152), [#153](https://github.com/zacharydf7/pdiffy/issues/153),
[#155](https://github.com/zacharydf7/pdiffy/issues/155) and [#156](https://github.com/zacharydf7/pdiffy/issues/156).

## 2. API pass

Breaking changes, in one release after the next one, before the checks add more names. PDiffy is new, so breaking
changes are fine until 1.0; each one is listed in the release notes.

| Issue | What | Status |
|---|---|---|
| [#91](https://github.com/zacharydf7/pdiffy/issues/91) | Consistent names across PDF and Word | Done; `Images` keeps both shapes (see the issue) |
| [#92](https://github.com/zacharydf7/pdiffy/issues/92) | Model records with real equality | Done |
| [#93](https://github.com/zacharydf7/pdiffy/issues/93) | Enums instead of magic strings | Done |
| [#94](https://github.com/zacharydf7/pdiffy/issues/94) | `SnapshotChange` for Word | Done |
| [#95](https://github.com/zacharydf7/pdiffy/issues/95) | Comparison methods and one front door | Done |

## 3. Adoption traps

Not breaking; each shipped as it was done.

| Issue | What | Status |
|---|---|---|
| [#96](https://github.com/zacharydf7/pdiffy/issues/96) | Streams, byte arrays and error messages | Done |
| [#97](https://github.com/zacharydf7/pdiffy/issues/97) | Duplicate snapshot names | Done |
| [#98](https://github.com/zacharydf7/pdiffy/issues/98) | Suite-wide default options | Done |
| [#99](https://github.com/zacharydf7/pdiffy/issues/99) | Finding the project folder | Done |
| [#100](https://github.com/zacharydf7/pdiffy/issues/100) | Structure tree navigation | Done |
| [#101](https://github.com/zacharydf7/pdiffy/issues/101) | Doc comments | Done |
| [#102](https://github.com/zacharydf7/pdiffy/issues/102) | Missing await | Done |
| [#103](https://github.com/zacharydf7/pdiffy/issues/103) | `--version` and tool pinning | Done |
| [#104](https://github.com/zacharydf7/pdiffy/issues/104) | Accept command in the failure | Done |
| [#105](https://github.com/zacharydf7/pdiffy/issues/105) | Missing-font hint | Done |
| [#106](https://github.com/zacharydf7/pdiffy/issues/106) | New snapshot breakdown | Done |

## 4. Accessibility checks

The epic [#50](https://github.com/zacharydf7/pdiffy/issues/50) and its sub-issues, built on the names from the API pass.
Findings are recorded in an opt-in `checks` section, so a new problem fails a test and a fixed one shows as fixed.

| Issues | What | Status |
|---|---|---|
| [#51](https://github.com/zacharydf7/pdiffy/issues/51) | Catalog of the machine-checkable PDF/UA-1 rules | Open |
| [#52](https://github.com/zacharydf7/pdiffy/issues/52)-[#54](https://github.com/zacharydf7/pdiffy/issues/54) | Severity, categories and rule sets per release | Done |
| [#55](https://github.com/zacharydf7/pdiffy/issues/55)-[#57](https://github.com/zacharydf7/pdiffy/issues/57) | The `checks` section, its diff and the report | Done |
| [#58](https://github.com/zacharydf7/pdiffy/issues/58)-[#61](https://github.com/zacharydf7/pdiffy/issues/61) | Help pages and command-line counts | Done |
| [#62](https://github.com/zacharydf7/pdiffy/issues/62)-[#77](https://github.com/zacharydf7/pdiffy/issues/77) | PDF/UA rules | Partly done: #62-#72, #74 |
| [#78](https://github.com/zacharydf7/pdiffy/issues/78)-[#80](https://github.com/zacharydf7/pdiffy/issues/80) | Warnings: placeholder alt text, vague link text, jumpy reading order | Open |
| [#81](https://github.com/zacharydf7/pdiffy/issues/81) | Checks for Word documents | Open |

## 5. Double down on the niche

| Issue | What | Status |
|---|---|---|
| [#107](https://github.com/zacharydf7/pdiffy/issues/107) | Accepted changes explained in pull requests | Open |
| [#108](https://github.com/zacharydf7/pdiffy/issues/108) | A reflow as one change | Done |
| [#111](https://github.com/zacharydf7/pdiffy/issues/111) | Screen reader transcript | Open |
| [#126](https://github.com/zacharydf7/pdiffy/issues/126) | Accessibility flags for Word | Open |
| [#116](https://github.com/zacharydf7/pdiffy/issues/116) | Docs: catching generator upgrades | Done |
| [#119](https://github.com/zacharydf7/pdiffy/issues/119) | Encoding promise on real PDFs | Open |
| [#109](https://github.com/zacharydf7/pdiffy/issues/109) | Grouped changes and accept rules | Open |
| [#121](https://github.com/zacharydf7/pdiffy/issues/121) | `pdiffy review` | Open |
| [#110](https://github.com/zacharydf7/pdiffy/issues/110) | Structure templates | Open |
| [#112](https://github.com/zacharydf7/pdiffy/issues/112) | WCAG criteria on accessibility changes | Open |
| [#113](https://github.com/zacharydf7/pdiffy/issues/113) | Changes that need a person | Open |
| [#114](https://github.com/zacharydf7/pdiffy/issues/114) | Unused snapshots | Done |
| [#115](https://github.com/zacharydf7/pdiffy/issues/115) | PDF 2.0 and PDF/UA-2 structure | Open |
| [#117](https://github.com/zacharydf7/pdiffy/issues/117) | Untagged text by line | Done |
| [#118](https://github.com/zacharydf7/pdiffy/issues/118) | Run width, transparency, optional content | Partly done: layers; width needs a decision; opacity open |
| [#125](https://github.com/zacharydf7/pdiffy/issues/125) | Reading order on the page map | Decision needed (after [#148](https://github.com/zacharydf7/pdiffy/issues/148), done) |
| [#127](https://github.com/zacharydf7/pdiffy/issues/127) | Word against its exported PDF | Open |
| [#120](https://github.com/zacharydf7/pdiffy/issues/120) | Fuzzing | Partly done: a first pass found [#155](https://github.com/zacharydf7/pdiffy/issues/155); a checked-in harness is open |
| [#123](https://github.com/zacharydf7/pdiffy/issues/123) | Generator recipes | Done |
| [#124](https://github.com/zacharydf7/pdiffy/issues/124) | Standalone binary and GitHub Action | Open |
| [#122](https://github.com/zacharydf7/pdiffy/issues/122) | Open the report on failure | Done |

## Also open

| Issue | What | Status |
|---|---|---|
| [#43](https://github.com/zacharydf7/pdiffy/issues/43) | Page-tree depth inside PdfPig | Open, waiting for PdfPig 0.1.17 |
| [#46](https://github.com/zacharydf7/pdiffy/issues/46) | Load and snapshot time past 10 pages | Open |
| [#48](https://github.com/zacharydf7/pdiffy/issues/48) | The report's word highlight takes in a closing quote | Open |
| [#154](https://github.com/zacharydf7/pdiffy/issues/154) | Layers an image or form XObject puts itself in | Open |

## Parked

[#41](https://github.com/zacharydf7/pdiffy/issues/41), [#128](https://github.com/zacharydf7/pdiffy/issues/128) to
[#141](https://github.com/zacharydf7/pdiffy/issues/141) and [#150](https://github.com/zacharydf7/pdiffy/issues/150): ideas
kept with the `priority: someday` label, not planned.
