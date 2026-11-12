# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the game's `TogglePause` refuses when its settings menu exists or `disallowPause` holds
- [ ] Native parity — the pause call is the same one the ESC handler makes, and the clock is the game's own `Time.timeScale`
- [ ] Native parity probe — two toggles after the game's own Escape both refused, so the row cannot be judged until the refusing gate is named
- [ ] Entry mapping — `pause-clock` entered through `PauseHandler.TogglePause` and read `time-scale-read` on both clients
- [ ] Mechanism inventory — the slice parks one case and records two heal probes; nothing under `src/` changed
- [ ] Whole-family audit — the pause row belongs to the world-acceleration family; its siblings (`world-clock`) pass
- [ ] Self-check table — gates 834/0 this cycle
- [ ] Verification design — the case would judge `paused` and `timeScale` on the host plus the member's own `paused`
- [ ] Plan approved by the user — night mode: a refused row is parked with its reading and the queue continues
- [ ] Structure review done — one new case file kept for the next attempt, one registry comment
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
