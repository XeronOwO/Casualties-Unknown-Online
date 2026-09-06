# Remote medical CPR enhancement (KrokMP custom)

- Status: Future
- Priority: Low
- Category: Remote medical / KrokMP enhancement
- Parent: `docs/backlog/todo/remote-medical-native-minigame-parity.md`
- Source: User decision (2026-09-06) — CPR can be placed in future.

## Context

- Native `Assembly-CSharp` does not contain a native `CPRMinigame`.
- KrokMP ships custom `CPRMinigame` and `CPRHandler` in:
  - `reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesMP/CPRMinigame.cs`
  - `CPRHandler.cs`
- This is therefore not a native parity item; it is an optional KrokMP-style enhancement.

## Scope if implemented later

- Decide whether to port KrokMP's custom CPR minigame as a CUO native-like remote medical action.
- If implemented, it should follow the same `MedicalOperationSession` protocol as other continuous actions.
- Must cover multi-operator/per-target exclusivity, press timing, stamina, heart kickstart and rib-break side effects.

## Non-goals now

- Not part of the current native medical minigame parity stages.
- No code work in Stages 1–3.
