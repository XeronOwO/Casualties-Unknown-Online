# CUO Documentation Index

Map of this repository's documentation. Start with [architecture.md](architecture.md) for the design, then drill into the domain docs.

## Core design

- [architecture.md](architecture.md) — the full blueprint: overall architecture, technical stack, sync model, game adapter, mod API design, compatibility, saves, specs, pitfalls, and development phases.
- [tech-decisions.md](tech-decisions.md) — the landing log of binding decisions (moved out of the workspace instruction file): reasoning + traceability (commit hashes, protocol versions, `file:line`) per decision.

## Game reverse-engineering

- [game-internals.md](game-internals.md) — scenes & flow, `Body`/input, world generation & saves, the clone/render chain, and the sync-model findings.
- [krokmp-notes.md](krokmp-notes.md) — KrokMP's deployment layout, public API surface, and internals (reference only, never to copy).

## Domain features (reverse-engineered + synced)

- [item-features.md](item-features.md) + `item-features-matrix.csv` — the item feature matrix (battery/liquid/consumable/durability/modeswitch/gun/ammo/…), the crafting operation surfaces, and known state gaps.
- [entity-features.md](entity-features.md) + `entity-features-matrix.csv` — trap/mechanism entity events, the crystal family, trade, fluid, environment, buildings, creatures.
- [enemy-sync.md](enemy-sync.md) — enemy simulation/sync design and the runtime component map.
- [mod-api.md](mod-api.md) — the Phase 4 Mod API contract (the binding reference for mod authors).
- [selfchecks/](selfchecks/) — per-delivery fact sheets (mechanism × change × evidence + verification design). These are historical delivery records, not the current open-work view.

## Audits / plans

- [lobby-refactor-plan.md](lobby-refactor-plan.md) — lobby-domain refactor plan and acceptance evidence.
- [runtime-supply-refresh-audit.md](runtime-supply-refresh-audit.md) — runtime item-spawn surface sweep (which calls create items, repeatability).
- [worldgen-determinism-audit.md](worldgen-determinism-audit.md) — world-generation random-consumer / determinism audit.

## Process & gates

- [delivery-checklist.md](delivery-checklist.md) — the delivery-quality-gate checklist (paired with `tools/check-delivery.ps1`).
- [backlog.md](backlog.md) — open work and future/pending items (landed details live in `tech-decisions.md` and `selfchecks/`).
- `event-replay-matrix.csv` — the per-mechanism replay audit (paired with `tools/check-event-replay.ps1`).
- `tools/compare-itemtrace.ps1` — real-log vs replay SimTrace diff automation (whole-session subsequence matching, gzip logs, leak contract; see `selfchecks/simtrace-diff-selfcheck.md`).

## Conventions

- [`AGENTS.md`](../AGENTS.md) (repo root) — binding conventions, build gates, and the delivery gate.
- [`AGENTS.local.md`](../AGENTS.local.md) (repo root, gitignored) — machine-local notes (paths, sandboxes, deployment, HotRepl).
