# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops.
- [ ] Design comprehension — the pair setup read before the change
- [ ] Native parity — a down client is started, a live-but-stuck one is a finding
- [ ] Native parity probe — the case passes with no restart
- [ ] Entry mapping — the leave wait is one driver call
- [ ] Mechanism inventory — the driver blocks in-process, so no cross-process poll
- [ ] Whole-family audit — the 750 ms loop is gone
- [ ] Self-check table — test-only change
- [ ] Verification design — the next run must still pass and take less time
- [ ] Plan approved by the user — no dead waits
- [ ] Structure review done — one place, documented
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: no deploy in this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
