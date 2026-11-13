# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the window's leave button and hidden join controls read `ctx.IpDirectActive = _router.IsIpDirectActive`; the router answered a SELECTION (`_ipDirectActive`, set by `UseIpDirect()` and never reset), so after a session ended the window still said "in an IP-direct session"
- [ ] Native parity — no game content involved; the chain is the drawer → the window state → the router’s answer
- [ ] Native parity probe — the member's window offered `home.leave` with `role None, lobby 0` (a stale selection), read on the deployed `cf4a00172`
- [ ] Entry mapping — every reader of `IsIpDirectActive`: the drawer's session/leave rows, the member-list drawer, the network drawer, `OnlineUiHost`’s two assignments, and `CuoNetworkRouter.SendTo`’s transport choice; all now read the same live fact
- [ ] Mechanism inventory — the misreadings named: the flag is a selection, the transport’s `IsRunning` is the fact; the router already exposed `IsRunning` (`IpDirectTransport.cs:388`)
- [ ] Whole-family audit — the stale-selection shape exists only in this flag; `IsConnected` (peer-scoped) and `IsActive` (identity) are separate facts already fixed
- [ ] Self-check table — build 0 warnings / 0 errors; unit 6658/0; gates 834/0; `dotnet format` no diff
- [ ] Verification design — the row closes when the member re-opens the window after the host leaves and the join controls are offered; the suite’s `role-clearing` reads it
- [ ] Plan approved by the user — the goal’s main line: finish the remaining tickets, one measured row at a time
- [ ] Structure review done — one property changed to compose two existing facts; no new type, no new event
- [ ] Build + dotnet format + dotnet test normative gates pass — build 0/0, unit 6658/0, gates 834/0, format 0

- [ ] Release-cycle deployment/acceptance: the fix is deployed and the row is re-read on the pair in the next cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
