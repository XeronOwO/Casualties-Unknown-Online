# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the pause call is refused while the handler's own three guards hold; the probe read every guard clear and the toggle still did nothing
- [ ] Native parity — the two recipes read the game's own `PauseHandler.main`, `isPaused`, `disallowPause` and `Time.timeScale`
- [ ] Native parity probe — `pause-probe` called `TogglePause` and read the handler's own field in the same eval: `before false, after false, pausedProperty false, exception ""`
- [ ] Entry mapping — the row enters through `PauseHandler.TogglePause` and reads `pause-state`
- [ ] Mechanism inventory — the slice adds two read-only instruments and one reading; nothing under `src/` changed
- [ ] Whole-family audit — the pause row belongs to the world-acceleration family; `world-clock` passes
- [ ] Self-check table — gates 834/0 this cycle
- [ ] Verification design — the next probe is the pause container's own `Update` against the handler field, named on the ticket
- [ ] Plan approved by the user — night mode: a refused row is parked with its reading and the queue continues
- [ ] Structure review done — the instruments are read-only recipes with their own header and return contract
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
