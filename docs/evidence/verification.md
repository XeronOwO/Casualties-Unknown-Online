# Verification and Evidence

This is the evidence layer for the current architecture. Every key mechanism
should be traceable to a source path, a test, or a self-check fact sheet. This page
is the entry point for that evidence chain.

## Current baseline

| Item | Value |
|---|---|
| Test suite | **1991 passed** (2026-09-01 item content binding; see `docs/evidence/selfchecks/mod-api/mod-item-content-binding-selfcheck.md`) |
| Build | `dotnet build` 0 warnings / 0 errors |
| Format | Tracked-source `dotnet format` is clean; `--verify-no-changes` currently reports the generated `obj/Debug/net48/MyPluginInfo.cs`, so the documented baseline is “tracked sources clean” |
| Architecture | `tools/check-architecture.ps1` strict mode passes, including Phase E guards |
| Event/entity/delivery gates | `tools/check-event-replay.ps1`, `tools/check-entity-event-dispatch.ps1`, `tools/check-delivery.ps1` pass |

## Gates

Run the canonical commands from [`AGENTS.md`](../../AGENTS.md) before committing code
changes. The normative source-shape gate is now part of `dotnet test`:

- `tests/CasualtiesUnknownOnline.NormativeGates.Tests` — Roslyn syntax-tree gate
  for AGENTS.md #10 (prefer `using`/aliases over fully qualified names), plus
  `ExistingPowerShellGateTests` which runs every `tools/check-*.ps1` gate as
  part of `dotnet test`. The inventory of every normative rule's automation
  status is in [`normative-gates.md`](normative-gates.md).

The architecture-gate guard suite is:

- `tools/check-gamestate-isolation.ps1` — GameState project isolation
- `tools/check-item-authority.ps1` — item projection ownership
- `tools/check-no-legacy.ps1` — no `Shadow`/`Legacy`/`Compat`/`Dual`/removed marker
- `tools/check-command-authority.ps1` — every `GameCommand` declares authority
- `tools/check-kernel-shape.ps1` — no string-keyed/Hashtable kernel state

Pure documentation-only changes skip build/test/gates; run `git diff --check` and
review the diff. If a documentation change accompanies code, run the full gates.

## Kernel and domain test architecture

- **Kernel contracts** — same Command + State + Context produces same Decision;
  Event Reduce is deterministic; Batch atomicity; Operation idempotency; revision
  monotonicity; checkpoint round-trip equivalence.
- **Domain property tests** — generated operation sequences check item unique
  location/acyclic containers/no Terminal resurrection, player death/backpack/drop
  consistency, trap illegal states, damaged/removed entity behavior, and epoch
  isolation.
- **Replay and differential testing** — `.replay` traces drive both the legacy and
  kernel paths and compare only semantic facts, not internal call counts/log text.
- **Adapter contracts** — one native user operation produces exactly one Observation;
  RemoteApply does not echo; projection rebuild does not produce a local Command;
  display proxies do not enter authoritative capture.
- **Golden wire contract tests** — `tests/CasualtiesUnknownOnline.Tests/Protocol/ProtocolCodecTests.cs`
  locks the wire framing/encoding contract.
- **Network simulation** — virtual-time latency/duplication/reordering/loss/
  disconnect/checkpoint/reconnect tests; reliable batches converge, state streams
  converge on the next subsequent state.
- **Test replacement principle** — when a deep-module interface covers behavior,
  delete tests that lock old shallow cooperation order; keep wire golden tests,
  adapter contracts, domain model tests, property tests, and user-observable replay
  tests.

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
[records, not open-work status. Use `docs/evidence/selfchecks/MANIFEST.md`](selfchecks/MANIFEST.md)
to separate current from historical before citing. Key architecture-evolution self-checks:

- Phase A–C: historical phase evidence, superseded by later phases:
  `phase-a-kernel-foundation-selfcheck.md`,
  `phase-b-item-authority-selfcheck.md`,
  `phase-c-protocol-core-selfcheck.md`
- Phase D: `phase-d-full-domain-migration-selfcheck.md`
- Phase E: `phase-e-legacy-inventory-selfcheck.md`

### Current high-value selfchecks

These are the current-evidence seeds most useful for verifying active mechanisms:

| Selfcheck | Domain |
|---|---|
| [`phase-d-full-domain-migration-selfcheck.md`](selfchecks/architecture/phase-d-full-domain-migration-selfcheck.md) | Architecture |
| [`phase-d-high-frequency-stream-unification-selfcheck.md`](selfchecks/architecture/phase-d-high-frequency-stream-unification-selfcheck.md) | Protocol/Architecture |
| [`phase-d-players-shadow-selfcheck.md`](selfchecks/architecture/phase-d-players-shadow-selfcheck.md) | Players |
| [`phase-d-world-entities-shadow-selfcheck.md`](selfchecks/architecture/phase-d-world-entities-shadow-selfcheck.md) | World/Entities |
| [`phase-e-legacy-inventory-selfcheck.md`](selfchecks/architecture/phase-e-legacy-inventory-selfcheck.md) | Architecture |
| [`netmsg-registry-selfcheck.md`](selfchecks/protocol/netmsg-registry-selfcheck.md) | Protocol |
| [`world-entry-completion-selfcheck.md`](selfchecks/world/world-entry-completion-selfcheck.md) | Protocol/World |
| [`item-keyframe-state-selfcheck.md`](selfchecks/items/item-keyframe-state-selfcheck.md) | Items |
| [`container-content-sync-selfcheck.md`](selfchecks/items/container-content-sync-selfcheck.md) | Items |
| [`custom-item-data-state-selfcheck.md`](selfchecks/items/custom-item-data-state-selfcheck.md) | Items |
| [`remote-backpack-container-take-selfcheck.md`](selfchecks/items/remote-backpack-container-take-selfcheck.md) | Players/Items |
| [`remote-container-destroy-authority-selfcheck.md`](selfchecks/items/remote-container-destroy-authority-selfcheck.md) | Items |
| [`respawn-rules-selfcheck.md`](selfchecks/players/respawn-rules-selfcheck.md) | Players |
| [`trader-recruit-selfcheck.md`](selfchecks/enemies/trader-recruit-selfcheck.md) | Players |
| [`chat-selfcheck.md`](selfchecks/ui/chat-selfcheck.md) | UI/Protocol |
| [`host-ban-selfcheck.md`](selfchecks/session/host-ban-selfcheck.md) | Players/Protocol |
| [`ip-direct-selfcheck.md`](selfchecks/protocol/ip-direct-selfcheck.md) | Protocol |
| [`partial-aware-gate-selfcheck.md`](selfchecks/tooling/partial-aware-gate-selfcheck.md) | Architecture/Tooling |
| [`simtrace-diff-selfcheck.md`](selfchecks/tooling/simtrace-diff-selfcheck.md) | Tooling |
| latest architecture-split sheets (`world-service-split`, `item-service-split`, `mod-service-split`) | Architecture |
| [`tutorial-claw-stream-selfcheck.md`](selfchecks/world/tutorial-claw-stream-selfcheck.md) | World/Entities |

Domain/feature fact sheets are named by mechanism (for example
`carry-interaction-selfcheck.md`, `cross-player-item-use-selfcheck.md`,
`fluid-presentation-selfcheck.md`).

## Replay and simulation

The replay archive lives under `tests/.../Replays/*.replay` (one step per line on a
monotonic timeline). `ReplayRunner` dispatches by domain:

- `ItemSimWorld` — item-domain scenarios;
- `EntityEventSimWorld` — entity/fluid scenarios;
- `BlockBreakReplayWorld` — block-break scenarios;
- `TradeReplayWorld` — trade scenarios.

The legacy-vs-kernel semantic diff (`ItemSimWorld.CompareKernel`) is currently
item-domain only; entity/fluid/block-break/trade replays do not perform that
legacy-vs-kernel comparison. `tools/compare-itemtrace.ps1` compares a real log
against a replay `SimTrace`. These are the strongest evidence that a fix preserved
user-observable semantics.

## Delivery checklist

`docs/evidence/delivery-checklist.md` is the project's delivery-quality gate and is paired
with `tools/check-delivery.ps1`. Follow it for each delivery cycle; check boxes one
at a time.

## Related pages

[- Architecture: architecture-evolution/current-architecture.md](../architecture/current.md)
[- Domain ownership: architecture-evolution/domains.md](../architecture/domains.md)
[- Protocol/data flow: architecture-evolution/protocol.md](../architecture/protocol.md)
[- Decision log: tech-decisions.md](../decisions/active.md)
[- Open work: backlog.md](../backlog/README.md)
