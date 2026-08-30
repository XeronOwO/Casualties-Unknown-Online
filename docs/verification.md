# Verification and Evidence

This is the evidence layer for the current architecture. Every key mechanism
should be traceable to a source path, a test, or a self-check fact sheet. This page
is the entry point for that evidence chain.

## Current baseline

| Item | Value |
|---|---|
| Test suite | **1791 passed** (Phase E closure baseline; see `docs/tech-decisions.md` #158) |
| Build | `dotnet build` 0 warnings / 0 errors |
| Format | Tracked-source `dotnet format` is clean; `--verify-no-changes` currently reports the generated `obj/Debug/net48/MyPluginInfo.cs`, so the documented baseline is “tracked sources clean” |
| Architecture | `tools/check-architecture.ps1` strict mode passes, including Phase E guards |
| Event/entity/delivery gates | `tools/check-event-replay.ps1`, `tools/check-entity-event-dispatch.ps1`, `tools/check-delivery.ps1` pass |

## Gates

Run the canonical commands from [`AGENTS.md`](../AGENTS.md) before committing code
changes. The architecture-gate guard suite is:

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
records, not open-work status. Use [`docs/selfchecks/MANIFEST.md`](selfchecks/MANIFEST.md)
to separate current from historical before citing. Key architecture-evolution self-checks:

- Phase A–C: historical phase evidence, superseded by later phases:
  `phase-a-kernel-foundation-selfcheck.md`,
  `phase-b-item-authority-selfcheck.md`,
  `phase-c-protocol-core-selfcheck.md`
- Phase D: `phase-d-full-domain-migration-selfcheck.md`
- Phase E: `phase-e-legacy-inventory-selfcheck.md`

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

`docs/delivery-checklist.md` is the project's delivery-quality gate and is paired
with `tools/check-delivery.ps1`. Follow it for each delivery cycle; check boxes one
at a time.

## Related pages

- Architecture: [architecture-evolution/current-architecture.md](architecture-evolution/current-architecture.md)
- Domain ownership: [architecture-evolution/domains.md](architecture-evolution/domains.md)
- Protocol/data flow: [architecture-evolution/protocol.md](architecture-evolution/protocol.md)
- Decision log: [tech-decisions.md](tech-decisions.md)
- Open work: [backlog.md](backlog.md)
