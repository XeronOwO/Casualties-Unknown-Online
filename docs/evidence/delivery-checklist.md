# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the minigame reads GetKeyDown once per press; a held key gives no second edge
- [ ] Native parity — no product content; the case drives the game's own minigame
- [ ] Native parity probe — with a release and a fresh press the lock opens; two of three runs passed
- [ ] Entry mapping — one entry, the medcrate's lockpick minigame
- [ ] Mechanism inventory — the pointer mapping is the game's own (`hand = (pointer - screenPos) / uiScale`) and the correction uses it
- [ ] Whole-family audit — no other case presses a held key twice
- [ ] Self-check table — one test file and the registry changed
- [ ] Verification design — three runs; the failing one read no `uiScale` (no minigame)
- [ ] Plan approved by the user — the goal's main line
- [ ] Structure review done — the case parks with the reopen-retry named as its missing step
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
