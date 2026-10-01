# The one-top-level-type gate sees every modifier

- Status: Done
- Acceptance (20261001-t): the `new` sample is in the theory and the gate suite is green (300/300); the batch `20260927-b` rejection is closed — record `docs/evidence/acceptance/source-shape-gate-modifier-blindness-20261001.md`.
- Priority: Medium
- Category: Tooling / normative gates / source shape
- Source: the stage 1 adversarial review of the remote-inventory native-intent rework (2026-09-21): the gate's declaration does not equal its reach — the modifier list it spells by hand leaves `internal readonly record struct …` (and the same shapes carrying `file`, `private`, `protected`, `unsafe`, `new` or `ref`) invisible.
- Related: `docs/evidence/selfchecks/items/remote-inventory-native-intent-stage1-selfcheck.md` (where the hole is recorded), `docs/architecture/remote-inventory-native-parity.md`, `docs/evidence/selfchecks/tooling/one-top-level-type-gate-selfcheck.md` (the 2026-09-25 cycle's evidence; its `new`-positive claim is corrected in the fix section below)
- Acceptance record: batch `20261001-t` closed the rejection — `docs/evidence/acceptance/source-shape-gate-modifier-blindness-20261001.md`.

## The defect

`SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` counts top-level type
declarations with a regex that enumerates modifiers by hand:

```text
^(public|internal|sealed|static|abstract|partial)*(class|struct|interface|enum|record)\s+(\w+)
```

`readonly`, `file`, `private`, `protected`, `unsafe`, `new` and `ref` are missing from that list, so
`internal readonly record struct X` (and every other shape carrying one of them) is invisible: the
rule fires only for the shapes it happens to spell, and two such types can sit in one file with a
green gate. (`sealed` was always spelled, so the `internal sealed record Y` the source note named was
never the blind shape; the blindness is the modifier list, and the two-word `record struct` also cost
the matcher its NAME, which the hand-spelled form read as `struct`.) The rule's failure text ("rule:
one per file") therefore overstated what the check reaches, which `tests/AGENTS.md` forbids for any
gate.

The same blindness already covered existing files (`Runtime/Session/World/WorldTimePolicy.cs` declares
two `public readonly record struct`s - `WorldTimePlayerState` and `WorldTimeDecision` - beside the
policy class), so the fix had to deal with the pre-existing offenders at the same time.

## What done looks like

1. The matcher accepts every modifier the language allows on a top-level type declaration
   (`public`, `internal`, `private`, `protected`, `file`, `sealed`, `abstract`, `static`, `readonly`,
   `partial`, `unsafe`, `new`, `ref`, in any order) — a fact-based matcher, not a shape-based one.
2. The matcher's own samples pin the shapes (a positive sample per modifier and a negative sample for
   a mention inside a doc comment or a nested type), in the style the file already uses.
3. The pre-existing multi-type files are either split (the rule's spirit) or carry a recorded,
   reviewed exception; a census floor keeps the scan from passing by finding nothing.
4. The three-check acceptance for a gate change: the old revision of the tree fails the widened gate,
   the fixed tree passes it, and the repository has no false positives.

## What landed (2026-09-25)

- **A fact-based matcher.** `TopLevelTypeRegex` now accepts an optional attribute run, then any number
  of the modifiers the language allows on a type declaration in any order (`public`, `internal`,
  `private`, `protected`, `file`, `static`, `sealed`, `abstract`, `readonly`, `partial`, `unsafe`,
  `new`, `ref`), then one of the five type keywords, with `record struct`/`record class` read as the
  two-word keyword they are so the NAME is the name. `TryReadTopLevelTypeName(line, depth, out name)`
  carries the depth fact and reads the matcher once; the failure text names the declarations found and
  states the scope.
- **35 samples pin it** (29 shape samples plus 6 depth/name samples): a positive per modifier, the
  attribute run, both record keywords, and negatives for a mention in a doc comment (with and without
  declaration text after it), ordinary code, a nested declaration's depth and the declared
  `delegate` boundary.
- **Census floors**: 1000 `src/` files and 1000 top-level declarations against the measured 1662 files
  / 1668 declarations (about 60%). They catch a scan that sees nothing; a narrowed pattern is caught
  by the samples.
- **Seven offenders split** one top-level type per file, the rule's remedy rather than a recorded
  exception: `EnemyCombatArbitration` + `EnemyTargetFact`, `RadiationStragglerPolicy` +
  `RadiationPlayerProgress`, `ItemFollowDecision` + `FollowDecision`, `LayerModifierDecide` +
  `LayerModifierDecision`, `TrapLayoutAlign` + `TrapLayoutAlignment`, `WorldTimeLocalInitiation` +
  `WorldTimeRampStep`, `WorldTimePolicy` + `WorldTimePlayerState` + `WorldTimeDecision`.
- **Scope recorded, not assumed**: the rule counts DEPTH-0 declarations only (its name and its
  implementation always said so). Nested declarations - 306 across 130 files, e.g.
  `RemoteItemPresentation.SourceValues` and the `RemoteCharacterPresentation` records - are not
  offenders: they belong to their owner and count toward its aggregate. Declared limits: a
  line-wrapped declaration and a top-level `delegate` are still outside the matcher (both measured
  zero in `src/`), and the brace counter is string-blind (pre-existing).

Red, evidence and the independent review's dispositions:
`docs/evidence/selfchecks/tooling/one-top-level-type-gate-selfcheck.md`.

## Fix after batch `20260927-b` (the `new` sample)

Row 4 was right: the shape theory carried no positive sample for `new`, so dropping `new` from
`TopLevelTypeRegex` failed nothing. The fix is one sample, `public new class Foo` — the modifier is
legal on a nested type declaration, the matcher reads the line rather than the brace depth, and with
the sample in place the mutation fails while the restored matcher passes. The theory now pins 29 shape
samples (35 with the 6 depth/name samples). The 2026-09-25 selfcheck's "a positive per modifier" line
was wrong when written; the correction is recorded here and appended to that point-in-time record rather than silently rewriting its claim.
Status returned to `Review`: the next acceptance batch judges row 4 against the widened sample set.

## Non-goals

- Not a licence to split files gratuitously: the rule is one top-level type per file, and the fix is
  to make the gate say what it enforces.
