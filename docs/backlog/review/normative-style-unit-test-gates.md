# Convert non-.editorconfig normative requirements into unit-testable gates

- Status: Review
- Priority: High
- Category: Tooling / engineering gates / Roslyn analyzers / maintainability
- Source: User request (2026-09-05) — normative checks that live only in
  `tools/` gates or `AGENTS.md` prose, and which `.editorconfig` cannot express,
  should become unit-test-form gates. Official C# syntax/analysis libraries may
  be used to implement them.

## Problem

Several repository rules are currently enforced only by review discipline or
by small PowerShell/static checks. A concrete example is the rule requiring
`using` directives / aliases over unnecessary fully qualified type names
(`AGENTS.md` Engineering Conventions #10). `.editorconfig` can format but cannot
reliably reject a fully qualified name when a `using` would be clearer. Other
normative requirements in `AGENTS.md` and in `tools/check-*.ps1` have the same
gap: they are documented but not automatically caught in a normal build/test
cycle.

## Goal

Make the repository's normative, non-formatting rules executable as unit-test
gates. Use official Roslyn APIs (e.g. `Microsoft.CodeAnalysis.CSharp` /
`Microsoft.CodeAnalysis.CSharp.Workspaces`) to parse and inspect C# syntax
instead of relying on brittle textual heuristics where a real syntax tree is
available.

Cover at least:

- Unnecessary fully qualified type names when a `using` directive or alias
  would be the natural, cleaner form.
- Other `AGENTS.md` normative requirements that are currently prose-only and
  can be checked reliably from syntax/semantic data.
- Existing `tools/check-*.ps1` checks that are really source-shape checks and
  can be migrated into the test suite without losing their current coverage.

## Scope

- Inventory the normative rules in `AGENTS.md` and the existing `tools/`
  checks.
- For each rule, decide one of:
  1. implement as a Roslyn-based test/analyzer gate;
  2. keep as-is with a documented reason why it cannot be automated;
  3. remove/merge if redundant or obsolete.
- Add the gates to the standard `dotnet test` run so they are part of the
  ordinary build/test loop, not optional PowerShell-only checks.
- Keep the gates precise: no false positives on legitimate cases such as
  intentional fully qualified names (ambiguity, HotRepl eval context where
  `using` is unavailable, external-name collisions).
- No change to runtime behavior or production code.

## Acceptance criteria

- A normative-rule inventory is added (or an existing checklist is extended)
  mapping each rule to its gate/automation status.
- At least the unnecessary-fully-qualified-name rule has an automated test that
  fails on a clear violation and passes on the accepted forms.
- All new gates pass in the full test suite.
- Build, format, architecture, event-replay, entity-event-dispatch,
  no-absolute-paths, and delivery gates remain green.
- The implementation uses official Roslyn/syntax-tree APIs rather than
  hand-rolled regex-only scanning, unless the rule genuinely cannot be
  represented in a syntax tree.

## Non-goals

- Not replacing `dotnet format` or `.editorconfig`; formatting stays where it
  belongs.
- Not creating an analyzer for every low-value personal preference.
- Not changing repository architecture or runtime semantics.
- Not turning review requirements into an unmanageable, noisy gate set.

## Evidence / references

- `AGENTS.md` Engineering Conventions #10 (prefer `using` / aliases over fully
  qualified names).
- Existing tools gates: `tools/check-architecture.ps1`,
  `tools/check-event-replay.ps1`, `tools/check-entity-event-dispatch.ps1`,
  `tools/check-no-absolute-paths.ps1`, `tools/check-delivery.ps1`.
- Candidate official libraries: `Microsoft.CodeAnalysis.CSharp`,
  `Microsoft.CodeAnalysis.CSharp.Workspaces`.

## Implementation / evidence (2026-09-06)

- Added a dedicated Roslyn gate test project:
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests` (net8.0, xUnit,
  `Microsoft.CodeAnalysis.CSharp` 4.8.0). It is part of the solution and the
  normal `dotnet test` run.
- Implemented `FullyQualifiedNameGate`: a Roslyn syntax-tree scanner for
  AGENTS.md #10. It reports unnecessary fully qualified type names and
  namespace-qualified static/type member accesses, while excluding using
  directives, namespace declaration names, string literals, and enclosing-type
  member-name collisions.
- Added tests for the violation, accepted forms, file-scoped namespace body
  handling, the `System.IO.Path` member-collision exception, and a repository
  scan over `src/` and `tests/`.
- Added `docs/evidence/normative-gates.md`, the normative rule → automation
  status inventory requested by the ticket.
- Added `ExistingPowerShellGateTests`, which invokes every
  `tools/check-*.ps1` from the ordinary `dotnet test` run, so the remaining
  repo-wide/data/process PowerShell gates are no longer optional commit-time
  scripts.
- Cleaned the small set of existing fully qualified names that the new gate
  caught (`System.StringComparison`, `System.InvalidOperationException`,
  `System.Collections.Generic.IReadOnlyDictionary`).
- Full `dotnet test` (2335 existing + 6 new), `dotnet format`,
  `check-architecture`, `check-event-replay`, `check-entity-event-dispatch`,
  `check-no-absolute-paths`, and `check-delivery` pass.
