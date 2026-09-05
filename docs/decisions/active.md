# CUO Active Decision Register

This is the **normative decision register**: the decisions that still constrain
current architecture, protocol, API, and development practice.

Historical delivery records, feature-slice logs, and Phase A–E daily sub-steps are
not here:

[- Historical delivery archive: `tech-decisions-archive.md`](archive.md)
[- Phase A–E evolution record: `architecture-evolution/phase-decisions.md`](../architecture/phase-decisions.md)
[- Full traceability index: `tech-decisions-index.md`](index.md)

## Binding current decisions

| # | Decision | Canonical doc |
|---|---|---|
| 1 | BepInEx 5 / net48 / HarmonyX / protobuf-net / Microsoft.Extensions stack. | `AGENTS.md`, `docs/operations/README.md` |
| 2 | Pure star topology, host-authoritative, no guest-guest traffic; reliability follows “can the loss self-heal”. | `docs/architecture/protocol.md` |
| 3 | Session owns its state; narrow `HandlerContext` capability interfaces; no broad DI/global mutable state. | `AGENTS.md`, `docs/architecture/current.md` |
| 6 | Perfect-match Harmony patch contracts must be tested against copied game assemblies. | `AGENTS.md`, tools |
| 9 | Mod API binding contract lives in `docs/api/mod-api.md`; opaque mod messages, fail-closed manifest. | `docs/api/mod-api.md` |
| 19 | Mod permissions, host commands, dependency ordering, strict SemVer. | `docs/api/mod-api.md` |
| 25 | BepInEx `ConfigFile` → `IOptionsMonitor`; logging levels; state-stream cadence. | `AGENTS.md`, `docs/api/mod-api.md` |
| 37 | Mod state saves are host-persistent and versioned. | `docs/api/mod-api.md` |
| 38 | Mod content registration is a read-only framework registry. | `docs/api/mod-api.md` |
| 46 | `ReadGameState` is a read-only player-character projection. | `docs/api/mod-api.md` |
| 49 | Mod entity spawn is permission-gated native prefab replication. | `docs/api/mod-api.md` |
| 50 | `AccessNativeApi` is a curated read-only registry, never open reflection. | `docs/api/mod-api.md` |
| 63 | NetMsg direction registry is fail-closed; unregistered/incorrect-direction ids are rejected. | `docs/architecture/protocol.md` |
| 65 | Partial-aware architecture gate + debt ledger. | `AGENTS.md`, `tools/check-architecture.ps1` |
| 73 | HandlerContext per-domain narrowing via capability interfaces. | `docs/architecture/current.md` |
| 75 | GameAdapter depends on narrow control interfaces, not concrete session services. | `AGENTS.md`, `docs/architecture/domains.md` |
| 82 | IP-direct TCP transport is a supported non-Steam mode. | `docs/evidence/selfchecks/protocol/ip-direct-selfcheck.md` |

## Active kernel/protocol decisions

| # | Decision | Canonical doc |
|---|---|---|
| 128 | Four-envelope protocol, checkpoint+journal join, checkpoint-only save, kernel wire mapping. | `docs/architecture/protocol.md` |
| 129 | High-frequency player/enemy streams ride `StateStreamEnvelope` over `KernelEnvelope`. | `docs/architecture/protocol.md` |
| 137 | Pre-release protocol numbering was reset; `ProtocolVersion.Current` is bumped on behavioral wire changes (currently 2 after the PantSound `CharacterSoundKind` extension). | `docs/architecture/protocol.md`, `docs/api/mod-api.md` |
| 152 | Player durable skills are kernel-owned in `PlayerState`. | `docs/architecture/domains.md` |
| 153 | Player kernel identity is ensured when entity sync starts. | `docs/architecture/domains.md` |
| 154 | Cross-player take/heal/use/carry are `HostValidatedNoPrediction`; push is `PresentationOnly`. | `docs/architecture/domains.md` |
| 155 | Carry relation requires a live carrier; kernel invariant. | `docs/architecture/domains.md` |
| 156 | Player/item ownership consistency and death preservation are kernel invariants. | `docs/architecture/domains.md` |
| 157 | Generic Prediction Runtime is future work; current cross-player operations are not client-predicted. | `docs/architecture/domains.md`, `docs/backlog/README.md` |
| 158 | Kernel reset centralized in `KernelProtocolService`; no-legacy/command-authority/kernel-shape guards are active. | `docs/architecture/guards.md` |
| 159 | Manual world-time acceleration is cooperative: `Fast`/`SuperFast` never accelerate a shared session while any in-world player is awake; all-unconscious sleep remains the only shared-clock acceleration. | `docs/evidence/selfchecks/world/world-time-selfcheck.md` |
| 160 | Sleep policy: normal and forced sleep remain allowed; shared-clock acceleration is host-authoritative and only applies when every in-world alive player is unconscious; no sleep-gating host rule or new wire field. | `docs/backlog/resolved/sleep-behavior-policy.md` |

## Reference rules

- Older decision numbers remain traceable through
[  `tech-decisions-index.md`](index.md).
- Delivery-cycle detail and per-feature evidence live in
[  `tech-decisions-archive.md`](archive.md) and
  [`[docs/selfchecks/`](../evidence/selfchecks/).
