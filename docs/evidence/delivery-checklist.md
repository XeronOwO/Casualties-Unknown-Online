# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [x] Design comprehension — the reset writes the captured health/limbs through `HealResetState.ApplyTo` then `ApplyHealState` — evidence: `LocalBodyReset.cs:65-101`
- [x] Native parity — the code is the game's own body and the table's values — evidence: the same lines
- [x] Native parity probe — the host stored the member's reported reset while the member's limb stayed hurt — evidence: the 07:22 reading in the ticket
- [x] Entry mapping — one entry, `/heal @a` on the host — evidence: `console-answer` `applied: true`
- [x] Mechanism inventory — the reset clears medication and hollows, un-breaks and un-dislocates limbs, then writes the table — evidence: `LocalBodyReset.cs:70-107`
- [x] Whole-family audit — no other path writes the same fields outside the reset — evidence: the capture/apply calls are the only writers in this file
- [x] Self-check table — documentation only — evidence: `git status --porcelain`
- [x] Verification design — the next probe reads the limb in the reset's own frame and again after 2 s — evidence: the ticket's next probe
- [x] Plan approved by the user — the goal's main line — evidence: the 2026-11-14 paragraph
- [x] Structure review done — the note names the write path and the suspected return — evidence: the diff
- [x] Errors are loud — the refusal and the applied line both name the body id — evidence: `LocalBodyReset.cs:130-132`

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)