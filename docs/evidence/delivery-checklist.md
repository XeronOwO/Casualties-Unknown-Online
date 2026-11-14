# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — a persona lookup on a Steam-less client throws on every frame and freezes the window build
- [ ] Native parity — no game content; the guard returns the caller's own fallback
- [ ] Native parity probe — build 0 errors, unit 6658/0, gates 834/0, format exit 0
- [ ] Entry mapping — one entry, `OnlineUiHomeDrawer.BuildIpDirect` → `ctx.DisplayName` → `SteamService.GetPersonaName`
- [ ] Mechanism inventory — the guard answers empty before any Steamworks call, so the drawer finishes and the join controls render
- [ ] Whole-family audit — the lobby-member lookup path uses the same service method
- [ ] Self-check table — one src file changed
- [ ] Verification design — after the deploy the guest's home page offers join and the pair joins
- [ ] Plan approved by the user — the goal's main line
- [ ] Structure review done — the guard sits at the service boundary, not in each drawer
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: deployed after the commit, judged by the pair's join

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
