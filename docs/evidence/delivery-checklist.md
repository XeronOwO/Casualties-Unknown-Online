# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — a wait for a log line must mean a line written AFTER the call started; an old line is another run's event
- [ ] Native parity — no game content; the client reads its own file from a byte offset
- [ ] Native parity probe — `console-heal` still passes and still reports the reset line, now only a fresh one
- [ ] Entry mapping — one entry: the driver's `wait-log` command, reached only by the suite's socket
- [ ] Mechanism inventory — one mechanism: the byte offset at entry, the tail-only search, the `found`/`line`/`waitedMs` answer
- [ ] Whole-family audit — the other log reader (`log-tail`) is a deliberate whole-file read (it answers history); only `wait-log` needed the offset
- [ ] Self-check table — `console-heal` PASS after the change; suite build 0 errors
- [ ] Verification design — the case that reads a fresh line is the check; a stale line would have made it pass instantly with `waitedMs 0`
- [ ] Plan approved by the user — the goal's main line: an event-driven acceptance project
- [ ] Structure review done — 3 inserted fields in one command block; no new type
- [ ] Build + dotnet format + dotnet test normative gates pass — suite build 0 errors, gates 834/0, language clean

- [ ] Release-cycle deployment/acceptance: the pair ran the deployed build; the wait read a fresh line

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
