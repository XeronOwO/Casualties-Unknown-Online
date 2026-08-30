# Enemy Proximity Effects & Host-Local Lunge — Self-Check Table

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/selfchecks/MANIFEST.md` and
> `docs/architecture-evolution/protocol.md` before citing.

Delivery-cycle fact sheet for the remaining enemy-interaction backlog (ElderThornback /
Xaloris / GrabberPlant proximity side effects + the host-local CrystalEnemy lunge report),
plus the prefab-script mapping runtime check. Every touched mechanism is listed with the
change and its evidence. Verification is L0 / contract / simulation / smoke — not manual
dual-open acceptance.

## Step 1 — Mechanism inventory

| # | Mechanism | Current behaviour | Evidence |
|---|-----------|-------------------|----------|
| 1 | Enemy prefab script mapping | The generated enemy prefabs' component lists had asset-side evidence only; the freeze list covers every moving script already | HotRepl runtime eval 2026-08-15 (`Resources.Load` + `GetComponents<MonoBehaviour>`); `resources.assets` GameObjects 2949/2950/2951/3042/3050/3392/3530/3592/3848; `EnemyPatches.cs:27/33/51/57` |
| 2 | ElderThornback 1 s horror/stamina field | Every second, within 45 units, writes `horrifiedLevel/focusedLevel/adrenaline/energy/stamina` on the LOCAL body only; within 101.25 units writes the mid-range variant | `ElderThornbackBehaviour.cs:43-101` |
| 3 | ElderThornback defeat reward | `OnDestroy` sets `horrifiedLevel = 0`; when `health <= 0` and the local body is within 45 units it adds `happiness +40` and `caffeinated +600` locally only | `ElderThornbackBehaviour.cs:28-40` |
| 4 | Xaloris septic field | `OnWillRenderObject` every 0.5 s, within 5.5 units, adds `septicShock +0.074` to the local body only | `XalorisScript.cs:23-31` |
| 5 | GrabberPlant grab | Every 5 s within 3.2 units, grabs a random limb and applies `Ragdoll/Scream`, `shock = max(20)`, `eyePanicTime = 0.5` locally only; the existing event is a body-less trace (`EntityEventKind.GrabberGrabbed`) with empty apply/replay | `GrabberPlant.cs:75-90`; `TrapGrabberPlantPatch.cs:28`; `TrapEffectApplier.cs` / `TrapVisualReplay.cs` no-op cases |
| 6 | Host-local CrystalEnemy lunge | The native `Lunge` hits the host body directly; `EnemyCombatDirector.OnCrystalLunge` returns without a dedicated report when the selected victim is the local SteamId — the terminal state rides the 1 Hz character snapshot | `CrystalEnemy.cs:133-168`; `EnemyCombatDirector.cs:135-171` |
| 7 | Enemy event save-merge | `EnemyBite` / `EnemyLunge` events update the clone fact table but NOT the host's session-scoped saved character, so a disconnect before the next 1 Hz snapshot loses the event's terminal state on reconnect | `EnemyBiteHandler.cs:20-29`; `CharacterDataHandler.cs:20-29`; `CharacterDataStore._savedCharacters` |
| 8 | Wire surface | New message id, direction rule, version gate | `NetMsg.cs:119-121`; `PacketReceiver.cs:64-70`; `ProtocolVersion.cs` |

Explicitly recorded, not silently degraded: `LookTarget` local gaze/scare stays local presentation
(remote clones do not look at enemies); `Heater` on the `xaloris` prefab remains the existing
temperature-field low-priority item.

Updated 2026-08-23: `LookTarget` gaze/scare is now closed via the 20 Hz player entity stream
(`EntityStateMsg` carries the override gaze + the eye face timers, `SessionStatePump` writes them
onto the remote clone; see `docs/tech-decisions.md` #44). The `Heater` temperature-field item
remains the only recorded local-presentation gap.

## Step 2 — Design

- One new bidirectional star message: `EnemyEffectMsg` (NetMsg 85), `EnemyEffectKind`
  `ElderHorrorTick / ElderHorrorDefeat / XalorisSepticTick / GrabberGrabbed`. It carries the
  victim SteamId and the post-effect terminal fields (exact rebuild, never a delta), exactly like
  `EnemyBite` / `EnemyLunge`.
- `CharacterHealthMsg` gains `HorrifiedLevel / FocusedLevel / EyePanicTime` (ProtoMember 62-64)
  so the effect events and the 1 Hz snapshot fallback can both carry those terminal fields.
- A pure `EnemyTerminalStateApplier` (Runtime) applies bite/lunge/effect terminal state onto a
  `CharacterDataMsg`; the host `CharacterDataStore` and the Game Adapter's `CloneFactTable` share it.
- Host-local crystal lunge: `CrystalEnemyLungePatch` captures a pre-lunge limb trace in
  Harmony `__state` (host-local target only), the native `Lunge` still runs unchanged, and the
  postfix reports `EnemyLungeMsg` only after verifying the limb diff (verified commit — no diff,
  no report).
- `TrapGrabberPlantPatch` stops sending `EntityEventKind.GrabberGrabbed`; it reports the
  dedicated `EnemyEffectMsg` instead. The enum member stays ONLY as the trap-layout identity key
  (TrapEntityScan / TrapLayoutRegistry) and is documented that way.

## Step 2 — Self-check table (mechanism × change × evidence)

| # | Mechanism | Change | Evidence (file:line / test / runtime) |
|---|-----------|--------|----------------------------------------|
| 1 | Enemy prefab script mapping | No freeze-list change; runtime mapping recorded in docs | HotRepl eval output (prefabs above); `resources.assets` GameObjects; `EnemyPatches.cs:27/33` |
| 2 | ElderThornback 1 s tick | `ElderThornbackBehaviour.Update` prefix/postfix edge detection (private `timeChecked`) → `EnemyEffectKind.ElderHorrorTick` | `ElderThornbackBehaviour.cs:43-101`; new patch; `GameFieldContractTests` row; `EnemyEffectSyncTests` |
| 3 | ElderThornback defeat | `OnDestroy` postfix verifies `health <= 0 && dist < 45` → `EnemyEffectKind.ElderHorrorDefeat` | `ElderThornbackBehaviour.cs:28-40`; new patch; `GameFieldContractTests` row |
| 4 | Xaloris septic tick | `OnWillRenderObject` prefix/postfix edge detection (private `lastTime`) → `EnemyEffectKind.XalorisSepticTick` | `XalorisScript.cs:23-31`; new patch; `GameFieldContractTests` row |
| 5 | GrabberPlant grab | Existing no-grab→grab edge detection now reports `EnemyEffectKind.GrabberGrabbed` with shock/eye-panic terminal state; `EntityEventKind.GrabberGrabbed` becomes a layout-only key | `GrabberPlant.cs:75-90`; `TrapGrabberPlantPatch`; `../event-replay-matrix.csv` row update |
| 6 | Enemy terminal-state application | New pure `EnemyTerminalStateApplier`; `CharacterDataStore` merges bite/lunge/effect into `_savedCharacters`; `CloneFactTable` uses the same applier | `CharacterDataStoreTests` new merge cases; `EnemyTerminalStateApplierTests` |
| 7 | Wire direction / protocol | `NetMsg.EnemyEffect = 85` bidirectional; `ProtocolVersion.Current = 9` | `DirectionTests`; `EnemyEffectSyncTests` roundtrip + star relay; `ProtocolVersion` |
| 8 | Host-local crystal lunge | `CrystalEnemyLungePatch` prefix returns a pre-lunge limb trace; postfix verifies the changed limb and reports `EnemyLungeMsg` | `CrystalEnemy.cs:133-168`; `EnemyCombatArbitrationTests` local-first cases; `PatchContractTests` |
| 9 | Patch installability | Every new hook in the contract inventory and the startup verification count | `PatchContractTests`; deploy smoke `BepInEx/LogOutput.log` |

## Verification design

- **L0 / protocol**: `EnemyEffectMsg` roundtrip for every kind incl. zero-valued float fields;
  `EnemyTerminalStateApplier` exact-rebuild cases for all four event kinds; `EnemyEffectSyncTests`
  wire simulation (guest report → host apply+relay; host own effect → all guests; relay applies).
- **L0 / save-merge**: `CharacterDataStore` merges bite/lunge/effect into the saved snapshot and
  `SendSavedCharacter` returns the merged terminal state.
- **Patch contract**: every hook in the new proximity set resolves with the exact signature;
  the traversed private fields (`ElderThornbackBehaviour.timeChecked/build`,
  `XalorisScript.lastTime`, `GrabberPlant.grabBody`) have `GameFieldContractTests` rows;
  `SpiderHandler.Update` still carries freeze + target guidance.
- **Code gates**: `dotnet build` / `dotnet test` / `dotnet format` + `check-architecture` +
  `check-event-replay` + `check-entity-event-dispatch`.
- **Runtime smoke**: deploy to the real game directory only (`deploy.ps1` hard-rejects sandbox
  paths); start the real game once; `BepInEx/LogOutput.log` must show all patch targets installed
  and no CUO patch error.

## Closeout record

- Code gates: `dotnet build` 0 warnings/0 errors; `dotnet test` **661/661 green**;
  `dotnet format` clean; check-architecture / check-event-replay /
  check-entity-event-dispatch all passed.
- Deploy smoke (real game dir, post-deploy): `Game Adapter patches installed and verified
  (104 targets)` in `BepInEx/LogOutput.log` — the 3 new enemy-proximity patch classes installed
  with the rest of the patch set and no CUO patch error. The
  `System.ComponentModel.DataAnnotations` TypeLoadException in the same log is the pre-existing
  HotRepl-side error (present in the previous 101-target smoke log too, stack entirely inside
  HotRepl/NJsonSchema).
- Structure review: `GameAdapter` split into `GameAdapter.Enemy.cs` partial at the 600-line gate;
  all touched classes <= 600 lines and architecture gate passed.
- Delivery checklist: all real boxes checked line-by-line; the forbidden box remained unchecked;
  checklist reset for the next cycle.
