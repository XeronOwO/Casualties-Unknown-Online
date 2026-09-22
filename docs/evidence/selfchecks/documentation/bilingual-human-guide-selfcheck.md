# Bilingual human guide — three layers, paired pages, and the gate that holds a pair together

Date: 2026-09-22
Scope: `review/bilingual-human-docs.md`. The repository gains the human-facing layer it never had:
three layers declared by index, the two guide levels paired English + Chinese with a consistency
record, and a gate that fails the moment one side moves without the pair being re-confirmed. 45 new
paired files, two policy documents, a nested instruction file and two gate sources; no runtime code
and no wire change.

## What landed

- **The policy** — `docs/i18n/README.md` (7,270 bytes): the layer rule, the paired scope, the
  three-file pair, the switchers, the structural mirror, the record, the maintenance workflow, and
  the stated limit that a green gate proves the pair was confirmed at those contents rather than that
  the counterpart is accurate.
- **One terminology source** — `docs/i18n/terminology.md` (5,974 bytes, 52 rows) with the columns
  English, 中文, First use, Never render as and Notes; a rendering is decided once, not per page.
- **The layer rule inside the instruction chain** — `docs/AGENTS.md` (711 bytes) carries the
  subtree rules and links the detail; the root `AGENTS.md` states the layer rule in four lines;
  `docs/README.md` states the three layers up front and points a Chinese reader at the paired index.
- **The guide** — 15 paired topics, 45 files: 6 player pages under `docs/guide/` and 9 developer
  pages under `docs/developer/`, each topic as `foo.md` + `foo.zh.md` + `foo.i18n.yaml`.
- **The gate** — `HumanDocsPairing.cs` (562 lines, pure checks) and `HumanDocsPairingGateTests.cs`
  (291 lines, 9 cases) in `tests/CasualtiesUnknownOnline.NormativeGates.Tests`; no new toolchain and
  no new dependency.

## Mechanism inventory

| Mechanism | Change | Evidence |
| --- | --- | --- |
| Layer declaration | A directory declares its layer through its index: agent instructions, a human entry, or a paired guide index | `docs/i18n/README.md`; the three-layer table in `docs/README.md`; `docs/AGENTS.md` |
| Paired scope | Only the two guide trees are paired; the 15 topics are discovered from the tree, not from a manifest | `EveryPairedTopic_HasItsThreeSiblingFiles` (discovery floor 12) |
| Consistency record | Each side's `git hash-object` value must equal the recorded 40-hex blob hash | `EveryPair_MatchesItsRecordedBlobHashes` |
| Language switchers | Both sides carry their switcher immediately under the H1, and it names the sibling file | `EveryPair_CarriesBothLanguageSwitchers` |
| Structural mirror | Heading depth and order, paragraphs as units, list kind, item numbers and item shape (links, inline code, bold spans), table rows by column count and per-cell shape, verbatim code blocks, and link targets in order (inline links, images and reference definitions), with corpus targets localized; a pipe escaped as `\|` is cell content rather than a separator | `EveryPair_MirrorsItsStructureAndLinks` |
| Inverse scope | A `.zh.md` or `.i18n.yaml` anywhere in the repository's own file set (tracked plus untracked-and-not-ignored) outside the two guide trees is an error | `NoPairedArtifact_LivesOutsideTheGuideScope` (with its synthetic scan contract) |
| Instruction budget | `docs/AGENTS.md` stays at or under 1,024 bytes and the whole workspace chain — root, `docs/AGENTS.md` and the machine-local file that competes for the same loader budget — stays under 65,536 bytes (65,375 today) | `AgentInstructionChain_StaysInsideTheWorkspaceBudget` |
| Checker contract | A valid pair passes; every break class is flagged (switcher text and target, list item count, dropped paragraph, table width, code-block body, unlocalized link, record shape); line wrapping and switcher wording are not treated as structure | `ThePairingChecks_AcceptAValidPair`, `ThePairingChecks_FlagEveryContractBreak`, `TheStructureCheck_IgnoresLineWrappingAndSwitcherWording` |

## Verification

| Check | Result |
| --- | --- |
| Build (inside the suite run, `dotnet test CasualtiesUnknownOnline.slnx`) | 0 warnings, 0 errors |
| `dotnet format CasualtiesUnknownOnline.slnx` | exit 0 |
| Normative gates project | 128 total, 9 of them this gate's; 127/128 before the checklist boxes are filled, the single failure being `DeliveryChecklist_NoIncompleteRequiredBoxes` (it is filled after the change lands) |
| Full suite with build | 3795/3795 |
| Independent adversarial review (separate context, frozen tree) | 0 blocker / 4 major / 8 minor / 3 nit; all fixed in this commit — the mirror now counts item and cell shapes, reports a differing window plus the length difference, treats an escaped pipe as cell content, and reads reference definitions; the Chinese pages were aligned to `terminology.md`, and the instruction-budget check now includes the machine-local file |
| Real-tree control 1 — one line appended to `docs/guide/playing.md`, record untouched | `EveryPair_MatchesItsRecordedBlobHashes` red and names the pair: recorded `9a9d2273…` versus a file hashing to `2842c5eb…`; after restoring the file its hash equals the record again |
| Real-tree control 2 — `docs/evidence/stray-check.zh.md` created | `NoPairedArtifact_LivesOutsideTheGuideScope` red with that path; the file was removed afterwards |
| `docs/handoff/` removal (ticket verification item) | the directory does not exist; the remaining mentions are the ticket that ordered the removal and this fact sheet, and no index points at it |

## Limits

- The gate compares blob hashes and Markdown structure, never meaning: a re-recorded pair with a
  sloppy counterpart passes it. Wording, terminology and accuracy stay review duties, exactly as the
  policy page states.
- The mirror never reads either side: translated words are not comparable, so markers are counted
  instead. Two same-shaped list items can trade places, two same-shaped cells can swap columns, a
  paragraph can say something else, and a heading can keep its depth while its meaning drifts — all of
  that passes. `docs/i18n/README.md` states this limit rather than implying a stronger one.
- The workspace instruction chain now sits 161 bytes under its 65,536-byte budget, and the
  machine-local `AGENTS.local.md` is the largest term in it; the gate fails when the chain no longer
  fits rather than reporting headroom that does not exist.
- No runtime code, no wire change, no behaviour claim: this cycle cannot and does not assert anything
  about host or guest behaviour.
- The player-facing steps are written from documented behaviour. This cycle ran no game client, so the
  ticket's "every tutorial step executed once against the current build" is **not** verified here and
  stays part of the user's acceptance pass.
- The discovery floor (12) sits deliberately below the 15 topics present, so adding a page needs no
  gate edit; the floor only catches a discovery break such as a renamed scope root.
- `AGENTS.local.md` is outside the instruction-budget check on purpose: it is machine-local,
  gitignored and not reviewable from the repository.
