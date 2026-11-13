# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the suite's waits were caller sleeps; the client now answers when its own log carries the line
- [ ] Native parity — no game content; the driver reads the client's own `BepInEx/LogOutput.log`
- [ ] Native parity probe — `console-heal` PASS with the member's own line in the report
- [ ] Entry mapping — one entry: the driver's command table (`wait-log` beside `log-tail`), reached only by the suite's socket
- [ ] Mechanism inventory — one mechanism: a log line is the event; the client polls its own file every 100 ms inside the budget and answers `found`/`line`/`waitedMs`
- [ ] Whole-family audit — the other polling waits in the suite are unchanged and named: the limb and body polls still poll the game's own state, which is the same shape (a reading, not a sleep)
- [ ] Self-check table — suite builds 0 errors; `console-heal` PASS on the deployed pair
- [ ] Verification design — the case that needed the reset line is the check; the report names the line it waited for
- [ ] Plan approved by the user — the goal's main line: an event-driven acceptance project
- [ ] Structure review done — the helper sits beside `LogTailAsync` and shares its transport; no new process per wait
- [ ] Build + dotnet format + dotnet test normative gates pass — suite build 0 errors, gates 834/0, format 0, language clean

- [ ] Release-cycle deployment/acceptance: the pair ran the deployed `0.1.0+c6428d88e…` and `console-heal` passed with the new wait

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
