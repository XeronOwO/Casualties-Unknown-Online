# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the divergence is a write the relay does not cover; the fix moves the relay to `SetBlock`
- [ ] Native parity — the game's own `GenerateObjectAtPos` is the named writer (not our code)
- [ ] Native parity probe — `[SetBlock] 523,1015 -> 0 from WorldGeneration.GenerateObjectAtPos` and 34 differing rows, first at 988
- [ ] Entry mapping — `SetBlock` is the single runtime write entry for the block table
- [ ] Mechanism inventory — the deferred-apply path and the remote-apply guard already exist in `BlockBreakSync`
- [ ] Whole-family audit — every other block-table writer now has a named channel or a recorded reason
- [ ] Self-check table — the fix design names message, interface pair, handler and tests
- [ ] Verification design — the case `block-idle-drift` becomes the acceptance row once the channel lands
- [ ] Plan approved by the user — "locate the root cause and fix it"
- [ ] Structure review done — no code in this cycle; the record is the input for the next
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: no deploy in this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
