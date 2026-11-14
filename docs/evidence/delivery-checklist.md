# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the drive loop was read before the change
- [ ] Native parity — the stage name is the game's own coroutine type
- [ ] Native parity probe — the previous field read `stage=?` on both clients
- [ ] Entry mapping — the name is set on the frame that is about to save
- [ ] Mechanism inventory — nested drives set their own name
- [ ] Whole-family audit — one field, one log line, no behaviour change
- [ ] Self-check table — the reflection helper was removed, not left dead
- [ ] Verification design — the next run names the stage at segment 17 on both clients
- [ ] Plan approved by the user — locate the root cause, no workaround
- [ ] Structure review done — the probe stays inside the isolation that owns the boundary
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: deploy and read the stage field

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
