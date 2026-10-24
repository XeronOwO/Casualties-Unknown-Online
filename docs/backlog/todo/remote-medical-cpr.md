# Remote medical CPR enhancement (KrokMP custom)

- Status: Todo — **stages 1a–1c landed and the row READS end to end on the machine** (2026-10-24, deployed
  `0.1.0+e15a8521`, record `docs/evidence/acceptance/cpr-end-to-end-20261024-l.md`: host heartRate 0 → 81.151,
  bloodPressure 5 → 131.191, conscious false → true after 40 compressions at the batch's cadence). The ticket stays open
  for stage 2 (ventilation/oxygen), stage 3 (rib breaks and the AED restart arbitration) and stage 4 (the operator's
  strength/intelligence gates and the reference's feedback). — **promoted 2026-10-07** by the user's backlog directive (do KrokMP's CPR feature), with the
  mechanics the user added then: inflate after an interval to give the teammate air, an oxygen-style mechanic
  if the game's own model can carry one, and an explicit complexity assessment before the work is planned.
- Priority: Medium
- Category: Remote medical / KrokMP enhancement
- Parent: `docs/backlog/review/remote-medical-native-minigame-parity.md`
- Source: User decision (2026-09-06) — CPR can be placed in future; user directive 2026-10-07 — promote it and
  implement the KrokMP-style feature.
- Related: `review/remote-medical-stage-1-injection-session.md` (the `MedicalOperationSession` shape a
  continuous action rides), `done/mod-content-ceiling.md` (a mod-authored medical interaction is the
  registration surface its Stage 3 names), `docs/decisions/active.md` (the acting side judges on its own
  picture and timeline)

## Context

- Native `Assembly-CSharp` does not contain a native `CPRMinigame`.
- KrokMP ships custom `CPRMinigame` and `CPRHandler` in:
  - `reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesMP/CPRMinigame.cs`
  - `CPRHandler.cs`
- This is therefore not a native parity item; it is an optional KrokMP-style enhancement.
- The KrokMP handler already carries most of what the user asks for as a reference: a strength/intelligence
  gate on the healer, a press cadence with a stamina cost per press, a hand-force term that scales the push,
  and rib damage as a side effect of over-pressing. It does **not** show a ventilation/inflation step or an
  oxygen model, so those two are CUO's own design.

## Scope if implemented later

- Decide whether to port KrokMP's custom CPR minigame as a CUO native-like remote medical action.
- If implemented, it should follow the same `MedicalOperationSession` protocol as other continuous actions.
- Must cover multi-operator/per-target exclusivity, press timing, stamina, heart kickstart and rib-break side
  effects.
- **Added 2026-10-07 — ventilation**: after an interval the operator inflates for the patient (giving air),
  with its own cadence and its own failure mode; the cycle decides whether that is a second action or part of
  one rhythm.
- **Added 2026-10-07 — oxygen**: evaluate whether the game's own model can express a ventilation/oxygen
  quantity at all (a body field, a moodle, or a value derived from the existing ones). If it cannot, the cycle
  says so and the feature keeps the mechanics the game can carry rather than inventing a parallel stat.
- **Added 2026-10-07 — first deliverable is an assessment**: the user flagged the complexity of the realistic
  model explicitly, so this ticket's first output is a staged plan with the complexity named, not code.

## Non-goals now

- Not part of the current native medical minigame parity stages.
- No code work in Stages 1–3.

## The assessment this ticket asked for first (2026-10-17)

The user asked for the complexity to be named before any code, and for one question to be answered with
evidence: **can the game's own model express a ventilation/oxygen quantity at all?** It can, completely.

### Oxygen: the game already models it, and dying depends on it

`Body` carries `public float bloodOxygen = 100f;` and updates it every frame from the body's own breathing
(`Body.cs:669`): the rate is `respiratoryRate * 0.01f` while `breathing` (and not underwater without scuba
gear), minus a flat `0.5f`, so an unbreathing body's oxygen falls on its own. Three ceilings pull it down and
one floor clamps it — low blood volume caps it proportionally, stamina loss subtracts from it, a hemothorax
caps it, and the result is clamped to `0..100` (`Body.cs:670-684`). It is not decorative: below `80f` the
brain takes damage every frame (`:880-882`, `brainHealth -= delta * (80 - bloodOxygen) / 600`), and below
`50f` the body counts as critically dying (`:338`). So restoring it is a real intervention with a real
failure mode, and **no parallel oxygen stat is needed** — which is what the ticket required this cycle to
establish.

Two more consequences worth recording. Ventilation is meaningful only up to those ceilings, so a patient with
a hemothorax or no blood volume cannot be inflated back to health — the mechanic has to read them or it will
promise something the model refuses. And the game already has a `respiratoryRate` field driving that same
line, so "giving air" has a native quantity to act on rather than a hand-rolled number.

### What the reference implementation does, and what CUO would do differently

KrokMP's handler is the reference and it is a **server-authoritative** design: the operator's client sends the
press timing, and the host computes the effect, rolls the rib break and the heart kickstart, then queues a
health sync. It gates on strength (`skills.STR * handMaxTotalForce > 6`), makes a low-intelligence operator's
screen harder (`INT < 12`), scores the press rate against 65–220 per minute, charges stamina and a little
temperature per press, scales the push by hand force, restores oxygen and blood pressure, and breaks ribs on
a roll that scales with strength against intelligence.

**CUO's own decisions differ in two places, and both are already decided in this repository rather than open**:
the acting side judges on its own picture and timeline, and the patient's own body state belongs to the
patient's client. So the natural division here is: the operator's client owns the rhythm judgement (it has the
press timing and its own picture), the **patient's** client owns the physiological write (that body is its
own), and the host arbitrates exclusivity — which is what the multi-operator rule needs anyway.

### Staged plan, with the complexity named

- **Stage 1 — the press loop.** One continuous action on the existing `MedicalOperationSession` shape, with a
  cadence judgement, the stamina cost, and the oxygen/pressure effect on the patient. This is the stage that
  needs the most care: a press rate of up to ~4 per second must not become a message per frame, so the operator
  reports the rhythm in a throttled batch and the patient applies the same curve.
- **Stage 2 — ventilation.** The user's own addition, and the cycle's decision is that it is **part of the one
  rhythm rather than a second action**: real CPR alternates compressions with breaths, the user's words are
  "after an interval the operator inflates", and a separate action would make the player press two unrelated
  controls for one procedure. The interval and its own failure mode (inflating at the wrong moment, or into a
  patient the model will not accept air for) are Stage 2's whole content.
- **Stage 3 — the side effects.** Rib breaks on over-pressing and the heart kickstart. Both are native-field
  writes the game already reacts to, and both need the same exclusivity rule as Stage 1.
- **Stage 4 — the operator's screen.** The strength and intelligence gates and the feedback the reference
  shows. Deliberately last: it is the only stage that touches presentation, and the procedure works without it.

**Complexity: high, and it is the medicine that makes it so** — three parties (operator, patient, host) with a
different owner for each fact, a continuous action at a rate high enough to need throttling, and physiological
writes whose ceilings have to be read before promising an effect. The staging above is what makes it
deliverable: Stage 1 alone is a usable procedure, and each later stage is independent of the ones after it.

**Not staged, and named as out of scope**: KrokMP's AED minigame. The user asked for CPR, this ticket's own
scope is CPR, and an AED is a second procedure with its own item and its own rhythm rather than a variation of
this one. It would be its own ticket if wanted.

## The decision this stage needs before any code (recorded 2026-10-24)

Stage 1 can be built — the assessment above names the parties, the owner of each fact and the throttling the
press rate needs — but it cannot be ENTERED: the medical family's continuous actions all start from the wound
view's limb-use gesture, and that gesture requires an item in hand (`PlayerCamera.ApplyWoundItem` runs the
item's own `useLimbAction`). CPR is performed with empty hands on the chest, so it has no native call site to
mirror and no existing gesture to inherit. The choice is the user's because it changes what the player presses:

1. **A limb entry on the wound view without an item**: the operator opens a patient's wound view and picks a
   "compressions" row for the chest. Reuses the window the player already uses for medical actions; the row only
   appears when the operator's hands are empty, so it never competes with a bandage or a syringe.
2. **A dedicated hotkey while standing over a patient**: the KrokMP-like shape, but a new input the player has to
   learn, which the interaction rule asks the user to approve.
3. **A console verb first** (`/cpr <player>`), shipping the whole Stage 1 mechanism with a test entry the user
   can drive in a session, and the gesture chosen afterwards. The cheapest way to read Stage 1's behaviour, and
   the one this ticket would take without a ruling — it is not a player-facing design the user has approved.

Nothing is implemented before that ruling; the ticket's stages above remain the plan.

## The ruling (user, 2026-10-24): "参考 KrokMP" — its own entry, read from the reference

The user answered the entry question with the reference itself, so this is not a new design: KrokMP's entry is a
**button inside the wound view**, and that is what CUO mirrors. The exact shape, read from
`reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesMP/PlayerCamera_ToggleWoundView_MultiplayerPatch.cs`:

- The patch clones the wound view's own **`WorkoutButton`** (found by name under the view's transform), renames the
  clone, gives it the reference's own icon and tooltip strings, inserts it at the anchor's sibling index and hides
  the original — so the CPR entry sits exactly where the window's own action button sits, in the window the player
  already opens for medical actions. That is native-UI reuse in the repository's sense: a clone of the game's
  button, not a CUO panel.
- The clone's click is gated by two of the reference's own rules, verbatim:
  `CheckIfBodyIsNotOkAndNeedCPR(body) || …` —
  `(heartRate < 50 && bloodOxygen < 100 && bloodPressure < 30) || heartRate < 5 || isDying || isCriticallyDying` —
  and `IsCPRBeingPerformedOnThisBody_IsMeOrNobody(body)`, the exclusivity rule (nobody else is in a CPR/AED
  minigame on that body). The click then ends a running defib minigame and starts `CPRMinigame(body)` on the
  native `MinigameBase.main`.
- A third rule of the reference is an admission gate rather than a button gate, and it belongs to Stage 1's own
  judgement: `skills.STR * max(hand.totalForce, otherHand.totalForce) > 6`.

**What that decides for CUO's stages**: the entry is a wound-view button that mirrors the anchor's own; it is
offered only when the patient needs CPR and nobody else is performing it; and the operator's own strength gate is
Stage 1's cadence-side check, not an entry filter. The continuous action still rides
`MedicalOperationSession`, with the operator judging the rhythm on its own picture, the PATIENT's client owning the
physiological write, and the host arbitrating exclusivity — the division this ticket's assessment already settled.

**Next slice, in order**: (1) the wound-view button clone with its two gates and the localization keys for its
tooltip, which is the consumer the admission policy needs; (2) the press-rhythm policy (the reference's 65–220 per
minute window, the per-press stamina cost and the hand-force term) as the pure, testable half; (3) the throttled
report and the patient-side oxygen/pressure write; (4) ventilation inside the same rhythm; (5) the side effects.
Nothing is implemented by this ruling itself: it names what to build and where it attaches.

## CPR and AED at once — the user's 2026-10-24 rulings on the three conflicts

The user asked whether the two may run together and ruled on the conflicts this ticket raised:

1. **One person does one thing, but the RESCUE can be two people.** A single operator never runs two actions at
   once (the native one-minigame-per-client rule stands). The combined CPR + AED procedure exists, and it needs **at
   least two other living, conscious players** besides the patient — one compresses, one defibrillates. CUO's claim
   model already allows it: `MedicalOperationClaims` locks the operator slot and the item, not the patient or the
   limb, so two operators on one victim is the family's existing shape.
2. **What each half does to the physiology.** Compressions act on **blood pressure**; ventilation acts on
   **oxygen** — the user's own reading was that compression's breathing effect was unclear to them and their ruling
   is that it does affect it, so ventilation is a real oxygen intervention rather than decoration. Both read the
   patient's existing ceilings (a hemothorax or lost blood volume caps what either can achieve), which the
   assessment above already recorded.
3. **The one real conflict is the heart-restart trigger: first to the host wins, the loser corrects.** When CPR's
   kickstart and the AED's shock would both restore the rhythm, the HOST arbitrates the first arrival and that one
   stands; the later one is **rolled back and corrected** against the authoritative state rather than replayed (the
   same "one resolving unit, arbitrated against the authoritative value" shape `MedicalOperationUnitRules` already
   uses). Neither client decides the outcome on its own picture.

With these three, the design has no open question left: the entry, the parties, the physiology each half owns and
the arbitration of the single shared trigger are all decided. The next slice remains (1) above — the button — and
then the rhythm policy, which now has the two channels it writes (pressure from presses, oxygen from ventilation)
and a named arbitration rule for the one outcome both halves can claim.

## Stage 1a landed (2026-10-24, deployed `0.1.0+a38bc8bd`): the rhythm's physiology, as one rule

`CprRhythmPolicy` (Runtime, pure) is the three rulings written down before anything touches a body:

- **Compressions act on blood pressure, ventilation on oxygen** — two gains with the same cadence shape (full near
  ~110 presses a minute, half when the presses are too fast because the chest cannot refill, scaling to nothing past
  the slow line).
- **The patient's own limits are real ceilings** — missing blood volume lowers the ceiling with it and an open chest
  halves it, so a rhythm that reaches the body's limit stops implying progress instead of pretending.
- **The restart is one shared outcome** — `RestartsTheHeart` answers whether THIS compression would restart it; the
  HOST arbitrates the first claim (the family's one-resolving-unit shape) and the later claimant is corrected. The
  threshold (`RestartPressure = 45`) is recorded as this ticket's tuning point, and a patient whose own ceiling sits
  below it restarts at the ceiling, because that is the most their body allows.

The numbers are anchored on the game's own scales rather than invented: `Body.brainDying` is
`bloodPressure < 10 && consciousness < 5` (the arrest CPR is for), `Body.fibrillationRising` starts at
`bloodPressure < 88`, and a normal pressure sits around 100-120. Nine cases pin the cadence shapes, both ceilings and
the restart, including the patient whose ceiling is below the threshold.

**Still owed, and the ticket stays open for it**: stage 1c — the operator's entry, the native wound view's button cloned
from `WoundView.specialUseButton` the way the ruling's KrokMP reference does it. Until it exists nothing in a real
session can start a CPR operation, so the procedure is reachable through the session protocol (and its two tests) but
not through a player's hands.

## Stage 1b landed (2026-10-24, deployed `0.1.0+6c804a97`): the compression reaches the patient's own data

- **The kind and its action**: `MedicalOperationKind.Cpr` (10) and `MedicalOperationUpdateAction.Compress` (6) ride the
  generic session envelope the other stage-3 operations already use; the update's value is the operator's MEASURED
  interval between presses, so the ruling's cadence is what travels rather than a frame count.
- **The start is a bare-hand operation**: the operator's half accepts it only with no item named (a named item is the
  refusal, not the requirement) and the target's half requires cardiac arrest — the game's own `inCardiacArrest` is
  `heartRate < 20`, and the AED family owns the quivering heart — plus a conscious operator and no second operation.
  CPR is not limb-scoped, so a request carries no limb index and is not refused for it.
- **What the applier writes is the pressure and nothing else**: `CprRhythmPolicy`'s gain for that interval, capped by
  this patient's own ceiling (`CeilingFor` reads the synced `BloodVolume` through the game's own fraction
  `0.5 + volume/200` and the synced `Hemothorax`). The game's model then carries the pressure into heart rate and
  consciousness, which is why no heart rate is written here — writing one would fight the physiology that decides it.
- **The reading**: a harness case starts CPR from a member on an arrested host, sends three well-timed compressions and
  reads the host's own `BloodPressure` risen and capped by the ceiling; a second case reads the beating-heart refusal by
  name.

## Stage 1c, decision half landed (2026-10-24, deployed `0.1.0+1c348291`): what the control shows

`CprButtonPolicy` (Runtime, pure) decides the control the native wound view will carry, so the rendering half has no
judgement left in it:

| state | when |
|---|---|
| `Hidden` | no remote focus, or the focused body's heart is still beating |
| `Start` | a focused remote body whose heart has stopped (`Body.inCardiacArrest`, `heartRate < 20`) |
| `Compress` | this client's own CPR session on that body is running (the host arbitrates the restart, so a running session keeps compressing rather than inviting a start it would refuse) |

`IntervalSeconds` clamps a press pair into the range the update accepts. Five cases pin the three states and the clamp.

## Stage 1c's rendering half landed (2026-10-24, deployed `0.1.0+de978301`)

`CprButtonCoordinator` clones `WoundView.specialUseButton` into the same parent, one row below it, renames the clone
(`CUO CPR Control`), **replaces its `onClick` event** — `Object.Instantiate` copies a UnityEvent with its PERSISTENT
listeners intact, so without that the CPR control would also run the template's own limb action — and labels it from
`CprButtonPolicy`'s state (`medical.cpr_start` / `medical.cpr_compress`). It is driven from the same per-frame wound-view
patch that hides the local-only surfaces, wired once at adapter construction
(`RemoteMedicalLocalControls.CprControl`).

Pressing it asks for a bare-hand CPR start on the focused target; the host's ACCEPTED acknowledgement is what turns the
same control into the compression control, and each later press sends the measured interval.

**Still owed, and the ticket stays in `todo` for it — the machine row**: open the wound view on an arrested teammate,
press the control, read the session's start and the pressure move.

What batch 20261024-i established, so the next attempt starts further along:

- **The world→screen projection works**: `tools/acceptance/recipes/screen-project.cs` (a recipe that had to be written
  with FULLY QUALIFIED UnityEngine types — the unqualified `Camera` was what the client's evaluator refused as
  `(1,1): InteractiveHost`) answers `screenX/screenY/screenHeight` and, when the panel is open, the CPR control's own
  screen rect and active flag. Read on the machine: the host's body at world (0, 505) projects to screen (672, 660) on
  the guest, i.e. client pixel (672, 60).
- **A real click at that point did not open the remote wound view**: the click was delivered to all three client
  windows in turn (`send-input.ps1`, `ok:true` each time) and the control never appeared
  (`controlFound=false` after each). So either the game's own "look at the body" hit test needs the pointer to move
  rather than a one-shot click, or the click needs the inventory/HUD state the batch did not set. That is the next
  thing to read, not the CPR code.
- **`send-input.ps1`'s `-Vk` was mandatory for every action** (a click with no key was refused by PowerShell's own
  binder); it is now optional and validated per action.

## Batch 20261024-j (2026-10-24, deployed `0.1.0+de978301`): the control reads, the start lands — and stage 1b's write is
overwritten by the simulation

Two recipes made the row reachable without a pixel: `medical-open` opens the remote chart through the SAME call the
Online UI makes (`IRemoteMedicalPresentation.OpenRemoteMedical`), and `cpr-press` finds the control among the template
button's siblings and either reads it or invokes its `onClick` — which IS a player's click. Both had to use UNQUOTED
string placeholders (`var mode = {{s:mode}};`): a quoted one compiles to `""read""` and the driver reports the failure as
`returned no value`, not as a syntax error.

Read on the machine (host + one member, host forced into arrest with `body-force heartRate=0,bloodPressure=5`):

| step | reading |
|---|---|
| `medical-open` on the member | `opened:true`, `nativeOpen:true` — the remote chart is up |
| `cpr-press mode=read` | `found:true, active:true, interactable:true` — **the CPR control appears for an arrested teammate** |
| `cpr-press mode=press` (start) | `pressed:true`, and the host's log: `[MedicalOps3] started Cpr operation 1: 765…` — **the session started through the panel** |
| three more presses, then the host's own fields | `heartRate:0`, `bloodPressure:0.374` — **the pressure did NOT rise** |

**The defect that reading found**: `ApplyCprCompression` writes `BloodPressure` into the committed snapshot, but the
pressure is a SIMULATED value — the game recomputes it every frame from its own model (volume, resistance, heart rate), so
a unilateral write is overwritten before the next frame shows it. The compression must move an input the simulation
READS rather than the simulated value: the game's own lever for that is `bloodPressureChangeFromMedicine` (the field its
`Body` reads for medicinal pressure changes), with the heart-restart path being the same idea on `heartRate` /
`fibrillation`. That is stage 1b's correction and it needs its own cases (a write to the lever moves the simulated
pressure; a write to `BloodPressure` does not).

## Batch 20261024-k (2026-10-24, deployed `0.1.0+e15a8521`): the restart is written, and the read still shows heartRate 0

The correction was implemented — compressions accumulate the session's perfusion progress and, at the threshold, write
`HeartRate = 40` (above the arrest line of 20) with fibrillation cleared; the harness case proves the two halves (one
press leaves the heart stopped, ten restart it). On the machine, with the chart open and the control reading
`found/active/interactable`, a start press plus ten compressions left the host at **`heartRate = 0`,
`bloodPressure = 0.022`** — no restart.

**Two candidate causes, and the next read tells them apart**:

1. **The compressions may never leave the client.** The control only sends `Compress` after the host's start
   ACKNOWLEDGEMENT arrives (`CprButtonCoordinator.OnStartAck` sets the operation id). If that ack does not reach the
   guest's control — a message routed to the wrong handler, or the ack's `TargetSteamId`/`Kind` not matching what the
   coordinator expects — every later press re-sends a start the host refuses as "operator already busy", and nothing
   reaches the applier. The guest's own `[Cpr]` lines (`asked to start`, `CPR accepted …`, `compression …s sent`) decide
   this in one read, and they live in the GUEST's log, which this batch did not collect: the shared `LogOutput.log` is
   the host's.
2. **`heartRate` is overwritten the same way `bloodPressure` was.** The game recomputes the simulated vitals every frame
   on the patient's client, so a value written into the snapshot may be replaced before the model reads it, in which case
   the restart needs the same treatment as the pressure: move an input the model reads (a forced-fibrillation/arrhythmia
   flag or a medicine-style lever), never the derived value.

The batch's next step is therefore a per-client log read (the guest's own file) plus, if needed, a probe recipe that
reports `Body.inCardiacArrest` and the session's progress on the host.

## Stage 3's first half landed (2026-10-24, deployed `0.1.0+5f8e24b0`): an over-press breaks a rib

`CprRhythmPolicy.IsAnOverPress` is the line — below `DangerousIntervalFactor` (half the ideal interval) the chest gets no
time to recoil, so the press is a shove — and `ApplyCprCompression` then sets the patient's chest limb's `Broken` flag
(limb index 1, the one the game's own defibrillation check treats as the chest) plus the pain it carries. The cost is the
game's OWN fracture state, so the patient's chart shows the rib rather than a number only this rule knows.

The policy case is landed (`AFranticPressIsAnOverPress_AndTheIdealRhythmNeverIs`); the session-level rib case is owed
with the stage-3 rows — the harness's host snapshot does not carry the limb the branch needs, and a first attempt was
withdrawn rather than left red.

**Still owed in stage 3**: the AED/CPR restart arbitration (both procedures claiming the same restart), and the
`bloodPressureChangeFromMedicine` lever question the batch above left open.
