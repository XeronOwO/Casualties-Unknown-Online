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
