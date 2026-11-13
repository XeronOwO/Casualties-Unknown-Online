# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the member's session ended by the watchdog because the OLD link's cleanup removed the NEW connection's entry: `ReadLoop`'s `finally` called `DisconnectPeer(peerId)` unconditionally, and the guest's fresh link is registered under the same key (`HostPeerId = 1`)
- [ ] Native parity — no game assembly touched; the change is CUO's own transport cleanup
- [ ] Native parity probe — `IsConnected(peerId)` returns `_connections.ContainsKey`, so a stale loop deleting the fresh entry makes the watchdog's grace answer "down"
- [ ] Entry mapping — every path that drops a connection: the read loop's finally, the send failure, `Disconnect()`; the send failure and the wholesale disconnect keep their unconditional removal
- [ ] Mechanism inventory — the class of defect is "a cleanup keyed by peer id removes a replacement connection"; the guard `ReferenceEquals(current, expected)` closes it for both the guest's host link and the host's member links
- [ ] Whole-family audit — one more site would have the shape if a peer id were reused for two live connections; the host rejects a duplicate peer id at accept (`IpDirectTransport.cs:309`), so the read loop is the only owner
- [ ] Self-check table — build 0 warnings/0 errors; gates `834/0`; unit `6658/0`; no behaviour change for a peer that is genuinely gone (the removal still happens, the notification still fires)
- [ ] Verification design — the row closes when a guest joins the host's second session and its own log carries no `link ended` line; the fix is judged from that log
- [ ] Plan approved by the user — the goal's main line: finish the remaining tickets, one measured row at a time
- [ ] Structure review done — the guard lives in the one method that owns the map; the loop hands its own connection in, so no second notion of identity
- [ ] Build + dotnet format + dotnet test normative gates pass — build 0/0, gates 834/0, unit 6658/0

- [ ] Release-cycle deployment/acceptance: the fix is not yet deployed or read on the pair

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
