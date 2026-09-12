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
| 167 | A mid-run cut is never taken from inside the callback that asked for it: the trigger ARMS it and the host pump takes it at ONE seam — the CUO pump's last step (`cutPhase` = `frame-end`), the point where no CUO command batch is mid-commit and no CUO frame flush is mid-send (the game's own scripts may run before or after that pump, so the guarantee is the one synchronous read, not Unity's frame boundary). The console's `/save` (open to anyone; the SAVE LAYER refuses a guest, which is what keeps solo play — where there is no session role — able to save) and the host's deliberate menu return both go through that seam, and the menu return is a full mid-run cut taken BEFORE the leave; when the cut cannot be written the leave still happens and the previous snapshot stays. Layer-end cuts keep their own seam (the kernel's layer-advance commit, `cutPhase` = `layer-boundary`) and cannot be armed at the frame-end seam. | `docs/architecture/save-archive-format.md` §4, `docs/backlog/todo/save-mid-run-consistent-cut.md` |
| 168 | In-flight state is decided per class, never silently: `WorldTransientPolicy` declares one verdict per class — `capture` (carried as data), `resolve-before-save` (the cut is deferred, request still armed, bounded by `WorldSaveService.MaxCutDeferralFrames` = 8 frames, and named if it outlasts the deadline), or `drop-with-log` — plus whether CUO can COUNT it (an `Observed` row is named with its count, a `Standing` row — the game's craft coroutine, item velocity, the run clock, the earthquake timers — is named as a class every mid-run cut leaves behind). An undeclared class reported by an owner REFUSES the cut. A native world table that cannot be read refuses the cut instead of being read as empty, and a restore's live-world write reports its refused rows (or an engine throw) back to the caller that started the restore (`WorldRestoreAudit`), so a restore can never be reported as a success while the game's own bounded table dropped a row. | `docs/architecture/save-archive-format.md` §4/§6, `docs/backlog/todo/save-mid-run-consistent-cut.md` |
| 169 | The run values the native `SaveSystem` used to load are split by OWNER and by SEAM, not packed into one blob: the two rarity multipliers are world-generation INPUTS, so they ride the kernel run baseline (`RunState` → `WireRunState` → `WorldStartParams`) and a cut STAMPS the cut instant's value on them — a side that generates a layer with the game's fresh `1f` builds a different layer than the run's authority, which is what a guest joining a run mid-way used to do. The run clock base is a `run.json` `native-run-fields` row, written at the slot the native `SaveSystem.TryLoadGame` used to occupy because `WorldGeneration.Start` derives the layer's time limit from it. The recipe unlock table is the SAME row but lands at the world-entry seam with the other native layer facts, because the game rebuilds `Recipes.recipes` in `WorldGeneration.Awake` and CUO's mod-content provider appends the custom recipes on a LATER Update frame — a save-slot write would refuse every custom recipe's row. A reader that cannot read the run fields REFUSES the cut rather than storing a clock that restarts at zero and a re-locked recipe table; a snapshot without the row is restored with the live clock and recipe state and NAMES the gap, and a refused recipe row reaches the same restore account. | `docs/architecture/save-archive-format.md` §3.4/§4/§6.1, `docs/backlog/todo/save-native-run-field-parity.md` |
| 170 | A continue hands the LOCAL player's own character back to the client that can apply it: the Runtime binds every stored key a present peer claims into the character table the reconnect path sends from, and returns the key the LOCAL player claims with the continue outcome (`WorldContinueOutcome.LocalCharacter`), which the adapter queues on the SAME local restore path a next-level respawn uses (`CharacterDataSync.QueueLocalRestore`, the two-frame wipe/apply). Nothing else can apply it: the scene — and therefore the only body it can go on — loads after the click, and the character-table slot holding it is the one the live 1 Hz snapshot overwrites. The queue remembers WHICH RUN queued it (`LocalCharacterRestoreQueue`): a restore this client's own run queued is dropped when this client instead follows a start it did not restore, a run this client starts ON ITS OWN drops whatever waited (a peer's un-followed hand-over included), a peer's hand-over survives the follow it belongs to, and session end clears it — so a restore armed for an abandoned continue can never land on another run's body, and a reconnect is never swallowed by the follow that brought it in. A `layer-end` cut's character POSITIONS are dropped at that restore — the snapshot names the layer being ENTERED, which is regenerated, so a position captured in the layer being left would teleport the body into a layer that no longer exists; a mid-run cut names the layer its bodies stood in and keeps its position. | `docs/architecture/save-archive-format.md` §3.4/§6.1, `docs/backlog/review/save-layer-end-save-and-restore.md` |

## Reference rules

- Older decision numbers remain traceable through
[  `tech-decisions-index.md`](index.md).
- Delivery-cycle detail and per-feature evidence live in
[  `tech-decisions-archive.md`](archive.md) and
  [`[docs/selfchecks/`](../evidence/selfchecks/).
