# One-top-level-type gate: every modifier, and the seven files it exposed - self-check (2026-09-25)

Ticket: `docs/backlog/todo/source-shape-gate-modifier-blindness.md` (Medium; found by the stage-1
adversarial review of the remote-inventory native-intent rework). Cycle scope: make
`SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` see every legal
top-level type declaration instead of the modifier spellings it happened to enumerate, pin the
matcher with its own samples, and deal with the offenders the widening exposes. No runtime
behaviour changes: the `src/` half is a file-boundary split.

## 1. Mechanism inventory - what the old matcher reached

The old matcher spelled the modifiers by hand:

    ^(public\s+|internal\s+|sealed\s+|static\s+|abstract\s+|partial\s+)*(class|struct|interface|enum|record)\s+(\w+)

Three consequences, all measured on the frozen pre-change tree:

- **Blind to seven modifiers.** `readonly`, `file`, `private`, `protected`, `unsafe`, `new` and
  `ref` are not in that list, so any declaration carrying one of them never matched.
- **Blind to the two-word record keyword.** The name group follows the FIRST type keyword, so
  `public sealed record struct X` recorded `struct` as the type name. Latent today: `src/` declares
  no such shape (checked by grep over the tree), so the defect cost the aggregate accounting its
  key rather than reporting a wrong file.
- **A file whose only declaration was invisible was skipped whole** (`topLevel.Count != 1` ->
  `continue`), so it also escaped the 600-line aggregate and five-boolean-flag limits: 51 files
  under `src/` were outside the accounting entirely.

The widened matcher sees 1668 top-level declarations across 1662 `src/` C# files; the old one saw a
strict subset of them.

## 2. The seven offenders (each a file the OLD gate called clean)

| File | Top-level types | Why the old matcher missed the extra one |
|---|---|---|
| `Runtime/Session/EntitySync/EnemyCombatArbitration.cs` | `EnemyTargetFact`, `EnemyCombatArbitration` | `public readonly struct` |
| `Runtime/Session/EntitySync/RadiationStragglerPolicy.cs` | `RadiationPlayerProgress`, `RadiationStragglerPolicy` | `public readonly struct` |
| `Runtime/Session/Items/ItemFollowDecision.cs` | `FollowDecision`, `ItemFollowDecision` | `internal readonly struct` |
| `Runtime/Session/World/LayerModifierDecision.cs` | `LayerModifierDecision`, `LayerModifierDecide` | `internal readonly struct` |
| `Runtime/Session/World/TrapLayoutAlign.cs` | `TrapLayoutAlignment`, `TrapLayoutAlign` | `internal readonly struct` |
| `Runtime/Session/World/WorldTimeLocalInitiation.cs` | `WorldTimeRampStep`, `WorldTimeLocalInitiation` | `public readonly record struct` |
| `Runtime/Session/World/WorldTimePolicy.cs` | `WorldTimePlayerState`, `WorldTimeDecision`, `WorldTimePolicy` | two `public readonly record struct` |

All seven were split (one top-level type per file, the rule's remedy) rather than recorded as debt;
the moved declarations are byte-identical to their pre-change text, only the file boundary moved.

**Scope of the rule (and the limit of this fix).** The rule counts DEPTH-0 declarations only - that
is what its name says, what the gate has always implemented, and what the adversarial review's major
finding turned on before it was withdrawn: a nested type belongs to its owner, its lines count toward
the owner's 600-line / 5-flag aggregate, and it is not a second top-level type. The tree leans on
that everywhere - 306 nested declarations across 130 files at this revision, the largest being
`RemoteDragMutationPatches` 15, `BodyPatches` 12, `RemoteMedicalPatches` 12 and `ModContext` 12. The
seven files above are the entire depth-0 offender set, before and after the widening.

## 3. Self-check table - claim x evidence

| # | Claim | Evidence |
|---|---|---|
| 1 | The matcher accepts every modifier the language allows, in any order, plus a leading attribute run | `TheMatcher_SeesEveryDeclarationShapeAndIgnoresMentions`: 28 samples - a positive per modifier (`readonly`, `file`, `private`, `protected`, `unsafe`, `new`, `ref`, `static` as a BARE modifier, `sealed`, `abstract`, `partial`), the attribute run, and `record struct`/`record class`; the negatives include a doc comment with and without declaration text after it, and the declared `delegate` boundary |
| 2 | A nested declaration is not a top-level type, and a mention in a comment or in ordinary code is not a declaration | the same theory's negatives plus `OnlyADepthZeroDeclarationCounts_AndTheNameFollowsTheKeyword` (depth 1 and 3 cases; `//` and `///` samples; `var`, method, field and `using` lines) |
| 3 | The name of a two-word record keyword is read correctly | `OnlyADepthZeroDeclarationCounts...` reads `Point` and `Node`, not `struct`/`class` |
| 4 | The blindness was real: the OLD gate passed all seven offenders | the pre-change gates run is green (`%TEMP%/cuo-gates-docs-move.txt`, 175/175) while the widened gate on the same tree fails, naming exactly those seven with their type names (`%TEMP%/cuo-red-top-level-gate.txt`) |
| 5 | One top-level type per file after the split | `SourceShapeGateTests` 61/61; normative gates 206/206 |
| 6 | The split changed no behaviour | build 0 warnings / 0 errors; main suite 3986/3986 with build; each moved declaration is byte-identical to its pre-change text (the seven diffs are deletions only) |
| 7 | No false positives anywhere in the repository | scratch audit of the frozen tree: 1974 widened-matcher hits under `src/` (1668 of them at depth 0); taking the text after the matched name with surrounding whitespace trimmed, every hit leaves a declaration continuation - `(` 609, end of line 1180, `:` 180, `<` 3, `{` 1, `;` 1 - and never prose; the gate itself is green |
| 8 | The scan cannot pass by finding nothing | `SourceFileFloor` / `TopLevelTypeFloor` = 1000 against the measured 1662 files / 1668 declarations (about 60%), each with its own failure message |
| 9 | This is a gate/tooling cycle: no wire, protocol, save or gameplay change | the `src/` half moves declarations between files; no production statement changed; the earlier commit of the cycle is documentation and backlog status only |

## 4. Red, ladder and the three checks a gate change owes

Red (required before the fix): the widened gate on the UNCHANGED `src/` tree fails and names the
seven files - `%TEMP%/cuo-red-top-level-gate.txt`. The old gate on that same tree was green
(`%TEMP%/cuo-gates-docs-move.txt`, 175/175): the defect was the gate's reach, not the tree.

Ladder, on the code frozen after the review round: focused `SourceShapeGateTests` 64/64
(`%TEMP%/cuo-focused-source-shape-final.txt`) -> normative gates 209/209 UNFILTERED
(`%TEMP%/cuo-gates-final.txt`) -> full suite WITH build 3986/3986 main + 208 gates, exit 0
(`%TEMP%/cuo-full-final.txt`; the evidence run filters out
`DeliveryChecklist_NoIncompleteRequiredBoxes` because this cycle's checklist is reset while it is
being filled, and the unfiltered gate run afterwards is what proves it complete) -> `dotnet format`
exit 0 having rewritten nothing (`%TEMP%/cuo-format-cycle2.txt`).

The ticket's own acceptance list: (a) the old revision of the tree fails the widened gate - the red
above; (b) the fixed tree passes it - the ladder above; (c) the repository has no false positives -
row 7; (d) the pre-existing offenders are split rather than exempted - §2 (seven files, no exception
list, no `docs/architecture-debt.json` entry).

## 5. Independent adversarial review

A fresh-context reviewer, read-only against the frozen tree; full report in the session artifact
`%TEMP%/cuo-review-top-level-type-gate.md`. It reproduced every headline number independently (1662
`src/` files and 1668 depth-0 declarations at widening from a `git archive HEAD` snapshot, 1974
matcher hits, 1670 files after the split, the sample counts, gates and suite totals, `dotnet format`
exit 0 with zero rewrites, the deletions-only `src/` diff, all eight moved declarations byte-identical
to their pre-change text) and cleared the reference pass: the only `.cs` filename literal among the
touched documents is `WorldTimeLocalInitiation.cs`, which still exists; no csproj lists files
explicitly; `BacklogReferenceGateTests` is green.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | major -> minor | It first reported the widening as incomplete: 127 files still declare several types. Counter-evidence (only depth-0 declarations are the rule's subject; 306 nested declarations across 130 files are the repository's normal composition and stay inside their owner's aggregate; the stage-1 note it cites records the root cause as "the gate's declaration does not equal its reach") made it withdraw the major and keep the documentation half. | landed: the matcher's doc comment, the gate's summary, the failure text and §2/§6 of this file now state the depth-0 scope and the measured nested numbers |
| 2 | minor | No sample pinned a BARE `static` - `static partial class Foo` also matched through `partial`, so the ticket's "every modifier pinned" was not literally true | landed: `public static class Foo` sample |
| 3 | minor | The doc-comment negative only exercised the `/// <summary>` shape; the reviewer held that `/// public class Foo` would match | landed: that sample was added and it is a NEGATIVE (the anchored matcher never reaches past `//`), so the shape is pinned and the claim is refuted by the test rather than by argument |
| 4 | minor | §3 row 7's character list was unstated in method and omitted the space case | landed: row 7 now names the method (whitespace-trimmed) and the exact histogram |
| 5 | nit | A top-level `delegate` is unmatched and unstated (0 live occurrences) | landed: declared as a limit in the matcher's doc comment and pinned as a negative sample |
| 6 | nit | The ticket's own example was stale ("two `internal sealed record`s"; the real shapes are two `public readonly record struct`s beside the class) | landed: corrected in the ticket with the shapes quoted |
| 7 | nit | The census floors catch an empty scan, not a narrowed matcher | landed: the floors' doc comment now says exactly that and points at the samples as the narrowing guard |
| 8 | nit | The brace counter is string-blind (pre-existing) | recorded in §6 as a declared limit |

Not checkable by the reviewer and not claimed here: the red run's tree state (it no longer exists;
the captured output is the record), any runtime or deployment property, and checkout line-ending
behaviour.

## 6. Limits - what this cycle does not prove

- The scan is line-based: a type declaration wrapped across lines would still be missed. Measured
  zero such declarations under `src/` at the widening, and the limit now sits in the matcher's own
  doc comment instead of staying implicit.
- The gate's surface is `src/`; `tests/` and `tools/` are outside it (unchanged by this cycle).
- The rule counts depth-0 declarations only; nested types are deliberately not counted (306 across
  130 files at this revision, all inside their owner and its aggregate accounting) - see §2.
- A top-level `delegate` is not matched (zero in `src/`): its name does not follow its keyword, and a
  generic delegate's name needs parsing rather than a regex. Declared in the matcher's doc comment
  and pinned as a negative sample.
- The brace counter is string-blind: a `{` inside a string literal on a declaration line would skew
  that file's depth. Pre-existing (the counter predates this cycle) and not measured here.
- The census floors catch a scan that sees nothing or almost nothing; a narrowed pattern is caught by
  the matcher's samples instead.
- Nothing here is a runtime claim: no game session, no deployment and no dual-client check is
  involved or claimed; the user's "no deployment this cycle" instruction is honoured.
- The companion docs commit is a status correction, not new work: the world-time ticket's reopened
  requirement shipped in `b045b974` with decision 223 and its own review ticket.
