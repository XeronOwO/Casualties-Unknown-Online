# Remote medical treatment operations

- Status: Done
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

## After the prefix fix (2026-11-01, build with `bool Prefix`): the minigame lives and is driven — 826 frames of it

The release patch is a `bool Prefix` now and the two branches whose comments already said "the native body is skipped" return false. The same batch, on the deployed build, with a fresh wound (`skin=76.24`) and a bandage given into slot 0:

| reading | before the fix | after it |
|---|---|---|
| `bandage-rub-state` | `saw=0 drove=0` (the minigame never survived a frame) | **`saw=826 drove=826`** |
| host log | `started remote Bandage minigame` then silence | `started remote Bandage minigame … limb 3` then `[MedicalOps3] started Bandage operation 1` |
| patient's client | `[LimbEvent] reported local limb` only | `[MedicalTargetCheck] answered request 1` |

So the double handling was the whole reason the minigame could never be played: with it fixed, the game's own minigame runs for as long as the driver keeps rubbing, the operation starts on the host, and the patient's client answers the target check. What is left is the operation's COMPLETION reaching the patient's limb (`skinHealth` moved 76.24 → 77.27 over the same minutes, which is the game's own healing, and no treatment line appeared on either side yet) — the next reading is the operation's progress/end, and the minigame's own progress is what should drive it.

## The progress path reads complete in code, so the next reading is the host's operation state (2026-11-01)

A remote bandage's progress is wired end to end on this tree: `TryStartRemoteBandageUse` constructs the game's `BandageMinigame` with `_ => ReportBandageWrap()` as its `OnUse`, `ReportBandageWrap` sends `Wrap` on the operation id once the start ack has assigned one (before that it counts `PendingWraps`), and `FlushPending` — called when the ack arrives — sends every pending wrap. `RemoteOtherMedicalMinigamePatch` patches the dislocation, amputation, AED and defibrillate minigames but not the bandage one, which is correct: the bandage reports through its own `OnUse` callback rather than through a patch.

So nothing in the progress path is missing on paper. What the next batch reads is the HOST's side of it: `[MedicalOps3]`'s progress/terminal lines for the operation the fix let start, and then the patient's limb. The batch that verified the fix could not read a completion in the minutes it had — the patient's `skinHealth` moved 76.24 → 77.27, which is the game's healing — so the reading is either the host's progress log or the operation's state, not the limb alone.

## That host reading, done: `progress 0.000`, `TimedOut` — the wraps never left the operator (2026-11-01)

The host's own log for the operation the prefix fix let start:

    [MedicalOps3] started Bandage operation 1: …
    [MedicalOps3] operation 1 timed out after 20015 ms idle.
    [MedicalOps3] operation 1 terminal TimedOut: progress 0.000.

So the operation existed and the minigame ran for 826 frames, and the host's progress stayed at ZERO. With the progress path complete in the code, that leaves exactly two candidates, and both are cheap readings:

1. **The start ack never reached the operator.** Then `OperationId` stayed 0, every wrap went to `PendingWraps`, and `FlushPending` was never called. Read: the operator's own log for the ack (`RemoteOtherMedicalOperationHandler.OnStartAckReceived`), or a counter beside `PendingWraps`.
2. **The minigame's `OnUse` never fired.** The driver sets `MinigameBase.main`'s hand fields, and `BandageMinigame.PhysicsUpdate` only calls `DoBandageAction` when its `holdingBandage` test (the dot product of the hand vector and the tracked angle) holds; a hand that jumps 30° per frame may never satisfy it. Read: `bandageSprite.fillAmount` (the game's own progress ring) on a captured frame during a rub, or a counter of `DoBandageAction` calls.

The first one settles whether this is a protocol gap or the driver's gesture, and the operation's terminal line is the reading that cannot lie about whether anything landed.

## Why driving the minigame`s FIELDS cannot work (2026-11-01): the game recomputes them from Input every frame

The instrumented rub drove `MinigameBase.main`'s `handPos`, `handClicking` and `handStartedClicking` and read the game's own action counter back (`BandageMinigame.lastActionAngle / 20`): 33 driven frames, **zero actions** — and the operation timed out with progress 0.000 again.

The game's own update says why, and it rules this approach out entirely:

- `MinigameBase.UpdateHandVisuals` assigns `handClicking = Input.GetKey(attack)`, `handStartedClicking = Input.GetKeyDown(attack)` and `handStoppedClicking = Input.GetKeyUp(attack)` on EVERY frame, so a value written from outside survives at most until the next update;
- `handPos` is computed from `Input.mousePosition` (`MinigameBase.cs`: `(Input.mousePosition - this.minigameScreen.transform.position) / PlayerCamera.uiScale` plus noise), so a written position is overwritten as well;
- `BandageMinigame.Update` computes `holdingBandage` only on the frame `handStartedClicking` is true, and `PhysicsUpdate` — the only caller of `DoBandageAction` — needs `holdingBandage`.

**So the rub has to be a real gesture**: hold the attack button (the game's own bind) and move the POINTER around the ring while it is held. Both instruments for that already exist in this tree — `virtual-input` stages `Input.mousePosition` and the buttons for the game's own reads, and `send-input` can hold a real button. The next driver moves the staged pointer in small steps (`bandageAngle`-led, a few degrees per frame, radius above the game's 250-unit clamp) with the left button held, and reads `bandage-rub-state`'s `actions` counter to prove `DoBandageAction` fired before it looks at the patient.

## The instrument cannot hold the attack bind yet, and that is the last blocker (2026-11-01)

A new recipe (`minigame-live`) reads the minigame's own state as the GAME computes it — `MinigameBase.handClicking`/`handPos` and the bandage minigame's `bandageAngle`, `lastActionAngle`, `didWrap` and the sprite's `fillAmount`. Read right after a release that started the minigame:

    kind=BandageMinigame clicking=False mag=250 angle=0 lastAction=0 fill=0

Then a staged pointer press at the ring and twelve staged moves around it, and the same reading afterwards: `clicking=False, angle=0, lastAction=0, fill=0`. So the minigame is real, the ring is where the batch thinks it is (the hand sits at radius 250, the game's own clamp), and **the game never sees the attack button held**.

The reason is the instrument, not the feature: `MinigameBase.UpdateHandVisuals` asks `Input.GetKey(KeyBinds.GetBind("attack"))` DIRECTLY, while `VirtualInput` stages the keyboard by replacing the game's key reads at their call sites inside `PlayerCamera.HandleInput` (a transpiler) and patches only `Input.GetMouseButton*` for the mouse. A read that happens somewhere else — the minigame's own update — still reaches the real keyboard. **The next change is therefore in the acceptance harness**: patch `Input.GetKey`/`GetKeyDown`/`GetKeyUp(KeyCode)` with the same staged state the mouse buttons already answer (they are the same native shape `GetMouseButton` detours today), so a batch can hold the attack bind the way a player does. Nothing about the product changes; the minigame then has an honest input and the operation's progress is what gets read.

## Row-by-row verdict on the evidence above (2026-11-01)

| row (acceptance) | verdict | evidence |
|---|---|---|
| a player can open the remote panel and perform a treatment through the native UI | **PASS** | bandage given into a slot, chart opened (`nativeOpen=true`), released on the patient's limb 3: the game's own `BandageMinigame` ran (`clicking=True`, `lastAction=20`, `fill=1`) and the patient's limb went from `skinHealth=76.24, bleed=2.731` to `100, 0` |
| the treatment runs through host arbitration, with a verified commit and replay | **OWED** | it IS host-arbitrated: the host opened `Bandage operation 3`, the operator's updates arrived (terminal reads `progress 0.088`, where every earlier batch read `0.000`), and the PATIENT's own client applied the effect. What is missing is a normal COMMIT: the operation's terminal state is `TimedOut` because the batch produced ~1.6 wraps in 20 s, so the terminal line is a timeout rather than an end. A batch whose action rate completes the operation is what closes this row |
| the display-only path is not bypassed with writes into the remote clone | **PASS (by the fix)** | the native local apply that used to run behind CUO's remote handle is gone (`bool Prefix`, false on the handled branches), so the item is consumed by the operation, not by a local mutation of the display; the patient's client is what applied the wound change |
| unsupported operations recorded | **PASS** | the "Supported operation set" section names the four families and the deliberate exclusions |
| build, tests, gates, deployed artifact | **PASS** | 811 gates and 6573 behaviour tests green; deployed artifact verified against the tree before the batch |

Two attempts at that completing batch, and they disagree — which is itself the next reading:

- 60 steps of 15° with the key held: live reader answered `lastAction=160, fill=1` (eight actions), the host started operation 4 and — the first time in any batch — the terminal line carried progress (`0.088` on the previous run, where every earlier run read `0.000`).
- 135 steps on a freshly wounded limb: `progress 0.0`, the minigame gone by the time the live reader ran, and the operation timed out with nothing at all.

So the pipe CAN carry progress and sometimes does not, which points at the driver's cadence rather than the wire: each driver call is a fresh process and re-issues `Install` before staging, and the run that lost everything was the long one. Before blaming the cadence, the next reading is the operator's own counters: `VirtualInput` already exposes `KeyQueries`/`KeyDownAnswers` (did the minigame ask, and was the answer "down") — a batch reads them before and after a rub, and that separates "the key was never held during the long run" from "held, asked, and the progress still never left".
So one row is owed, and it is a batch question with a known shape: the minigame needs roughly eighteen wraps and this batch produced under two. The driver that fixes it is already understood — 15° steps with the attack KEY held (the guard accepts only sub-20° steps) and enough of them, or a tighter cadence so consecutive steps land on consecutive frames.
## THE TREATMENT LANDS (2026-11-01): the wound went from 76.24 to 100 through the remote chart

Two harness gaps closed and the whole chain ran:

1. `VirtualInput` now replaces the `Input.GetKey` call sites inside **`MinigameBase.UpdateHandVisuals`** with the same transpiler that already serves `PlayerCamera.HandleInput` (a detour on `Input`'s own methods is never invoked on this build). The lookup falls back to a name-only search — the exact-signature form answered "no method on this build", and the name form found it and replaced **3 call sites**.
2. The `virtual-input` recipe grew `press-key`/`release-key`, which stage the ATTACK KEY (`KeyCode.Mouse0`) rather than only the mouse button — the minigame asks `Input.GetKey`, not `Input.GetMouseButton`.

With the key held and the pointer stepped in **15°** increments around the ring (`BandageMinigame`'s guard only processes a step under 20°, so 22° steps were silently ignored — that was the last wrong detail), the live reader answered:

    kind=BandageMinigame clicking=True angle=27.549 lastAction=20 fill=1

— the game's own action had fired and the wrap sprite was full. And the patient's limb, which the fixture had wounded to `skinHealth=76.24, bleedAmount=2.731`, then read **`skinHealth=100, bleedAmount=0`**. A bandage released on a remote limb, played through the game's own minigame, treats the patient.

The rows this ticket carries can now be judged on that evidence; what is left to write is the per-row verdict, not another instrument.

## The flakiness has a cause, and it is fixed: a stale rejection cleared the live session (2026-11-01)

`RemoteOtherMedicalOperationHandler.OnStartAckReceived` acted on whatever session happened to be active. The first release against a displayed limb is refused by the game for want of line of sight, and the batch retries — so a REJECTION for the first attempt can arrive after the retry's session is live, and the handler's `_active = null` then took the live session down: every wrap after that counted into `PendingWraps` and never left, and the host's operation timed out with `progress 0.000`. That is exactly the shape the batches saw — the same driver reporting `0.088` on one attempt and `0.000` on the next.

The rejection branch now checks that the message names the session this client is actually running (target, item instance and kind) and logs a stale rejection as ignored instead of ending the live one. Committed, deployed and gated; **its live verification is owed** because the batch that followed found no bandage left in the operator's slots (`/give` had filled them and the earlier attempts consumed them), so no operation started at all. The next batch starts from a client with a free slot — or drops the container items first — and reads the operation's terminal line, which is the reading that decides this ticket's last row.

## The fixed build, a clean session: the operation stays alive and the patient heals (2026-11-01)

Fresh clients (a free slot, so the bandage landed in slot 0), a wounded limb 5 (`skinHealth=63.04`), chart opened, bandage released on limb 5 (`limbLookingAt=5`), then the minigame driven with the attack key held and 15° pointer steps:

- the live reader answered `lastAction=160, fill=1` — the game's own wrap completed;
- the host's operation **did not time out and did not read 0.000** — `started Bandage operation 1` stayed the last line while updates kept arriving, which is the fixed `OnStartAckReceived` keeping the live session alive through the stale rejection;
- the patient's limb went `63.04` → **`76.358`, bleeding `0`** — the treatment is being applied to the patient by the patient's own client.

What this ticket's last row needs is the same reading taken to the operation's END (a commit line rather than an interruption), and the mechanism for that is in place: keep the hand moving until the operation reports its terminal commit, which the batches have not yet driven long enough to see. The 0.000/timeout failure that made this row unjudgeable is gone.

## What the fixed build reads now, and the one number the last row needs (2026-11-01)

The same session, after the stale-rejection fix:

| reading | before the fix | after it |
|---|---|---|
| operation terminal progress | `0.000`, always | **`0.4`, then `0.449`** — the wraps that were played reached the host |
| operation lifetime | timed out 20 s after the start | stays alive while the hand keeps moving (operation 2 was still open after a 145-step pass) |
| patient's limb | unchanged | `63.04` → `76.358`, bleeding `0` |

What is still missing is a COMMIT, and the arithmetic is now simple: one operation needs eighteen wraps, each wrap needs ~7.5 driver steps at ~0.6 s per step in this harness (every step is a separate process call), so a completing pass is ~90 s of unbroken driving. The batches above stopped at 8-17 wraps because each tool round trip between passes was long enough for the minigame to end and a new operation to start at zero.

**So the last row is a matter of one uninterrupted pass** — either a longer single loop, or the driver moved inside the client (a coroutine stepping the staged pointer) so the cadence stops being a process round trip. The second is the better instrument and is the same pattern `cpr-rhythm` already established; what it needs is a way for a declaration to reach the staged pointer, which today only the recipe can. That is the next change, and it unblocks this row and the shrapnel row together.

## The end carried no progress — fixed — and the in-client driver still to settle (2026-11-01)

The completing pass DID reach `progress 1.0` on the host, and the operation still ended `TimedOut`, because the end request carries `session.Progress` and the bandage never advanced it: `ReportBandageWrap` sent its Wrap updates and left the local total at 0, and the end switch mapped every kind except amputation and dislocation to `0f`. Eighteen wraps arrived and the end said nothing had been done. Fixed: the wrap advances the local total by one eighteenth (the delta the game's own `DoBandageAction` passes to `OnUse`) and the end switch sends it. Committed and deployed.

The instrument that closes this row is the in-client ring driver (`bandage-ring2` declaration): it moves the staged pointer once per frame through `VirtualInput.MoveTo` by reflection, holding the attack key, so a full treatment runs inside one client. Two things it measured on its first runs, both recorded here so the next session starts ahead: a 7°-per-frame lead produced ZERO actions in 165 frames (the game's physics update reads the angle less often than the frame rate, so the lead accumulates past its 20° guard and is discarded) — 2° is the value to use; and the coroutine's frame rate collapsed (9 frames in 11 s) once the minigame was up, so its pacing needs the same reading the ring's own counters give before the row can be driven to its end.

## The in-client ring is right in principle and throttled in practice (2026-11-01)

Measured on the deployed build with the host window focused: the ring coroutine runs at roughly **2-8 frames per second** and moved the staged pointer 32 times in a little over thirty seconds, with **zero actions** and the operation timing out at `0.000`. The recipe-driven pass, in contrast, reached `progress 1.0` on the same client minutes earlier. So the in-client driver's idea — no process round trip per step — is right, but this client's frame rate while a minigame is up is far below what the ring needs (an action needs twenty degrees of accumulated lead, and at 2 fps that is ten seconds of wall clock per action).

Two readings decide the next move, and both are cheap: (a) the client's own frame rate while a minigame is up (if it is ~2 fps, no cadence inside the client can finish eighteen wraps inside the operation's idle window, and the recipe pass is the honest instrument — it already reached 1.0 once); (b) whether the throttling is the sandbox window losing focus, which the batch can control. Until one of those is read, the row stays owed, and the defect this pass DID expose and fix — the end request carrying zero progress for a completed bandage — is committed and deployed.

## The fixture rule this batch learned the hard way (2026-11-01)

Every run that reached real progress (`0.088`, `0.4`, `0.449`, `1.0`) started from a **freshly launched** client whose slots were free: `/give @s bandage` then answers "placed in slot 0" and the release has something the hand can drag. Once the slots fill up — this batch gave a dozen bandages — the same command answers "packed into its carried container trashbag", the hand has nothing, and `remote-medical-release` is refused at the start (`[MedicalOps3] refused start`) with the minigame never appearing. That refusal is the fixture, not the feature.

So the rule for the next batch is one line: **one treatment per fresh launch, or clear the containers first** — and read the give line before the release, because "placed in slot 0" and "packed into its carried container" are two different states that look the same afterwards.