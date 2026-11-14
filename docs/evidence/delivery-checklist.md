# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — a minigame that closed during the aim must be reopened before the press
- [ ] Native parity — no product content; the case drives the game's own minigame
- [ ] Native parity probe — three consecutive single runs opened the lock with the reopen-retry
- [ ] Entry mapping — one entry, the medcrate's lockpick minigame
- [ ] Mechanism inventory — in the full order the press edge still misses in one run (`handStarted false`), so the case is order-sensitive
- [ ] Whole-family audit — the heal row also flaked in the same full run
- [ ] Self-check table — two test files changed, no product code
- [ ] Verification design — three singles green, one full run 30/32
- [ ] Plan approved by the user — the goal's main line
- [ ] Structure review done — the reopen is a guard, not a retry loop around the whole case
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
