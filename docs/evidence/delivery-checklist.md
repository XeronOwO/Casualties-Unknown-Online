# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the wait reads only lines written after it starts, so a trigger whose effect lands before the wait's evaluation needs the history read
- [ ] Native parity — no game content; the two instruments are the client's own log file (fresh tail) and its history
- [ ] Native parity probe — `console-heal` passes on a fresh line; `save-continue` passes on the history read
- [ ] Entry mapping — the cases reach the driver only through `SessionDriver`
- [ ] Mechanism inventory — one mechanism, two instruments: fresh tail (armed before the event) and whole-file history (the event may precede the call)
- [ ] Whole-family audit — the remaining polling waits in the suite are state polls (`limb-read`, `state`), not log reads; they stay polls of the game's own state
- [ ] Self-check table — suite build 0 errors; `save-continue` PASS after the revert; gates 834/0
- [ ] Verification design — each instrument is judged by the case that needs it, with the line in the report
- [ ] Plan approved by the user — the goal's main line: an event-driven acceptance project
- [ ] Structure review done — no new type; the contract is written on the ticket that owns the suite
- [ ] Build + dotnet format + dotnet test normative gates pass — suite build 0 errors, gates 834/0

- [ ] Release-cycle deployment/acceptance: both readings taken on the deployed pair

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
