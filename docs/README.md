# CUO Documentation Map

This is the semantic index for the CUO documentation. It is organized by reader
intent, not by file type. If you only read one page, start here and follow the
reading path.

## Reading path

```
Why / vision
  → README.md (repo root)
  → AGENTS.md (repo root)

Current architecture
  → architecture-evolution/README.md
  → architecture-evolution/current-architecture.md
  → architecture-evolution/domains.md
  → architecture-evolution/protocol.md

Domain reference (game mechanics & feature matrices)
  → item-features.md, entity-features.md, enemy-sync.md
  → mod-api.md

Verification / evidence
  → verification.md
  → selfchecks/
  → delivery-checklist.md

Decisions / history / future
  → tech-decisions.md
  → architecture-evolution/phase-*.md
  → backlog.md
```

## 1. Project / Vision

| Document | Layer |
|---|---|
| [`README.md`](../README.md) | Project purpose, status, build, documentation links |
| [`AGENTS.md`](../AGENTS.md) | Binding conventions, build gates, architecture rules |
| [`mod-api.md`](mod-api.md) | Binding public Mod API contract (the only API mods may use) |

## 2. Architecture / Domain Model

| Document | Layer |
|---|---|
| [`architecture-evolution/README.md`](architecture-evolution/README.md) | Entry point to the active architecture and evolution history |
| [`architecture-evolution/current-architecture.md`](architecture-evolution/current-architecture.md) | Current typed deterministic kernel: core flow, project structure, non-goals |
| [`architecture-evolution/domains.md`](architecture-evolution/domains.md) | Domain ownership: what is kernel state, what is a projection |
| [`architecture-evolution/protocol.md`](architecture-evolution/protocol.md) | Four-envelope protocol, join, state stream, save, command rejection |
| [`architecture.md`](architecture.md) | **Historical pre-kernel blueprint** — retained for context, not the current design |
| [`enemy-sync.md`](enemy-sync.md) | Enemy simulation/sync design and runtime component map |
| [`game-internals.md`](game-internals.md) | Reverse-engineering findings: scenes, Body, world generation, clone chain |

## 3. Domain Mechanisms / Feature Matrices

| Document | Layer |
|---|---|
| [`item-features.md`](item-features.md) + `item-features-matrix.csv` | Canonical item trait matrix and sync status |
| [`entity-features.md`](entity-features.md) + `entity-features-matrix.csv` | Canonical entity/trap mechanism matrix and sync status |
| [`enemy-sync.md`](enemy-sync.md) | Enemy mechanics and sync design |
| [`mod-api.md`](mod-api.md) | Mod API lifecycle, permissions, host commands, state, UI, content |

## 4. Verification / Evidence

| Document | Layer |
|---|---|
| [`verification.md`](verification.md) | Evidence chain: gates, test baseline, replay/simulation, self-checks |
| [`selfchecks/`](selfchecks/) | Per-delivery fact sheets (historical audit records) |
| [`delivery-checklist.md`](delivery-checklist.md) | Delivery quality gate checklist |
| [`runtime-supply-refresh-audit.md`](runtime-supply-refresh-audit.md) | Runtime item-spawn surface sweep |
| [`worldgen-determinism-audit.md`](worldgen-determinism-audit.md) | World-generation random-consumer determinism audit |
| [`simtrace-diff-selfcheck.md`](selfchecks/simtrace-diff-selfcheck.md) | Real-log vs replay diff automation |

## 5. Decision Log

| Document | Layer |
|---|---|
| [`tech-decisions.md`](tech-decisions.md) | Binding technical decisions with reasoning and traceability |

## 6. Operations / Tooling / Deployment

| Document | Layer |
|---|---|
| [`AGENTS.md`](../AGENTS.md) | General build/deploy guidance and gate commands |
| `AGENTS.local.md` (gitignored) | Machine-specific paths, sandboxes, HotRepl — never commit |
| `tools/` | Architecture gates, feature-matrix scripts, replay helpers, deploy script |

## 7. Evolution / History

| Document | Layer |
|---|---|
| [`architecture-evolution/README.md`](architecture-evolution/README.md) | Active architecture + completed phase history |
| [`architecture-evolution/status.md`](architecture-evolution/status.md) | Completed phase tracker |
| [`architecture-evolution/current-architecture.md`](architecture-evolution/current-architecture.md) | Current design reference |
| [`architecture-evolution/migration-roadmap.md`](architecture-evolution/migration-roadmap.md) | Completed migration route (historical) |
| [`architecture-evolution/phase-a-shadow-kernel.md`](architecture-evolution/phase-a-shadow-kernel.md) | Phase A history |
| [`architecture-evolution/phase-b-items-authority.md`](architecture-evolution/phase-b-items-authority.md) | Phase B history |
| [`architecture-evolution/phase-c-protocol-save-switch.md`](architecture-evolution/phase-c-protocol-save-switch.md) | Phase C history |
| [`architecture-evolution/phase-d-full-domain-migration.md`](architecture-evolution/phase-d-full-domain-migration.md) | Phase D history |
| [`architecture-evolution/phase-e-delete-dual-architecture.md`](architecture-evolution/phase-e-delete-dual-architecture.md) | Phase E history |
| [`architecture.md`](architecture.md) | Pre-kernel blueprint history |

## 8. Backlog / Future

| Document | Layer |
|---|---|
| [`backlog.md`](backlog.md) | Open bug, open work, open decisions, future/low priority |

## Historical audits / plans

These are point-in-time analyses, retained because they contain useful evidence or
decision context:

- [`lobby-refactor-plan.md`](lobby-refactor-plan.md)
- [`exploration-2026-08-23.md`](exploration-2026-08-23.md)
- [`krokmp-notes.md`](krokmp-notes.md)
- [`runtime-supply-refresh-audit.md`](runtime-supply-refresh-audit.md)
- [`worldgen-determinism-audit.md`](worldgen-determinism-audit.md)
