# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the pause is the client's own and the container ramps the clock a frame after the field is set
- [ ] Native parity — the row writes the same `PauseHandler.main.isPaused` field the ESC handler toggles, and reads the game's own `Time.timeScale`
- [ ] Native parity probe — the toggle itself does nothing from the evaluator while every guard reads clear
- [ ] Entry mapping — the case enters through `pause-set` and judges `time-scale-read` on both clients
- [ ] Mechanism inventory — one recipe added, one case re-registered, one ticket note
- [ ] Whole-family audit — the row joins `world-clock`; both read the game's own clock field
- [ ] Self-check table — build 0 errors, gates 834/0, suite 27/27 this cycle
- [ ] Verification design — the row is judged by the clock value and by the member's own `paused`
- [ ] Plan approved by the user — night mode: the sweep row conversion is the goal's own queue
- [ ] Structure review done — the case keeps its own file and the toggle limit is named in its comment
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
