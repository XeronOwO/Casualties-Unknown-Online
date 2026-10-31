# Remote medical treatment operations

- Status: Review
- Priority: Medium
- Category: Remote player UI / cross-player medical actions
- Source: User request (2026-09-05) — the remote medical panel is view-only and cannot perform medical treatment operations, e.g. using a syringe on a remote player.

## Problem

The current remote medical/health panel (`done/remote-player-medical-panel.md`)
reuses the native WoundView but is explicitly display-only. A player can see a
remote player's body/limb condition, but cannot perform treatment actions that
the native medical UI would allow on a local player — e.g. using a syringe
(administer medicine/injection) or other medical operations from the panel.

## Goal

Allow the native remote medical panel to perform real medical treatment actions
against a remote player, while preserving the safe cross-player action
semantics already established by the project:

- Reuse the game's native medical UI/operations where one exists.
- Route each treatment operation through the existing host-validated /
  cross-player interaction/arbitration path, not by mutating the remote clone
  or the display-only body copy directly.
- Define the exact supported operation set (at minimum: syringe/injection;
  identify other native WoundView medical actions and either support or
  explicitly record them).
- Cover all roles/directions: local acting on remote, remote acting on local,
  and third-party views.
- Keep the remote clone non-authoritative; the target player's own client
  validates/applies the committed operation.

## Supported operation set

This cycle adds the native WoundView **drag-a-local-medical-item-onto-a-remote-limb** treatment gesture:

- Bandages/dressings from `RemoteHealProfiles` → `PlayerHealRequest` (host-authoritative heal, selected limb).
- Injectable/IV medicines from `RemoteMedicineCatalog` → `PlayerItemUseRequest` (host-authoritative medicine apply, selected limb).
- Topical treatment from `RemoteTopicalCatalog` → `PlayerItemUseRequest` (host-authoritative topical apply, selected limb).
- Non-liquid limb tools from `RemoteLimbToolCatalog` (splint, tourniquet, icepack, clottingmush, medicalsuture, etc.) → `PlayerItemUseRequest` (host-authoritative limb-tool apply, selected limb).

Wearable, food and drink-only items are deliberately **not** accepted by the
medical limb gesture: the native WoundView limb target is not their entry point,
so routing them would incorrectly become a remote wear/feed action from the
wrong UI.

### Explicitly not supported in this cycle

- Native WoundView **special-removal actions** (`WoundSpecialAction`: remove
  tourniquet, remove shrapnel, remove splint, fix dislocation) remain blocked
  while the remote medical focus is open. They mutate the local/display body in
  the native path and do not yet have a host-authoritative removal operation.
  They are listed here rather than silently deferred: a separate operation slice
  is required.
- Nap, radial use/wear, and other non-treatment special actions remain blocked.

## Implementation

- **Selected-limb wire support** — `PlayerHealRequestMsg` and
  `PlayerItemUseRequestMsg` gain a selected-limb representation. The protobuf
  field stores `limbIndex + 1` so limb 0 survives the protobuf default-zero
  omission; the C# `LimbIndex` property decodes 0/positive to auto/-1 or the
  actual limb. `ProtocolVersion.Current` bumped 8 → 9.
- **Runtime validates the selected target limb** — `RemoteHealApplication`
  gains `ResolveLimbIndex` (requested valid limb, otherwise most-injured auto
  pick). `PlayerHealService`, `PlayerItemUseService`, `RemoteMedicineApplication`,
  `RemoteTopicalApplication` and `RemoteLimbToolApplication` all honor it.
- **Native WoundView drag routing** — `RemoteMedicalPatches`
  `TryPerformSpecialUIAction` now routes only the WoundViewLimb drag release to
  the new bridge; all other special actions stay blocked.
  `PlayerCameraDragUsePatch` lets the remote-medical view reach the native UI
  before the world-overlap cross-player use path can preempt it.
- **Remote medical bridge handler** — new
  `IRemoteMedicalPatchBridge` / `RemoteMedicalOperationHandler`: checks the
  remote focus target, requires a local item with an authoritative instance id,
  rejects remote display proxies, and dispatches to `SendHealRequest` or
  `SendUseRequest` with the selected limb. The local eligibility check
  (`LocalUseItemEligibility.IsMedicalLimbUseItem`) accepts only the medical
  limb-treatment surfaces above and rejects zero-condition items.
- **Read-only guarantee** — `PlayerCamera.WoundSpecialAction` and
  `PlayerCamera.ApplyWoundItem` are both blocked in remote focus, so no native
  path can mutate the display body; the remote display body is never written by
  this treatment path.
- **Remote view mutual exclusion** — opening the remote medical panel closes an
  open remote backpack, and opening a remote backpack closes an open remote
  medical panel. `RemoteBackpackView.Close` also clears the native radial-open
  state so the two native surfaces cannot overlap and bypass the backpack proxy
  protections.

## Acceptance criteria

- A player can open the remote medical panel and use the native medical
  treatment UI (e.g. syringe) to perform a treatment on the remote player.
- The treatment is applied through the existing cross-player interaction /
  host arbitration path, with a verified commit and replay on the target side.
- The display-only WoundView path is not bypassed by direct writes to remote
  clones.
- Unsupported native medical operations are recorded explicitly above (not
  silently left as an undocumented future).
- Full build, tests, architecture/event/entity gates pass; deployed artifact
  verified before `review/`.

## Evidence

- Selfcheck: `docs/evidence/selfchecks/players/remote-medical-treatment-operations-selfcheck.md`
- Handler: `src/CasualtiesUnknownOnline.GameAdapter/RemoteMedicalOperationHandler.cs`
- Bridge: `src/CasualtiesUnknownOnline.GameAdapter/IRemoteMedicalPatchBridge.cs`
- Patch: `src/CasualtiesUnknownOnline.GameAdapter/Patches/RemoteMedicalPatches.cs`
- Selected-limb runtime: `RemoteHealApplication.ResolveLimbIndex`, `PlayerHealService`,
  `PlayerItemUseService`, `RemoteMedicineApplication`, `RemoteTopicalApplication`,
  `RemoteLimbToolApplication`
- Protocol: `PlayerHealRequestMsg`, `PlayerItemUseRequestMsg`, `ProtocolVersion.Current = 9`

## Non-goals

- Not re-introducing a parallel CUO medical panel.
- Not mutating the remote render clone as the operation path.
- Not implementing remote special-removal actions in this cycle (recorded above).
- Not expanding beyond the native medical UI's own operations without user direction.

## Batch 20261101-d: the release does reach the operation, and the row now waits on the MINIGAME, not on the gesture

A wounded patient was made on purpose (new recipe `wound-seed`: the game's own `Limb.ImpactDamage`, `reversing/Assembly-CSharp/Assembly-CSharp/Limb.cs` — force 220 left the guest's limb 2 at `skinHealth=8.92, bleedAmount=18.184`). Then the host gave itself a bandage, opened the guest's chart (`medical-open` → `opened=true, nativeOpen=true`) and released the bandage on limb 2 (`remote-medical-release` → the item left its slot).

The host's own log says what happened next, and it is not "nothing":

- `[MedicalOps3] refused start for … ` then `[MedicalView] remote Bandage start rejected for …: No line of sight.` — the first attempt was refused with a named reason (the operator's body was not in line of sight after the teleports);
- `[MedicalView] started remote Bandage minigame for … limb 2 (item bandage).` — the retry STARTED the treatment as the game's own bandage minigame.

The patient's limb read unchanged (`skinHealth=8.92, bleedAmount=17.9`) because a minigame-start is not a completed operation: the wound view's own minigame has to be played to its end before the effect lands. **So this ticket's live row now waits on a minigame driver, exactly like the shrapnel and CPR rows** — the same shape `shrapnel-break` and `cpr-rhythm` already solve with a declaration rather than a recipe, because the pieces have to be driven across frames the game itself runs. The next batch starts from that instrument, not from another release.

## The minigame driver exists (`bandage-rub`), and what it has not yet caught

The same batch built the driver the row waits on: a declaration (`bandage-rub`) that moves the minigame's OWN hand — `MinigameBase.main.handPos`, the field the game reads as the pointer — around a circle one step per frame with the hand held, so `BandageMinigame.PhysicsUpdate` measures its angle between frames the game itself advances and calls `DoBandageAction` (the game's function, never ours). A recipe (`bandage-rub steps=n`) starts it; both compile and load on the client.

What is still owed is TIMING, and the batch measured it: the bandage minigame ends on its own within a frame or two unless a hand is driving it, and a rub started in the same call as the release performed `steps=120` frames while `minigame-end` answered `before=none` — the window had already closed. The next attempt starts the rub FIRST and keeps it running ACROSS the release (the driver now waits for a `BandageMinigame` to appear instead of breaking on the first empty frame), then reads the patient's limb for the applied effect.

## Two fixture corrections the next batch starts from (2026-11-01, read off one frame and one give line)

The frame taken right after a release shows the remote view with a **joint-reset control** ("reset joint") over the patient's limb, not a bandage control — and the give line for the item said it went into the host's carried container because no slot was free. Both are the batch's own faults, not the feature's:

- **The wound fixture also dislocated the limb.** `Limb.ImpactDamage` runs its dislocation roll at `force > Random.Range(jumpSpeed * 3.5, jumpSpeed * 5)`, and a 220 force passes it — so the limb's current special action is the joint reset, and a bandage release lands on a limb whose menu is offering something else. The next fixture uses a force that wounds without dislocating (the skin roll is at `1.55..4.4 × jumpSpeed`, the dislocation roll at `3.5..5 × jumpSpeed`: a force near the low end of the gap wounds and does not dislocate), and `limb-read` now confirms `dislocated=false` before the treatment.
- **The item went into a container.** With the slots full, `/give @s bandage slot 5` answered "nowhere to put it — dropped at the player's position", and an earlier give went straight into the carried trashbag. A release can only drag what the hand can hold, so the batch empties a slot first (or picks up the dropped bandage) and confirms the item's `origin` with `item-census mode=body` before releasing.

## The driver does not report, and that is the next thing to fix (2026-11-01)

The instrumented rub never printed its line — not the per-run summary, and not the first-frame line added to prove the coroutine runs at all — in either log (`BepInEx/LogOutput.log` and Unity's own `Player.log`). `Start` answers `started`, so `StartCoroutine` was called on a live `PlayerCamera`; what is missing is any evidence that the coroutine body executes, and a `Debug.Log` from the evaluator's dynamic assembly appears to reach neither file.

**So the next instrument reports through CUO's own logger, not Unity's**: the driver's counters need to go where the rest of the acceptance reads live (the adapter's `ILogger`, which is what puts `[MedicalView]` lines in `LogOutput.log`), or the driver needs to write its counters into a field the acceptance can read with a recipe (`minigame-end`-style) instead of a log line. Until one of those exists, "the rub drove nothing" and "the rub never ran" are indistinguishable — which is exactly the class of defect this project keeps recording.

## The counters are readable now, and they say the minigame is not where the operator's hand is

The driver's counters moved into static fields (`Starts`, `Saw`, `Drove`, `Running`, `MainNull`, `Other`) that a recipe reads (`bandage-rub-state`), because a `Debug.Log` from the evaluator's dynamic assembly reaches neither log. The reading, after a 800-frame rub running across a release on limb 4:

| counter | value | what it means |
|---|---|---|
| `starts` | 2 | the coroutine really starts and runs to its end (`running=false` after the first) |
| `mainNull` | 0 | `MinigameBase.main` was present on every sampled frame |
| `other` | `none` | `MinigameBase.main.currentMinigame` was NULL on every frame |
| `saw` / `drove` | 0 / 0 | the hand was never driven, because there was no current minigame to drive |

And the host's own log, in the same seconds, says `[MedicalView] started remote Bandage minigame for … limb 4`. **So the bandage minigame a remote treatment starts is NOT `MinigameBase.main.currentMinigame` on the operator's client** — either CUO's remote path drives a different object, or the minigame lives on the patient's side. That is the next question, and it is now a readable one: the counters separate "no minigame object" from "a different minigame" (the `Other` field names it).

The patient's limb confirms nothing landed: `skinHealth` went 19.75 → 23.58 and `bleedAmount` 15.6 → 2.8 over the same minutes, which is the game's own wound healing, not a bandage — its log carries `[LimbEvent] reported local limb 4` and no treatment line.

## Why the minigame dies in the same frame it starts, read from the game's own update (2026-11-01)

The counters said `currentMinigame` was null on every frame the rub sampled, while CUO's own start path asserts the opposite at the moment it runs: `RemoteOtherMedicalOperationHandler.StartMinigameSession` calls `MinigameBase.main.StartMinigame(session.Minigame!, item)` and immediately checks `ReferenceEquals(MinigameBase.main.currentMinigame, session.Minigame)` before logging that it started. So the object IS there at start, and it is gone by the next frame.

The game says when that happens — `reversing/Assembly-CSharp/Assembly-CSharp/MinigameBase.cs`, `ActiveMinigameUpdate`'s first check:

    if ((!this.currentItem && this.currentMinigame.NeedsItem()) || !this.body.conscious) { this.EndMinigame(); return; }

and `BandageMinigame.NeedsItem()` returns true. So the minigame ends on the next frame when its `currentItem` is gone (or the operator's body is unconscious). The release path consumes the dragged item — `remote-medical-release` answers `dragAfter=none`, the item leaves its slot — which is exactly the shape that leaves `currentItem` empty on the following frame. **The next reading is `currentItem` itself** (add it to the driver's counters): if it is null by the first sampled frame, the batch must keep the item alive across the start (send the start before the release consumes it, or hand the minigame a live item), and that is a CUO-side question, not a gesture one.

## The double handling, named: the same release both starts the minigame and applies the item locally

`RemoteOtherMedicalOperationHandler.StartMinigameSession` hands the minigame the item (`MinigameBase.main.StartMinigame(session.Minigame!, item)`), and CUO's remote release reaches that code by running the game's own release path (`PlayerCamera.HandleReleaseDragging` → `TryPerformSpecialUIAction`). On the local limb the game's release is what APPLIES the item — it consumes it and starts the minigame with it in one motion. On the remote display limb the same release therefore does both: it consumes the operator's item AND starts a minigame whose `currentItem` is the object it just consumed, and the game's own `ActiveMinigameUpdate` ends any minigame whose `currentItem` is gone (`(!this.currentItem && this.currentMinigame.NeedsItem())`). That is why the counters see it for zero frames.

**The fix is a CUO-side decision, not a gesture**: either the remote release is diverted BEFORE the native local apply consumes the item (the operation is host-authoritative; the local apply must not run), or the item's consumption is deferred to the operation's completion — which is also what native play does, since the bandage is spent when the treatment finishes, not when it starts. Naming it here keeps the next session from re-deriving it: the minigame is real, the item is real, and the item dies in the same call that starts the minigame.

## The exact change, read off the patch (2026-11-01): the prefix cannot skip the original

`PlayerCameraDragUsePatch` hooks `PlayerCamera.HandleReleaseDragging` with `private static void Prefix(...)` — a void prefix. Every handled branch inside it ends with a bare `return;`, which means "handled here" but also **lets the original method run**, and the original is the game's own local apply/use — the call that consumes the item and starts the minigame with it. So the remote path does the work twice: CUO routes the operation to the host AND the native body applies the item locally.

The change its fix needs is therefore mechanical and small: `bool Prefix` — `return false;` on the branches CUO handled (the give onto the displayed backpack, the use on its centre), `return true;` where the game's own drop semantics are the answer (the "anywhere else" branch). Each branch's verdict has to be stated one by one, because skipping the original too eagerly removes the game's own drop and too timidly leaves the double apply — and the batch that reads the result is the one this ticket already carries: the minigame must survive its first frame (the counters say so) and the patient's limb must change.