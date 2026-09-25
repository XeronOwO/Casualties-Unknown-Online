# The one-top-level-type gate cannot see `readonly` and `sealed` records

- Status: Todo
- Priority: Medium
- Category: Tooling / normative gates / source shape
- Source: the stage 1 adversarial review of the remote-inventory native-intent rework (2026-09-21): the reviewer found two `internal readonly record struct` types declared beside the class that owns them, and the gate that forbids exactly that reported a clean file.
- Related: `docs/evidence/selfchecks/items/remote-inventory-native-intent-stage1-selfcheck.md` (where the hole is recorded), `docs/architecture/remote-inventory-native-parity.md`

## The defect

`SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` counts top-level type
declarations with a regex that enumerates modifiers by hand:

```text
^(public|internal|sealed|static|abstract|partial)*(class|struct|interface|enum|record)\s+(\w+)
```

`readonly` and `file` are missing from that list, and the pattern also has to match a modifier order
that the repository's own style does not always use. So `internal readonly record struct X` and
`internal sealed record Y` are invisible: the rule fires only for the shapes it happens to spell, and
two such types can sit in one file with a green gate. The rule's failure text ("rule: one per file")
therefore overstates what the check reaches, which `tests/AGENTS.md` forbids for any gate.

The same blindness already covers existing files (`Runtime/Session/World/WorldTimePolicy.cs` declares
two `internal sealed record`s), so the fix has to decide what to do with the pre-existing offenders at
the same time.

## What done looks like

1. The matcher accepts every modifier the language allows on a top-level type declaration
   (`public`, `internal`, `private`, `protected`, `file`, `sealed`, `abstract`, `static`, `readonly`,
   `partial`, in any order) — a fact-based matcher, not a shape-based one.
2. The matcher's own samples pin the shapes (a positive sample per modifier and a negative sample for
   a mention inside a doc comment or a nested type), in the style the file already uses.
3. The pre-existing multi-type files are either split (the rule's spirit) or carry a recorded,
   reviewed exception; a census floor keeps the scan from passing by finding nothing.
4. The three-check acceptance for a gate change: the old revision of the tree fails the widened gate,
   the fixed tree passes it, and the repository has no false positives.

## Non-goals

- Not a licence to split files gratuitously: the rule is one top-level type per file, and the fix is
  to make the gate say what it enforces.
