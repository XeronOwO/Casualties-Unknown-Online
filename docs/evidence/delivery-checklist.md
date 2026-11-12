# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the lock's completion needs the game's own `handStarted` while the button is held
- [ ] Native parity — the reading is the game's own minigame state
- [ ] Native parity probe — the hand reaches the band (225.2) with the button down; `handStarted` stays false
- [ ] Entry mapping — the entry is the held press through `lockpick-hold` and the game's own update
- [ ] Mechanism inventory — the standalone run completed; the suite's fresh run does not
- [ ] Whole-family audit — the row stays parked with the new reading
- [ ] Self-check table — the report's own `aim`/`live`/`progress` lines
- [ ] Verification design — read the aim's `correctAngle` against the hand's angle next
- [ ] Plan approved by the user — night mode: a park with its cause, no product decision
- [ ] Structure review done — the case stays beside the others, switched off by one comment
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
