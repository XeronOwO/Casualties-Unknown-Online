# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — `remote-removal`'s red is a probe-family question, not a body leftover
- [ ] Native parity — no game content touched; the note names the next probe
- [ ] Native parity probe — the probe's own reading (`cloneCount 4`, components listed) is the evidence
- [ ] Entry mapping — one entry, the suite's `remote-clone-probe` reading
- [ ] Mechanism inventory — the case counts every `RemoteCloneRender`, items included
- [ ] Whole-family audit — the other cases that count clones (`clone-probe`) read the same probe and pass, so the probe itself answers
- [ ] Self-check table — tree clean, nothing deployed
- [ ] Verification design — the next probe is named: item clones versus the host's own items
- [ ] Plan approved by the user — the goal's main line: the suite carries the regression
- [ ] Structure review done — a reading, not a fix; the fix waits for the probe
- [ ] Build + dotnet format + dotnet test normative gates pass — no code change this cycle; gates were green at the last run

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
