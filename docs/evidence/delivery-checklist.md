# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the leave button belongs to a live session, not a cached lobby id
- [ ] Native parity — no game content; the window reads the session control it already holds
- [ ] Native parity probe — build 0 errors, gates 834/0, format exit 0
- [ ] Entry mapping — one entry, `OnlineUiHomeDrawer.BuildIpDirect`
- [ ] Mechanism inventory — a stale synthetic lobby id (1) hid the join controls on an IP-direct guest
- [ ] Whole-family audit — the copy button's lobby row uses `lobbyIsLive`, which already refuses a dead lobby
- [ ] Self-check table — one src file changed
- [ ] Verification design — after the deploy, the guest's window offers join and the pair joins
- [ ] Plan approved by the user — the goal's main line
- [ ] Structure review done — the condition names the three live facts instead of one cached id
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: deployed after the commit, judged by the pair's join

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
