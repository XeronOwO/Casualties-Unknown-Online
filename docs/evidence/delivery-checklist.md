# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the reset line now names both the body it wrote and the body the camera reads
- [ ] Native parity — no new behaviour; one log field on the game's own reset path
- [ ] Native parity probe — build 0 errors, gates 834/0
- [ ] Entry mapping — one entry, `LocalBodyReset.Reset` called by the member's own instruction handler
- [ ] Mechanism inventory — the ids compared on the member's own client answer the ticket's next probe
- [ ] Whole-family audit — no other log line needs the camera id
- [ ] Self-check table — one src file changed
- [ ] Verification design — a live heal reads the line with both ids
- [ ] Plan approved by the user — the goal's main line
- [ ] Structure review done — the field sits on the existing line, no new branch
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: deployed after the commit and judged by a live heal

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
