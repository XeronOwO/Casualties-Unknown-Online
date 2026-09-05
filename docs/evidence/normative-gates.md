# Normative Rule Gate Inventory

This page maps the repository's binding normative requirements to the gate that
actually enforces them. It exists because several conventions were previously
documented only in `AGENTS.md` prose or in commit-time PowerShell scripts; this
inventory makes the automation status explicit and tracks which rules are now
part of the ordinary `dotnet test` loop.

Automation status legend:

- **dotnet test** — a unit-test gate in `tests/CasualtiesUnknownOnline.NormativeGates.Tests`.
- **dotnet test (script wrapper)** — the canonical `tools/check-*.ps1` script is
  invoked by `ExistingPowerShellGateTests`, so the PowerShell gate is part of the
  ordinary `dotnet test` run while keeping one implementation.
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
| #3 One top-level type per file; file name matches type name | dotnet test (script wrapper) + PowerShell gate | `tools/check-architecture.ps1` (also aggregates logical line counts and bool-flag debt), run by `ExistingPowerShellGateTests`. |
| #4 Evidence-based changes / cite decompiled sources | Review / process | Human process; not a source-shape rule. |
| #5 Absolute-machine-path red line | dotnet test (script wrapper) + PowerShell gate | `tools/check-no-absolute-paths.ps1`; scans tracked files via `git grep`, run by `ExistingPowerShellGateTests`. |
| #6 Requirement triage | Review / process | Human judgment. |
| #7 Self-learning / record reusable knowledge | Review / process | Human process. |
| #8 Architecture-first, get consent | Review / process | Approval process before risky changes. |
| #9 Patch hooks report only verified writes | dotnet test + runtime | Existing patch-contract tests and adapter contract tests; this is behavioral, not a syntax gate. |
| #10 Prefer `using` / aliases over fully qualified names | **dotnet test** | New Roslyn gate: `FullyQualifiedNameGateTests` in `tests/CasualtiesUnknownOnline.NormativeGates.Tests`. |
| #11 Attribute/reflection registration for large families | Review / process | Design preference; no reliable syntax gate without false positives. |
| #12 Reuse native game UI | Review / process | Acceptance-readiness audit; explicitly a human acceptance decision. |

## Quality and delivery rules

| Rule | Automation status | Gate / evidence |
|---|---|---|
| No self-assumption; every claim needs evidence | Review / process | Human/process; reflected in self-check fact sheets. |
| Root cause over patch stacking | Review / process | Human architecture review. |
| Line-count / architecture gate escapes are real responsibility splits | dotnet test (script wrapper) + PowerShell gate | `tools/check-architecture.ps1` + `docs/architecture-debt.json`, run by `ExistingPowerShellGateTests`. |
| Red→green hard gate | Review / process | Process rule; deliverable evidence is the failing-test commit. |
| Core + edge/failure test coverage | dotnet test | Existing xUnit suite; coverage breadth is a review/size question. |
| Every key path observable | Review / process | Logging discipline; not a single source-shape gate. |
| Acceptance-readiness audit | Review / process | `docs/evidence/delivery-checklist.md`. |
| User-found issues are hard blockers | Review / process | Backlog/human process. |
| Independent adversarial self-check | Review / process | Human/process. |
| Delivery checklist | dotnet test (script wrapper) + PowerShell gate | `tools/check-delivery.ps1` + `docs/evidence/delivery-checklist.md`, run by `ExistingPowerShellGateTests`. |
| Deployment/artifact verification | PowerShell + process | `tools/deploy.ps1` and deployment hash/file check. |

## Existing tools checks

| Tool | What it checks | Status |
|---|---|---|
| `tools/check-architecture.ps1` | One type/file, logical line count, bool-flag debt, plus the Phase E guard suite below. | dotnet test (script wrapper) + PowerShell gate; run by `ExistingPowerShellGateTests`. |
| `tools/check-gamestate-isolation.ps1` | GameState project isolation from runtime/game/network dependencies. | dotnet test (script wrapper) + PowerShell gate; part of `check-architecture` and also run by the wrapper. |
| `tools/check-item-authority.ps1` | Legacy item projection tables only mutated by their owners. | dotnet test (script wrapper) + PowerShell gate; part of `check-architecture` and also run by the wrapper. |
| `tools/check-no-legacy.ps1` | No removed dual-architecture markers in production source. | dotnet test (script wrapper) + PowerShell gate; currently text-based and is part of `check-architecture.ps1`. |
| `tools/check-command-authority.ps1` | Every `GameCommand` carries an `AuthorityKind` policy. | dotnet test (script wrapper) + PowerShell gate; part of `check-architecture` and also run by the wrapper. |
| `tools/check-kernel-shape.ps1` | No string-keyed dictionaries / `Hashtable` kernel state. | dotnet test (script wrapper) + PowerShell gate; part of `check-architecture` and also run by the wrapper. |
| `tools/check-event-replay.ps1` | Event-replay matrix completeness; also guarded by `ReplayMatrixDataTests`. | dotnet test (script wrapper) + PowerShell gate + dotnet test data integrity. |
| `tools/check-entity-event-dispatch.ps1` | Entity event dispatch matrix. | dotnet test (script wrapper) + PowerShell gate. |
| `tools/check-no-absolute-paths.ps1` | Tracked-file absolute machine paths. | dotnet test (script wrapper) + PowerShell gate; repo-wide and applies to non-C# files too. |
| `tools/check-delivery.ps1` | Delivery checklist/forbidden-box integrity. | dotnet test (script wrapper) + PowerShell gate. |

## Why the FQ-name rule became a unit-test gate

The rule is a pure C# source-shape convention and is best served by a Roslyn
syntax tree rather than textual scanning. The new gate:

- parses every `src/` and `tests/` C# file with official Roslyn APIs
  (`Microsoft.CodeAnalysis.CSharp`);
- reports type names and static/type member accesses that are fully qualified
  by a known namespace root when a `using`/alias is the natural cleaner form;
- allows the documented exceptions: namespace declarations, using directives,
  string literals/HotRepl strings, and enclosing-type member-name collisions
  (for example `System.IO.Path` inside a class that declares a method named
  `Path`).

The gate lives in `tests/CasualtiesUnknownOnline.NormativeGates.Tests` and is
therefore part of the standard `dotnet test` run, not an optional PowerShell
check.

## Why the remaining PowerShell gates are wrapped, not rewritten

The remaining `tools/check-*.ps1` scripts are not all pure C# syntax rules:

- some are repo-wide and cover non-C# files too (`check-no-absolute-paths`);
- some cover project/process state (`check-delivery`, event/entity data matrices);
- some are deliberately textual/scriptable checks with history and edge-case
  behavior already validated (`check-architecture` family).

Rewriting all of them as C# would duplicate the existing implementations and
risk two divergent gates. Instead, `ExistingPowerShellGateTests` invokes every
`tools/check-*.ps1` from the normal `dotnet test` run. The scripts stay the
single canonical implementation, but they are no longer optional: a failed
PowerShell gate now fails the test suite too.

## Related

- `AGENTS.md` Engineering Conventions #10
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/FullyQualifiedNameGate.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/FullyQualifiedNameGateTests.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ExistingPowerShellGateTests.cs`
- `docs/evidence/delivery-checklist.md`
