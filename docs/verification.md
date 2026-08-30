# Verification and Evidence

This is the evidence layer for the current architecture. Every key mechanism
should be traceable to a source path, a test, or a self-check fact sheet. This page
is the entry point for that evidence chain.

## Current baseline

| Item | Value |
|---|---|
| Test suite | **1791 passed** (Phase E closure baseline; see `docs/tech-decisions.md` #158) |
| Build | `dotnet build` 0 warnings / 0 errors |
| Format | `dotnet format` clean |
| Architecture | `tools/check-architecture.ps1` strict mode passes, including Phase E guards |
| Event/entity/delivery gates | `tools/check-event-replay.ps1`, `tools/check-entity-event-dispatch.ps1`, `tools/check-delivery.ps1` pass |

## Gates

Run before committing code changes:

```powershell
dotnet build CasualtiesUnknownOnline.slnx
dotnet test CasualtiesUnknownOnline.slnx
dotnet format CasualtiesUnknownOnline.slnx
powershell -File tools/check-architecture.ps1
powershell -File tools/check-event-replay.ps1
powershell -File tools/check-entity-event-dispatch.ps1
powershell -File tools/check-delivery.ps1
```

`tools/check-architecture.ps1` includes the strict structural checks plus the
Phase E guard suite:

- `tools/check-gamestate-isolation.ps1` — GameState project isolation
- `tools/check-item-authority.ps1` — item projection ownership
- `tools/check-no-legacy.ps1` — no `Shadow`/`Legacy`/`Compat`/`Dual`/removed marker
- `tools/check-command-authority.ps1` — every `GameCommand` declares authority
- `tools/check-kernel-shape.ps1` — no string-keyed/Hashtable kernel state

Pure documentation-only changes skip build/test/gates; run `git diff --check` and
review the diff. If a documentation change accompanies code, run the full gates.

## Evidence chain

For a mechanism, prefer this order:

1. **Source** — a `src/...` file path (and line when available) proving the
   implementation.
2. **Behavior test** — a test name in `dotnet test` proving the runtime behavior.
3. **Replay/simulation** — a `.replay` trace or network-simulation test proving
   convergence under delay/reorder/loss.
4. **Self-check fact sheet** — a `docs/selfchecks/<topic>-selfcheck.md` recording
   the delivery cycle, verification design, and evidence.

## Self-checks

`docs/selfchecks/` contains per-delivery fact sheets. They are historical audit
records, not open-work status. Key architecture-evolution self-checks:

- Phase A: `docs/selfchecks/phase-a-kernel-foundation-selfcheck.md`
- Phase B: `docs/selfchecks/phase-b-item-authority-selfcheck.md`
- Phase C: `docs/selfchecks/phase-c-protocol-core-selfcheck.md`
- Phase D: `docs/selfchecks/phase-d-full-domain-migration-selfcheck.md`
- Phase E: `docs/selfchecks/phase-e-legacy-inventory-selfcheck.md`

Domain/feature fact sheets are named by mechanism (for example
`carry-interaction-selfcheck.md`, `cross-player-item-use-selfcheck.md`,
`fluid-presentation-selfcheck.md`).

## Replay and simulation

The replay archive lives under `tests/.../Replays/*.replay` (one step per line on a
monotonic timeline). `ReplayParser`, `ReplayRunner`, and `ItemSimWorld` drive the
same shared 3-node world for item/entity/fluid scenarios; `tools/compare-itemtrace.ps1`
compares a real log against a replay `SimTrace`. These are the strongest evidence
that a fix preserved user-observable semantics.

## Delivery checklist

`docs/delivery-checklist.md` is the project's delivery-quality gate and is paired
with `tools/check-delivery.ps1`. Follow it for each delivery cycle; check boxes one
at a time.

## Related pages

- Architecture: [architecture-evolution/current-architecture.md](architecture-evolution/current-architecture.md)
- Domain ownership: [architecture-evolution/domains.md](architecture-evolution/domains.md)
- Protocol/data flow: [architecture-evolution/protocol.md](architecture-evolution/protocol.md)
- Decision log: [tech-decisions.md](tech-decisions.md)
- Open work: [backlog.md](backlog.md)
