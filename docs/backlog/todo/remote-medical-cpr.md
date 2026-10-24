# Remote medical CPR enhancement (KrokMP custom)

- Status: Todo — **promoted 2026-10-07** by the user's backlog directive (do KrokMP's CPR feature), with the
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

**Still owed, and the ticket stays open for them**: stage 1b is the patient-side applier (write the pressure the rule
computes into the committed character data and let the game's own physiology carry it) and stage 1c the operator's
entry — the native wound view's button, cloned from `WoundView.specialUseButton` the way the ruling's KrokMP reference
does it. Neither is started; the rule above is the piece that had to be reviewed first because it decides what the
applier writes.
