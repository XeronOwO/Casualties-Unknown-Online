# CUO Active Decision Register

This is the **normative decision register**: the decisions that still constrain
current architecture, protocol, API, and development practice.

Historical delivery records, feature-slice logs, and Phase A–E daily sub-steps are
not here:

- Historical delivery archive: [`tech-decisions-archive.md`](tech-decisions-archive.md)
- Phase A–E evolution record: [`architecture-evolution/phase-decisions.md`](architecture-evolution/phase-decisions.md)
- Full traceability index: [`tech-decisions-index.md`](tech-decisions-index.md)

## Binding current decisions

| # | Decision | Canonical doc |
|---|---|---|
| 1 | BepInEx 5 / net48 / HarmonyX / protobuf-net / Microsoft.Extensions stack. | `AGENTS.md`, `docs/operations.md` |
| 2 | Pure star topology, host-authoritative, no guest-guest traffic; reliability follows “can the loss self-heal”. | `docs/architecture-evolution/protocol.md` |
| 3 | Session owns its state; narrow `HandlerContext` capability interfaces; no broad DI/global mutable state. | `AGENTS.md`, `docs/architecture-evolution/current-architecture.md` |
| 6 | Perfect-match Harmony patch contracts must be tested against copied game assemblies. | `AGENTS.md`, tools |
| 9 | Mod API binding contract lives in `docs/mod-api.md`; opaque mod messages, fail-closed manifest. | `docs/mod-api.md` |
| 19 | Mod permissions, host commands, dependency ordering, strict SemVer. | `docs/mod-api.md` |
| 25 | BepInEx `ConfigFile` → `IOptionsMonitor`; logging levels; state-stream cadence. | `AGENTS.md`, `docs/mod-api.md` |
| 37 | Mod state saves are host-persistent and versioned. | `docs/mod-api.md` |
| 38 | Mod content registration is a read-only framework registry. | `docs/mod-api.md` |
| 46 | `ReadGameState` is a read-only player-character projection. | `docs/mod-api.md` |
| 49 | Mod entity spawn is permission-gated native prefab replication. | `docs/mod-api.md` |
| 50 | `AccessNativeApi` is a curated read-only registry, never open reflection. | `docs/mod-api.md` |
| 63 | NetMsg direction registry is fail-closed; unregistered/incorrect-direction ids are rejected. | `docs/architecture-evolution/protocol.md` |
| 65 | Partial-aware architecture gate + debt ledger. | `AGENTS.md`, `tools/check-architecture.ps1` |
| 73 | HandlerContext per-domain narrowing via capability interfaces. | `docs/architecture-evolution/current-architecture.md` |
| 75 | GameAdapter depends on narrow control interfaces, not concrete session services. | `AGENTS.md`, `docs/architecture-evolution/domains.md` |
| 82 | IP-direct TCP transport is a supported non-Steam mode. | `docs/selfchecks/ip-direct-selfcheck.md` |

## Active kernel/protocol decisions

| # | Decision | Canonical doc |
|---|---|---|
| 128 | Four-envelope protocol, checkpoint+journal join, checkpoint-only save, kernel wire mapping. | `docs/architecture-evolution/protocol.md` |
| 129 | High-frequency player/enemy streams ride `StateStreamEnvelope` over `KernelEnvelope`. | `docs/architecture-evolution/protocol.md` |
| 137 | Pre-release protocol numbering was reset; `ProtocolVersion.Current = 1`; future behavioral wire changes bump it. | `docs/architecture-evolution/protocol.md`, `docs/mod-api.md` |
| 152 | Player durable skills are kernel-owned in `PlayerState`. | `docs/architecture-evolution/domains.md` |
| 153 | Player kernel identity is ensured when entity sync starts. | `docs/architecture-evolution/domains.md` |
| 154 | Cross-player take/heal/use/carry are `HostValidatedNoPrediction`; push is `PresentationOnly`. | `docs/architecture-evolution/domains.md` |
| 155 | Carry relation requires a live carrier; kernel invariant. | `docs/architecture-evolution/domains.md` |
| 156 | Player/item ownership consistency and death preservation are kernel invariants. | `docs/architecture-evolution/domains.md` |
| 157 | Generic Prediction Runtime is future work; current cross-player operations are not client-predicted. | `docs/architecture-evolution/domains.md`, `docs/backlog.md` |
| 158 | Kernel reset centralized in `KernelProtocolService`; no-legacy/command-authority/kernel-shape guards are active. | `docs/architecture-evolution/architecture-guards.md` |

## Reference rules

- Older decision numbers remain traceable through
  [`tech-decisions-index.md`](tech-decisions-index.md).
- Delivery-cycle detail and per-feature evidence live in
  [`tech-decisions-archive.md`](tech-decisions-archive.md) and
  [`docs/selfchecks/`](selfchecks/).
