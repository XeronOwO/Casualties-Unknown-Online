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
| 65 | Partial-aware architecture gate + debt ledger. | `AGENTS.md`, `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` |
| 73 | HandlerContext per-domain narrowing via capability interfaces. | `docs/architecture/current.md` |
| 75 | GameAdapter depends on narrow control interfaces, not concrete session services. | `AGENTS.md`, `docs/architecture/domains.md` |
| 82 | IP-direct TCP transport is a supported non-Steam mode. | `docs/evidence/selfchecks/protocol/ip-direct-selfcheck.md` |

## Active kernel/protocol decisions

| # | Decision | Canonical doc |
|---|---|---|
| 128 | Four-envelope protocol, checkpoint+journal join, checkpoint-only save, kernel wire mapping. | `docs/architecture/protocol.md` |
| 129 | High-frequency player/enemy streams ride `StateStreamEnvelope` over `KernelEnvelope`. | `docs/architecture/protocol.md` |
| 137 | Pre-release protocol numbering was reset; `ProtocolVersion.Current` is bumped on behavioral wire changes (currently 18 after the runtime-entity creation token, the snapshot's animal acknowledgement key list, and the enemy/trap-layout backfill creation keys). | `docs/architecture/protocol.md`, `docs/api/mod-api.md` |
| 152 | Player durable skills are kernel-owned in `PlayerState`. | `docs/architecture/domains.md` |
| 153 | Player kernel identity is ensured when entity sync starts. | `docs/architecture/domains.md` |
| 154 | Cross-player take/heal/use/carry are `HostValidatedNoPrediction`; push is `PresentationOnly`. | `docs/architecture/domains.md` |
| 155 | Carry relation requires a live carrier; kernel invariant. | `docs/architecture/domains.md` |
| 156 | Player/item ownership consistency and death preservation are kernel invariants. | `docs/architecture/domains.md` |
| 157 | Generic Prediction Runtime is future work; current cross-player operations are not client-predicted. | `docs/architecture/domains.md`, `docs/backlog/README.md` |
| 158 | Kernel reset centralized in `KernelProtocolService`; no-legacy/command-authority/kernel-shape guards are active. | `docs/architecture/guards.md` |
| 159 | Manual world-time acceleration is cooperative: `Fast`/`SuperFast` never accelerate a shared session while any in-world player is awake; all-unconscious sleep remains the only shared-clock acceleration. | `docs/evidence/selfchecks/world/world-time-selfcheck.md` |
| 160 | Sleep policy: normal and forced sleep remain allowed; shared-clock acceleration is host-authoritative and only applies when every in-world alive player is unconscious; no sleep-gating host rule or new wire field. | `docs/backlog/resolved/sleep-behavior-policy.md` |
| 161 | Accept-first arbitration is limited to reports the host can REPRESENT: a runtime creation the host cannot materialize (its content set lacks the prefab/template) is rejected — never recorded, never relayed — and the rejection is answered to the reporter and logged/surfaced. An accepted-but-unowned record has no owner whose death can retract it, so it would leak into every later snapshot and resurrect state a peer already destroyed. | `AGENTS.md`, `docs/backlog/review/runtime-entity-spawn-backfill.md` |
| 162 | Save identity is transport-scoped and the save holds every member present at the cut: Steam keys are `steam-<steamId64>`, IP-direct keys are `name-<sanitized display name>` (that mode has no account identity). A player absent from the package joins as a NEW player with a fresh character and starting supplies; the two key spaces never silently collide. | `docs/architecture/save-archive-format.md`, `docs/backlog/in-progress/save-system-mid-run-and-layer-end.md` |
| 163 | Save restore is repair mode with minimal loss: salvage is per ENTRY, not per domain — an entry whose content no longer exists (e.g. a prefab removed by a mod update) is skipped with a warning while the remaining entries of that domain still apply. Only an unreadable manifest is a hard gate, and then the newest readable backup is used and the fallback is reported loudly. Restoration never silently regenerates the layer. | `docs/architecture/save-archive-format.md` §6 |
| 164 | The save is a multi-world repository, not a slot list: one folder archive per world (`worldId` immutable `w-<yyyyMMdd>-<4 hex>`, display name freely renameable and never the directory key) holding an unpacked `live/` snapshot plus N backup archives (`backups/<kind>-<yyyyMMdd-HHmmss>.cuoz`). The native single-slot surface cannot express a mid-run cut, so CUO owns the storage design, including a configurable interval autosave. | `docs/architecture/save-archive-format.md` §2/§7 |
| 165 | The CUO save system is fully independent of the native `save.sv`: CUO never writes it and never reads it, because the native format cannot express a mid-run cut and coexistence would create two sources of truth. The native Continue entry (`PreRunScriptLoadRunPatch` already intercepts `PreRunScript.LoadRun`) becomes the CUO continue path. | `docs/backlog/in-progress/save-system-mid-run-and-layer-end.md` |
| 166 | The v1 save scope covers the kernel checkpoint (all domains) + run baseline + character data + world diff + transient policy. Explicitly out of v1: mod-state persistence, host bans, and migration of the old protobuf kernel saves. | `docs/backlog/in-progress/save-system-mid-run-and-layer-end.md` (stage table S1–S4) |
| 167 | A mid-run cut is never taken from inside the callback that asked for it: the trigger ARMS it and the host pump takes it at ONE seam — the Game Adapter pump's last step (`cutPhase` = `frame-end`), where every domain has finished the frame's work and no command batch or frame flush is in flight. The console's `/save` (host only) and the host's deliberate menu return both go through that seam, and the menu return is a full mid-run cut taken BEFORE the leave. Layer-end cuts keep their own seam (the kernel's layer-advance commit, `cutPhase` = `layer-boundary`) and cannot be armed at the frame-end seam. | `docs/architecture/save-archive-format.md` §4, `docs/backlog/todo/save-mid-run-consistent-cut.md` |
| 168 | In-flight state is decided per class, never silently: `WorldTransientPolicy` declares one verdict per class — `capture` (carried as data), `resolve-before-save` (the cut is deferred, request still armed, bounded by `WorldSaveService.MaxCutDeferralFrames` = 8 frames, and named if it outlasts the deadline), or `drop-with-log` (the class is named with its count in the cut report; the console shows the player-initiated cuts). An undeclared class reported by an owner REFUSES the cut. A native world table that cannot be read refuses the cut instead of being read as empty, and a restore's live-world write reports its refused rows back to the caller that started the restore (`WorldRestoreAudit`), so a restore can never be reported as a success while the game's own bounded table dropped a row. | `docs/architecture/save-archive-format.md` §4/§6, `docs/backlog/todo/save-mid-run-consistent-cut.md` |

## Reference rules

- Older decision numbers remain traceable through
[  `tech-decisions-index.md`](index.md).
- Delivery-cycle detail and per-feature evidence live in
[  `tech-decisions-archive.md`](archive.md) and
  [`[docs/selfchecks/`](../evidence/selfchecks/).
