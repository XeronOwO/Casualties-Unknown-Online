# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the console's Enter reaches the game's own IMGUI text field first, so the close must read the key where the opener does
- [ ] Native parity — the row drives the overlay's own `HandleKey` with an IMGUI keyboard event instead of calling the session rule directly
- [ ] Native parity probe — the empty-line close through `HandleKey` read `wasOpen true, consumed true, isOpenAfter false` on the live client
- [ ] Entry mapping — the player's Enter enters through `OnlineUiHost.Update` (opener and closer) and the OnGUI `HandleKey`; both lead to `ConsoleInputSession.SubmitOrClose`
- [ ] Mechanism inventory — `SubmitOrClose` closes only an empty line and submits otherwise, so the Update fallback closes only when `IsCommandConsoleEmpty`
- [ ] Whole-family audit — the same Enter is read in `OnlineUiHost.Update` for the opener; every surface that closes on Esc already guards its frame, so no second path needs the fallback
- [ ] Self-check table — build 0 errors, gates 834/0 after the shape gate's line budget was respected, `dotnet format` ran
- [ ] Verification design — the overlay handler's answer is the machine number; the physical Enter is the user's own press to confirm on the deployed build
- [ ] Plan approved by the user — the user reported the defect and asked for the fix; no design choice is pending
- [ ] Structure review done — no new type; two small accessors on the overlay and one branch in the host
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
