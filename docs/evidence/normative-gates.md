# Normative Rule Gate Inventory

This page maps the repository's binding normative requirements to the gate that
actually enforces them. The former `tools/check-*.ps1` scripts have been ported
to C# xUnit tests and removed; every gate below now runs as part of the ordinary
`dotnet test` loop.

Automation status legend:

- **dotnet test (C# port)** — implemented directly in
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests`.
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
| #3 One top-level type per file; file name matches type name | dotnet test (C# port) | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits`. |
| #4 Evidence-based changes / cite decompiled sources | Review / process | Human process; not a source-shape rule. |
| #5 Absolute-machine-path red line | dotnet test (C# port) | `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths`. |
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
| Line-count / architecture gate escapes are real responsibility splits | dotnet test (C# port) | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` + `docs/architecture-debt.json`. |
| Red→green hard gate | Review / process | Process rule; deliverable evidence is the failing-test commit. |
| Core + edge/failure test coverage | dotnet test | Existing xUnit suite; coverage breadth is a review/size question. |
| Every key path observable | Review / process | Logging discipline; not a single source-shape gate. |
| Acceptance-readiness audit | Review / process | `docs/evidence/delivery-checklist.md`. |
| User-found issues are hard blockers | Review / process | Backlog/human process. |
| Independent adversarial self-check | Review / process | Human/process. |
| Delivery checklist | dotnet test (C# port) | `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`. |
| Deployment/artifact verification | PowerShell + process | `tools/deploy.ps1` and deployment hash/file check. |

## Former PowerShell checks

The former `tools/check-*.ps1` scripts were removed after their logic was ported
into this test project. The mapping below records what replaced each one.

| Former check | What it checked | C# replacement |
|---|---|---|
| `check-architecture.ps1` | One type/file, logical line count, bool-flag debt, plus the Phase E guard suite below. | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` |
| `check-gamestate-isolation.ps1` | GameState project isolation from runtime/game/network dependencies. | `SourceShapeGateTests.GameStateIsolation_NoForbiddenReferencesOrTokens` |
| `check-item-authority.ps1` | Legacy item projection tables only mutated by their owners. | `SourceShapeGateTests.ItemAuthority_NoDirectProjectionMutation` |
| `check-no-legacy.ps1` | No removed dual-architecture markers in production source. | `SourceShapeGateTests.NoLegacy_NoRemovedDualArchitectureMarkers` |
| `check-command-authority.ps1` | Every `GameCommand` carries an `AuthorityKind` policy. | `SourceShapeGateTests.CommandAuthority_EveryGameCommandDeclaresAuthority` |
| `check-kernel-shape.ps1` | No string-keyed dictionaries / `Hashtable` kernel state. | `SourceShapeGateTests.KernelShape_NoStringKeyedStateOrHashtable` |
| `check-event-replay.ps1` | Event-replay matrix completeness; also guarded by `ReplayMatrixDataTests`. | `RepositoryGateTests.EventReplayMatrix_Completeness` |
| `check-entity-event-dispatch.ps1` | Entity event dispatch matrix. | `RepositoryGateTests.EntityEventDispatch_AllKindsCoveredInEveryTable` |
| `check-no-absolute-paths.ps1` | Tracked-file absolute machine paths. | `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` |
| `check-delivery.ps1` | Delivery checklist/forbidden-box integrity. | `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` |

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

## Related

- `AGENTS.md` Engineering Conventions #3, #5, #10
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/FullyQualifiedNameGate.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/FullyQualifiedNameGateTests.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/SourceShapeGateTests.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/RepositoryGateTests.cs`
- `docs/evidence/delivery-checklist.md`
