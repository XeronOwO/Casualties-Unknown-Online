# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: the role-clearing claim, the host's `home.leave` path and the run-id rule are written in the rebuild ticket's row 45 section.
- [x] Native parity — evidence: the host ends the session through its own window control, the member joins session two through the game's own IP controls, and the run ids come from the clients' own state.
- [x] Native parity probe — evidence: `.acceptance/roles-1` PASS 26.6 s — the pair moved from run 4 to run 6, and the member ended in that same run.
- [x] Entry mapping — host `home.leave` → member role `None` (≤ 10 s) → second session `ip.create_host` + `start-run` → member `ip.join` → `Guest` + `inWorld` in the new run id.
- [x] Mechanism inventory — session `state` on both clients, window controls (`tab.home`, `home.leave`, `ip.create_host`, `ip.join`), `start-run`.
- [x] Whole-family audit — evidence: "role exists but the run is the old one" is the same shape as the other rows that judged presence instead of identity; run ids now decide.
- [x] Self-check table — evidence: three questions answered in the ticket's row 45 section (which run, member scene, player's own controls).
- [x] Verification design — evidence: a red names both run ids and the member's own state.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: the row drives windows and reads state; no recipe both stages and judges.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests only, nothing to deploy; `.acceptance/roles-1` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
