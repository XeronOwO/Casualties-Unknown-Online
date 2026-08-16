# TutorialHandler claw double-give — mechanism inventory and self-check

Owner cycle: backlog item-domain TODO "TutorialHandler claw double-give in the
tutorial world (tutorial domain)". Decision: the tutorial claw's creations are
**per-player course props**, not shared world objects. Mark them at the native
`Utils.Create` call and keep them out of the shared item/entity domains until a
player actually picks one up — then the existing generation-item pickup flow
(spawn-then-pickup) takes over. No protocol change, no new wire message.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The native creation path | `TutorialHandler.Update` (`reversing/.../TutorialHandler.cs:184,255-271`): when `objectToCreate` is set and the claw reached `handPos`, the game calls `Utils.Create(this.objectToCreate, this.handPos, 0f)`, grabs it, plays the dispense sound and sets `lastSpawnedObject`. The branch is the ONLY `Utils.Create` call in that method |
| 2 | Both sides run their own course | Each process has its own `TutorialHandler.main` (`TutorialHandler.cs:44,488`) and starts its own course coroutine; the courses are per-side (the claw `handPos` follows each local `body`). Therefore host and guest each create their own prop at their own claw position |
| 3 | Current item double-give | `ItemPatches.ItemStartPatch` funnels every runtime item into `ItemWorldSync.OnItemInstantiated` (`ItemWorldSync.cs:177-236`), which allocates a per-SteamId instance id and reports the spawn. Both copies enter the host's world table under two ids; the host relays each to the other side and `ItemApplication.SpawnWorldItem` materializes the one it does not already have — every player sees two copies of the same course item |
| 4 | Current entity double-give | `BuildingEntityStartPatch` funnels every runtime `BuildingEntity` into `EntitySpawnSync.OnEntityInstantiated` (`EntitySpawnSync.cs:70-108`), which reports the creation. Both sides' copies are registered/relayed, so a claw-created building prop (barbed wire fence, terminal) can also exist twice |
| 5 | Marker timing | `UtilsCreateTutorialPatch` is a postfix on the exact `Utils.Create(string, Vector2, float)` overload. Unity runs `Item.Start` / `BuildingEntity.Start` on the frame AFTER an Update-phase `Instantiate`, so the marker added in the same postfix is visible when the domain hooks run |
| 6 | Call identity, not patch state | `TutorialHandlerUpdatePatch` opens `CallContext.Origin.TutorialClawSpawn` for the duration of the original `Update` and disposes it in the postfix (`__state` only — no cross-call patch state, AGENTS.md #10). `UtilsCreateTutorialPatch` marks only `Utils.Create` results inside that scope |
| 7 | Item-domain skip | `ItemWorldSync.OnItemInstantiated` returns before `_trace.NextOperationId`/`Allocate` when the item carries `TutorialClawProp` (`ItemWorldSync.cs:190-195`). The item stays id-less, exactly like a generation-time item |
| 8 | Pickup path is already built | `PickupSync.OnPickedUp` id-less branch (`PickupSync.cs:160-185`) allocates the id and commits ONE report that sends `ItemSpawned` then `ItemPickedUp` — the proven generation-item path. The host registers and transfers to the picker's inventory; peers materialize the spawn and immediately remove it on the pickup |
| 9 | Entity-domain skip | `EntitySpawnSync.OnEntityInstantiated` returns before any report for a marked entity (`EntitySpawnSync.cs:77-80`). A claw-created building prop never enters the shared entity domain |
| 10 | Cross-player bind guard (items) | `ItemApplication.FindExistingAt` skips marked items (`ItemApplication.cs:439-443`): a shared spawn can never bind to another player's private course prop, so one player's pickup can never destroy the other player's course object |
| 11 | Cross-player bind guard (entities) | `EntitySpawnSync.FindExisting` skips marked entities (`EntitySpawnSync.cs:285-291`): a shared entity creation can never absorb (and a later removal can never destroy) a private course prop |
| 12 | Course progression has no id dependency | The courses wait on `grabInfo.Item1`, which `TutorialHandler.Update` sets immediately after the local `Grab` (`TutorialHandler.cs:260-264`); the local item object exists and is grabbed with or without an instance id. Picking/wearing later runs through the ordinary `Body.PickUpItem`/`WearWearable` hooks |
| 13 | Snapshot/keyframe/reconcile are marker-safe | Position streams and the periodic item keyframe only read id-stamped items (`ItemPositionAuthority.cs:55-65`); `ItemReconcile` only kills/aligns id-stamped items (`ItemReconcile.cs:52-68`). Id-less tutorial props are invisible to all of them until picked up |
| 14 | Generation snapshot cannot catch a course prop | `GeneratedItemAuthority.Publish` runs once on the generation-finished edge (`GeneratedItemAuthority.cs:66-135`); the course-select screen and the first `objectToCreate` happen after that edge. A later layer switch does not exist in the tutorial world. Accepted boundary: a pre-existing id-less prop is NOT in a late joiner's snapshot — see §2 |

## 2. Whole-family audit

The change touches the runtime item/entity entry family. Every sibling is aligned in the same round:

| Family member | Change |
|---|---|
| `ItemWorldSync.OnItemInstantiated` | New marker guard: tutorial props stay id-less; every other runtime item path is unchanged |
| `PickupSync.OnPickedUp` / `OnPickupStart` | Unchanged — the id-less branch is the designed exit from the local-prop state (spawn-then-pickup, one commit) |
| `ItemWorldSync.OnItemDestroyed` | Unchanged — an id-less prop destroys silently; after pickup the item has an id and destroys normally |
| Item position stream / periodic keyframe / `ItemReconcile` | Unchanged — they never see id-less props; after pickup the prop is an ordinary domain item |
| `GeneratedItemAuthority` / `GeneratedItemApplication` | Unchanged — the generation edge runs before any course prop exists; marker items would otherwise be excluded from binding (`FindExistingAt`) and never destroyed as host-unknown because they are id-less and the reconcile pass only sees id-stamped items |
| `EntitySpawnSync.OnEntityInstantiated` | New marker guard: claw-created building props stay local and never report |
| `EntitySpawnSync.FindExisting` | New marker guard: shared entities never bind to a private prop |
| `BuildingEntity` damage/open/health family | Unchanged — position-keyed live reports still apply between overlapping copies; a private prop simply never got a spawn id |
| `CallContext.Origin` | New `TutorialClawSpawn` scope; the stack semantics and disposal are unchanged |
| `Utils.Create` patch family | `UtilsCreateDropPatch` (DamageBlockOrigin) and the new `UtilsCreateTutorialPatch` (TutorialClawSpawn) are two independent postfixes with disjoint scopes |
| Solo / mid-course late joiner | Accepted boundary recorded: tutorial courses were never session-synchronized; a prop created before a joiner arrives stays on the creator's side only. The joiner's own course creates its own prop. Re-open only with a deliberate tutorial-domain sync pass |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Claw-created object identification | `TutorialHandlerUpdatePatch` scope + `UtilsCreateTutorialPatch` marker, attached in the same postfix | Reflection patch-surface tests lock both patch shapes; marker type is a field-less MonoBehaviour |
| Marker before the domain hooks | The marker is added in the same `Utils.Create` postfix; Unity `Start` runs on the following frame | Static evidence (Unity lifecycle + patch shape); same proof shape as `DropOrigin` (`UtilsCreateDropPatch`) |
| Item no longer double-reports | `ItemWorldSync` marker guard before allocation | Code path; reflection test locks the patch targets; full suite covers the unchanged runtime half |
| Entity no longer double-reports | `EntitySpawnSync` marker guard before report | Code path; patch contract resolves `TutorialHandler.Update` and `Utils.Create` exactly |
| Course still progresses | Local `grabInfo.Item1` set from the local created object, no id involved | Static evidence `TutorialHandler.cs:260-264`; `BasicCourse` waits read `grabInfo.Item1` / the local `Item` |
| Pickup still works | Existing id-less pickup branch: allocate id, spawn-then-pickup, host transfer | Existing `PickupSync.cs:160-185`; the Runtime transfer is covered by the existing item simulation/race suites (no new wire shape) |
| No cross-player prop destruction | `FindExistingAt` + `EntitySpawnSync.FindExisting` skip marker props | Code path; reflection tests verify the marker type these guards read |
| No new protocol | Marker is local-only; the wire formats are untouched | ProtocolVersion unchanged; `DirectionTests` still green |
| Game update guard | Both new patches are `[HarmonyPatch]` classes | `PatchInventory.BuildContracts` automatically gains `TutorialHandler.Update` and `Utils.Create(string, Vector2, float)`; `PatchContractTests` resolves them against the real game assembly |

## 4. Verification design (development-period, no manual acceptance)

- Reflection patch-surface suite: `TutorialClawPropTests` locks the marker type, the
  `TutorialClawSpawn` call identity, the Prefix/Postfix `__state` shape, the
  `Utils.Create` postfix shape, and the two new patch contracts.
- Patch contract guard: `PatchContractTests` resolves both new contracts against
  the real `Assembly-CSharp.dll` — a game update that renames/retypes either
  target fails `dotnet test` before launch.
- Runtime half regression: the id-less spawn-then-pickup transfer is unchanged
  and already covered by the existing item simulation/race suites; the full
  `dotnet test` run is the guard.
- Static evidence: native creation path `TutorialHandler.cs:255-271`; per-side
  course independence (`TutorialHandler.main`, `StartCourse`); marker-before-Start
  timing (Unity lifecycle); id-less invisibility to stream/keyframe/reconcile.
- Runtime verification box for this development-period cycle: **L0 reflection +
  static evidence, no manual acceptance** (user rule 2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `docs/backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## 6. Verification results (2026-08-16)

Development-period rule applied: **no manual acceptance** — the runtime
verification box is checked on L0 reflection + static evidence (user rule
2026-08-16).

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | TBD |
| `TutorialClawPropTests` + `PatchContractTests` focused filter | TBD |
| `dotnet format CasualtiesUnknownOnline.slnx` | TBD |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | TBD |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | TBD |
| Patch contract | `PatchInventory.BuildContracts` contains `TutorialHandler.Update` + `Utils.Create(string, Vector2, float)`; `PatchContractTests` loaded the real game assembly and passed |
| Static evidence | Native creation path `TutorialHandler.cs:255-271`; marker-before-Start timing; id-less props invisible to stream/keyframe/reconcile |

## 7. Structure review

- Touched classes stay under the 600-line gate: `TutorialClawProp` 21,
  `TutorialHandlerUpdatePatch` 23, `UtilsCreateTutorialPatch` 29;
  `ItemWorldSync` / `ItemApplication` / `EntitySpawnSync` remain under the gate
  (TBD after the architecture gate).
- No new expression-state bool fields: the marker is a field-less MonoBehaviour
  and the patches carry only `__state`.
- Dead mechanisms: none. The generic item/entity report paths remain the only
  paths for every non-marker runtime object; the id-less pickup path is the
  designed exit, not a duplicate.
