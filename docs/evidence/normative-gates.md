# Normative Rule Gate Inventory

This page maps the repository's binding normative requirements to the gate that
actually enforces them. It exists because several conventions were previously
documented only in `AGENTS.md` prose or in commit-time PowerShell scripts; this
inventory makes the automation status explicit and tracks which rules are now
part of the ordinary `dotnet test` loop.

Automation status legend:

- **dotnet test (C# port)** — a unit-test gate in
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests`; the check is implemented
  directly in C# and runs as part of the normal test suite.
- **PowerShell gate** — one of the `tools/check-*.ps1` scripts listed in the
  repository commit gate (`AGENTS.md` Build / Commit Gates).
- **dotnet format / .editorconfig** — build-enforced formatting and code-style
  IDE rules.
- **Review / process** — enforced by human review, delivery checklist, or an
  explicit process step; not reliably expressible as a source gate without
  false positives or a semantic judgment.

## Engineering conventions

| Rule (AGENTS.md) | Automation status | Gate / evidence |
|---|---|---|
| #1 English in code/comments/docs | Review / process | Not reliably automatable; no language-quality gate. |
| #2 Modern idiomatic C# (`var`, nullable, `is null`, collection expressions) | dotnet format / .editorconfig | `.editorconfig` + `EnforceCodeStyleInBuild`; nullable is project-wide. |
| #2 Unity objects use `== null` / `!= null` | Review / process | Documented Unity exception; IDE0031 deliberately disabled because a global `?.` rewrite would break Unity object semantics. |
| #3 One top-level type per file; file name matches type name | dotnet test (C# port) + PowerShell gate | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` plus `tools/check-architecture.ps1`. |
| #4 Evidence-based changes / cite decompiled sources | Review / process | Human process; not a source-shape rule. |
| #5 Absolute-machine-path red line | dotnet test (C# port) + PowerShell gate | `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` plus `tools/check-no-absolute-paths.ps1`. |
| #6 Requirement triage | Review / process | Human judgment. |
| #7 Self-learning / record reusable knowledge | Review / process | Human process. |
| #8 Architecture-first, get consent | Review / process | Approval process before risky changes. |
| #9 Patch hooks report only verified writes | dotnet test + runtime | Existing patch-contract tests and adapter contract tests; this is behavioral, not a syntax gate. |
| #10 Prefer `using` / aliases over fully qualified names | **dotnet test** | Roslyn gate: `FullyQualifiedNameGateTests`. |
| #11 Attribute/reflection registration for large families | Review / process | Design preference; no reliable syntax gate without false positives. |
| #12 Reuse native game UI | Review / process | Acceptance-readiness audit; explicitly a human acceptance decision. |

## Quality and delivery rules

| Rule | Automation status | Gate / evidence |
|---|---|---|
| No self-assumption; every claim needs evidence | Review / process | Human/process; reflected in self-check fact sheets. |
| Root cause over patch stacking | Review / process | Human architecture review. |
| Line-count / architecture gate escapes are real responsibility splits | dotnet test (C# port) + PowerShell gate | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` + `docs/architecture-debt.json`. |
| Red→green hard gate | Review / process | Process rule; deliverable evidence is the failing-test commit. |
| Core + edge/failure test coverage | dotnet test | Existing xUnit suite; coverage breadth is a review/size question. |
| Every key path observable | Review / process | Logging discipline; not a single source-shape gate. |
| Acceptance-readiness audit | Review / process | `docs/evidence/delivery-checklist.md`. |
| User-found issues are hard blockers | Review / process | Backlog/human process. |
| Independent adversarial self-check | Review / process | Human/process. |
| Delivery checklist | dotnet test (C# port) + PowerShell gate | `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` + `tools/check-delivery.ps1`. |
| Deployment/artifact verification | PowerShell + process | `tools/deploy.ps1` and deployment hash/file check. |

## Existing tools checks

| Tool | What it checks | C# unit-test port |
|---|---|---|
| `tools/check-architecture.ps1` | One type/file, logical line count, bool-flag debt, plus the Phase E guard suite below. | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` |
| `tools/check-gamestate-isolation.ps1` | GameState project isolation from runtime/game/network dependencies. | `SourceShapeGateTests.GameStateIsolation_NoForbiddenReferencesOrTokens` |
| `tools/check-item-authority.ps1` | Legacy item projection tables only mutated by their owners. | `SourceShapeGateTests.ItemAuthority_NoDirectProjectionMutation` |
| `tools/check-no-legacy.ps1` | No removed dual-architecture markers in production source. | `SourceShapeGateTests.NoLegacy_NoRemovedDualArchitectureMarkers` |
| `tools/check-command-authority.ps1` | Every `GameCommand` carries an `AuthorityKind` policy. | `SourceShapeGateTests.CommandAuthority_EveryGameCommandDeclaresAuthority` |
| `tools/check-kernel-shape.ps1` | No string-keyed dictionaries / `Hashtable` kernel state. | `SourceShapeGateTests.KernelShape_NoStringKeyedStateOrHashtable` |
| `tools/check-event-replay.ps1` | Event-replay matrix completeness; also guarded by `ReplayMatrixDataTests`. | `RepositoryGateTests.EventReplayMatrix_Completeness` |
| `tools/check-entity-event-dispatch.ps1` | Entity event dispatch matrix. | `RepositoryGateTests.EntityEventDispatch_AllKindsCoveredInEveryTable` |
| `tools/check-no-absolute-paths.ps1` | Tracked-file absolute machine paths. | `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` |
| `tools/check-delivery.ps1` | Delivery checklist/forbidden-box integrity. | `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` |

## Why the FQ-name rule uses Roslyn

The rule is a pure C# source-shape convention and is best served by a Roslyn
syntax tree rather than textual scanning. The gate:

- parses every `src/` and `tests/` C# file with official Roslyn APIs
  (`Microsoft.CodeAnalysis.CSharp`);
- reports type names and static/type member accesses that are fully qualified
  by a known namespace root when a `using`/alias is the natural cleaner form;
- allows the documented exceptions: namespace declarations, using directives,
  string literals/HotRepl strings, and enclosing-type member-name collisions
  (for example `System.IO.Path` inside a class that declares a method named
  `Path`).

## How the remaining PowerShell checks became unit tests

The remaining `tools/check-*.ps1` gates were ported directly to C# xUnit tests,
not merely invoked from a test wrapper. The ports live in:

- `SourceShapeGateTests.cs` — architecture, GameState isolation, item authority,
  no-legacy, command authority, kernel shape;
- `RepositoryGateTests.cs` — no-absolute-paths, event-replay matrix,
  entity-event dispatch, delivery checklist.

The PowerShell scripts remain in `tools/` as the standalone commit-time
commands, but the C# ports are now part of the ordinary `dotnet test` run and
are the unit-test-form enforcement requested by the backlog ticket.

## Related

- `AGENTS.md` Engineering Conventions #10
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/FullyQualifiedNameGate.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/FullyQualifiedNameGateTests.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/SourceShapeGateTests.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/RepositoryGateTests.cs`
- `docs/evidence/delivery-checklist.md`
