# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the isolation's save points were read
- [ ] Native parity — the segment lists come from both clients' own logs
- [ ] Native parity probe — segments 1-16 agree and 17 slides by eight bytes
- [ ] Entry mapping — the first divergence is between segment 16's continuation and 17's save
- [ ] Mechanism inventory — the pod stage is the prime candidate
- [ ] Whole-family audit — the two clients stay in one session on the same run
- [ ] Self-check table — log reads and one ticket section
- [ ] Verification design — the next probe reads the pod stage's draws on both sides
- [ ] Plan approved by the user — root cause before the terrain backstop
- [ ] Structure review done — the finding is appended, the earlier misreads kept and corrected
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: no deploy in this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
