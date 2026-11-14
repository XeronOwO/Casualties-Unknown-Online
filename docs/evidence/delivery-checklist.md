# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the lockpick row needs the hand inside the ring band, measured per open
- [ ] Native parity — no product content; the case drives the game's own minigame
- [ ] Native parity probe — the case ran in the full order and failed on the sound, not on a crash
- [ ] Entry mapping — one entry, the lockpick minigame on the nearest medcrate
- [ ] Mechanism inventory — `handStarted` stays false because the hand sits outside the band
- [ ] Whole-family audit — no other case uses the pointer-to-hand scale
- [ ] Self-check table — one test file changed (parked with the reading)
- [ ] Verification design — the row re-opens when the per-open calibration exists
- [ ] Plan approved by the user — the goal's main line
- [ ] Structure review done — the case stays parked with its measured state
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
