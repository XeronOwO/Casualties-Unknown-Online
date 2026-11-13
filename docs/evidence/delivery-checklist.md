# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the row's remaining half: a member joining the host's second session has its own IP link to that session, and the old link's close must not end the new one
- [ ] Native parity — no game content involved; the chain is CUO's own router → `PeerDisconnectWatchdog` → `SessionService.EndSession`
- [ ] Native parity probe — the watchdog's grace already asks `_router.IsConnected(peer)` before acting (`:91-97`), so the end means that answer was false at the grace
- [ ] Entry mapping — the entries that can end a guest session: `PeerDisconnectWatchdog.DecideAndAct`, `GuestHostSilenceWatchdog`, `PeerSendStallWatchdog`; only the first logged this end
- [ ] Mechanism inventory — the misreadings named: the log line alone does not say which peer id the new session's host carries, and the grace answers by the id the old edge named
- [ ] Whole-family audit — the same "old edge ends the new session" shape exists only in the watchdog's guest branch; the host branch uses the same `IsConnected` check and is covered by the link-grace tests
- [ ] Self-check table — no code changed this cycle; the finding is documented with its line numbers; the next slice adds the diagnostic
- [ ] Verification design — the row closes when the member joins the host's second session and its own log carries no `link ended` line; the probe reads the peer id the new link registers
- [ ] Plan approved by the user — the goal's main line: finish the remaining tickets, one measured row at a time
- [ ] Structure review done — the note lives on the ticket that owns the row; no new document
- [ ] Build + dotnet format + dotnet test normative gates pass — unchanged code; the last full run this session was build 0 errors, unit `6658/0`, gates `834/0`, format 0

- [ ] Release-cycle deployment/acceptance: the row was driven on the deployed build; its second half stays open

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
