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
  continuous action rides), `todo/mod-content-ceiling.md` (a mod-authored medical interaction is the
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
