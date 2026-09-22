# CUO Documentation Map

This is the semantic index for the CUO documentation. It is organized by reader
intent, not by file type. If you only read one page, start here and follow the
reading path.

## Three layers

The tree carries three layers, and every documentation directory declares its layer
through its index:

| Layer | Index | Language | Reader |
|---|---|---|---|
| Agent instruction | [`docs/AGENTS.md`](AGENTS.md) and the root [`AGENTS.md`](../AGENTS.md) | English only | an agent doing the work |
| Reference / evidence | this map, plus each layer's own `README.md` | English only | a contributor who needs the full detail |
| Human guide | [`guide/README.md`](guide/README.md) + [`guide/README.zh.md`](guide/README.zh.md), [`developer/README.md`](developer/README.md) + [`developer/README.zh.md`](developer/README.zh.md) | paired English + Chinese | a player, or a developer meeting the product |

The layer rule and the pairing contract are [`i18n/README.md`](i18n/README.md); the
renderings are [`i18n/terminology.md`](i18n/terminology.md). Only the two guide levels
are paired — every other document in this tree is English only. 中文读者从这里开始:
[`guide/README.zh.md`](guide/README.zh.md).

## Reading path

```
What CUO is, and playing it (level 1, paired)
  → guide/README.md            (中文: guide/README.zh.md)
  → guide/getting-started.md
  → guide/playing.md
  → guide/limitations.md

Building on CUO (level 2, paired)
  → developer/README.md        (中文: developer/README.zh.md)
  → developer/overview.md
  → developer/layers.md
  → developer/protocol.md
  → developer/sync-model.md

Why / vision
  → README.md (repo root)
  → AGENTS.md (repo root)

Current architecture (level 3, English only)
  → architecture/README.md
  → architecture/current.md
  → architecture/domains.md
  → architecture/protocol.md

Domain reference (game mechanics & feature matrices)
  → features/items.md, features/entities.md, features/enemies.md
  → api/mod-api.md

Verification / evidence
  → evidence/verification.md
  → evidence/selfchecks/
  → evidence/delivery-checklist.md

Decisions / history / future
  → decisions/active.md
  → architecture/evolution/phase-*.md
  → backlog/README.md
```

## 1. Project / Vision

| Document | Layer |
|---|---|
| [`README.md`](../README.md) | Project purpose, status, build, documentation links |
| [`AGENTS.md`](../AGENTS.md) | Binding conventions, build gates, architecture rules |

## 2. Architecture / Domain Model

| Document | Layer |
|---|---|
| [`architecture-evolution/README.md`](architecture/README.md) | Entry point to the active architecture and evolution history |
| [`architecture-evolution/current-architecture.md`](architecture/current.md) | Current typed deterministic kernel: core flow, project structure, non-goals |
| [`architecture-evolution/domains.md`](architecture/domains.md) | Domain ownership: what is kernel state, what is a projection |
| [`architecture-evolution/protocol.md`](architecture/protocol.md) | Four-envelope protocol, join, state stream, save, command rejection |
| [`architecture-evolution/architecture-guards.md`](architecture/guards.md) | Active kernel-shape/authority/no-legacy guard list |
| [`architecture-evolution/save-archive-format.md`](architecture/save-archive-format.md) | CUO world archive: package format, world repository layout, restore/repair and backup policy |
| [`architecture-evolution/glossary.md`](architecture/glossary.md) | Stable vocabulary for kernel/domain/protocol terms |
| [`architecture.md`](history/architecture-blueprint.md) | **Historical pre-kernel blueprint** — retained for context, not the current design |
| [`game-internals.md`](features/game-internals.md) | Reverse-engineering findings: scenes, Body, world generation, clone chain |

## 3. Domain Mechanisms / Feature Matrices

| Document | Layer |
|---|---|
| [`item-features.md`](features/items.md) + `item-features-matrix.csv` | Canonical item trait matrix and sync status |
| [`entity-features.md`](features/entities.md) + `entity-features-matrix.csv` | Canonical entity/trap mechanism matrix and sync status |
| [`enemy-sync.md`](features/enemies.md) | Enemy mechanics and sync design |
| [`mod-api.md`](api/mod-api.md) | Mod API lifecycle, permissions, host commands, state, UI, content |
| [`advanced-modification-policy.md`](api/advanced-modification-policy.md) | Contract vs implementation, stability levels, Harmony policy, the reviewed public-surface baseline (`api/abstractions-api-baseline.txt`) |

## 4. Verification / Evidence

| Document | Layer |
|---|---|
| [`verification.md`](evidence/verification.md) | Evidence chain: gates, test baseline, replay/simulation, self-checks |
| [`test-parallelization.md`](evidence/test-parallelization.md) | Test suite parallel model, measured runtime, parallel-safety inventory, re-measurement method |
| [`selfchecks/`](evidence/selfchecks/) | Per-delivery fact sheets (historical audit records) |
| [`delivery-checklist.md`](evidence/delivery-checklist.md) | Delivery quality gate checklist |
| [`normative-gates.md`](evidence/normative-gates.md) | Normative rule → automation-gate inventory, including the Roslyn fully-qualified-name gate |
| [`agent-reference.md`](development/agent-reference.md) | Binding detail moved out of `AGENTS.md` to stay inside the harness instruction budget: repository layout, architecture & sync rules, known pitfalls |
| [`game-update-runbook.md`](development/game-update-runbook.md) | The update-day flow: snapshot → classified diff → contract tests → offline replay → compatibility verdict |
| [`review-prompt.md`](development/review-prompt.md) | Prompt template for the independent adversarial review that runs before every commit (risk tiers, what to attack, how to report) |
| [`selfchecks/tooling/simtrace-diff-selfcheck.md`](evidence/selfchecks/tooling/simtrace-diff-selfcheck.md) | Real-log vs replay diff automation |
| [`selfchecks/tooling/game-update-contract-toolchain-selfcheck.md`](evidence/selfchecks/tooling/game-update-contract-toolchain-selfcheck.md) | Game-update contract toolchain: snapshot, classified diff, runbook |

## 5. Decision Log

| Document | Layer |
|---|---|
| [`tech-decisions.md`](decisions/active.md) | **Active decision register** — the normative decisions that still apply today |
| [`tech-decisions-archive.md`](decisions/archive.md) | Compressed historical delivery archive |
| [`architecture-evolution/phase-decisions.md`](architecture/phase-decisions.md) | Compressed Phase A–E evolution record |
| [`tech-decisions-index.md`](decisions/index.md) | Numeric traceability index of all original decision numbers |

## 6. Operations / Tooling / Deployment

| Document | Layer |
|---|---|
| [`operations.md`](operations/README.md) | Shared operations layer: build/gates, deployment, git discipline, local tools |
| [`AGENTS.md`](../AGENTS.md) | Binding conventions and gate commands |
| `AGENTS.local.md` (gitignored) | Machine-specific paths, sandboxes, HotRepl — never commit |
| `tools/` | Architecture gates, feature-matrix scripts, replay helpers, deploy script |

## 7. Evolution / History

| Document | Layer |
|---|---|
| [`architecture-evolution/README.md`](architecture/README.md) | Active architecture + completed phase history |
| [`architecture-evolution/status.md`](architecture/evolution/status.md) | Completed phase tracker |
| [`architecture-evolution/current-architecture.md`](architecture/current.md) | Current design reference |
| [`architecture-evolution/migration-roadmap.md`](architecture/evolution/migration-roadmap.md) | Completed migration route (historical) |
| [`architecture-evolution/phase-a-shadow-kernel.md`](architecture/evolution/phase-a-shadow-kernel.md) | Phase A history |
| [`architecture-evolution/phase-b-items-authority.md`](architecture/evolution/phase-b-items-authority.md) | Phase B history |
| [`architecture-evolution/phase-c-protocol-save-switch.md`](architecture/evolution/phase-c-protocol-save-switch.md) | Phase C history |
| [`architecture-evolution/phase-d-full-domain-migration.md`](architecture/evolution/phase-d-full-domain-migration.md) | Phase D history |
| [`architecture-evolution/phase-e-delete-dual-architecture.md`](architecture/evolution/phase-e-delete-dual-architecture.md) | Phase E history |
| [`architecture-evolution/session-workflow.md`](architecture/evolution/session-workflow.md) | Historical phase-session workflow |
| [`architecture-evolution/templates/phase-session.md`](architecture/evolution/templates/phase-session.md) | Historical phase-session template |
| [`architecture.md`](history/architecture-blueprint.md) | Pre-kernel blueprint history |

## 8. Backlog / Future

| Document | Layer |
|---|---|
| [`backlog/README.md`](backlog/README.md) | DevOps-style issue/requirement backlog, one ticket per file, grouped by status |

## Historical audits / plans

These are point-in-time analyses, retained because they contain useful evidence or
decision context:

[- `lobby-refactor-plan.md`](history/audits/lobby-refactor-plan.md)
[- `exploration-2026-08-23.md`](history/audits/exploration-2026-08-23.md)
[- `krokmp-notes.md`](history/audits/krokmp-notes.md)
[- `runtime-supply-refresh-audit.md`](history/audits/runtime-supply-refresh-audit.md)
[- `worldgen-determinism-audit.md`](history/audits/worldgen-determinism-audit.md)
