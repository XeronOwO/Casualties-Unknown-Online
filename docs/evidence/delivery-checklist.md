# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the reset writes the captured health/limbs through `HealResetState.ApplyTo` then `ApplyHealState`
- [ ] Native parity — the code is the game's own body and the table's values
- [ ] Native parity probe — the host stored the member's reported reset while the member's limb stayed hurt
- [ ] Entry mapping — one entry, `/heal @a` on the host
- [ ] Mechanism inventory — the reset clears medication and hollows, un-breaks and un-dislocates limbs, then writes the table
- [ ] Whole-family audit — no other path writes the same fields outside the reset
- [ ] Self-check table — documentation only
- [ ] Verification design — the next probe reads the limb in the reset's own frame and again after 2 s
- [ ] Plan approved by the user — the goal's main line
- [ ] Structure review done — the note names the write path and the suspected return
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
