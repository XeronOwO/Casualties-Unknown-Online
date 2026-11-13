# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the pre-run join ticket's cause read in code: the host's ping tick sat behind `if (!SessionActive)`, so a session holding a world transition sent nothing and the guest's `GuestHostSilenceWatchdog` (15 000 ms) ended it
- [ ] Native parity — no game content is touched; the chain is the session's own tick
- [ ] Native parity probe — the heartbeat's first ping now waits one interval so an entry-window byte measurement is unchanged
- [ ] Entry mapping — the change sits on the one entry every session uses (`ICuoService.Update`) and its only other caller, the diagnostics `RequestPing`, was guarded
- [ ] Mechanism inventory — cadence 5 s (`PingInterval`), silence bound 15 s (three intervals), first-ping deferral (`_nextPingMs == 0` branch)
- [ ] Whole-family audit — the "no traffic during a transition" shape was checked on both sides: the guest's own ping to the host runs from the same tick and is now covered by the host-id guard; the peer maintenance calls already ran outside the session and are unchanged
- [ ] Self-check table — unit 6658/0, gates 834/0 after the debt record, `dotnet format` no diff, `check-language` clean
- [ ] Verification design — the unit facts pin the cadence and the first-ping deferral; the live row is the pre-run join (the member joins before the run and stays)
- [ ] Plan approved by the user — the goal's main line: finish the remaining backlog tickets, one measured row at a time
- [ ] Structure review done — the 616-line aggregate tripped `SourceShapeGateTests`; the growth is recorded with its split ticket instead of a silent overrun
- [ ] Build + dotnet format + dotnet test normative gates pass — build 0 errors, unit 6658/0, gates 834/0, format 0, language clean

- [ ] Release-cycle deployment/acceptance: deployed and read this cycle (the pre-run join row on the pair)

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
