# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the cycle adds one diagnostic line at the session teardown that names the role, the lobby id and the active flag the clearing branch decides on
- [ ] Native parity — the teardown is the game-independent Runtime path (`SessionService.TeardownSession`, `:522`), reached from `OnLobbyLeft` (`:411`) and `EndSession` (`:504`)
- [ ] Native parity probe — the earlier live reading showed the member ending with `active: false`, `hostId: 0`, `role: Guest`, and its own log carrying only `Session ended (role Guest kept).`
- [ ] Entry mapping — the entries: the host's leave-session control, the member's own `EndSession`, the teardown's clearing branch, and the new line
- [ ] Mechanism inventory — the misreading named: leaving only the world keeps the session (`active: true`); ending the session is the row
- [ ] Whole-family audit — the family is the teardown: `OnLobbyLeft` (lobby change) and `EndSession` (no lobby event) are both named; the new line fires on both
- [ ] Self-check table — build 0 errors, gates 834 passed / 0 failed, `dotnet format` exit 0
- [ ] Verification design — after the deploy, the host ends the session and the member's own `BepInEx/LogOutput.log` must carry `Session teardown finished: role …, lobby …, active …`
- [ ] Plan approved by the user — the user's main line is to finish the remaining tickets
- [ ] Structure review done — one `LogInformation` line at an existing teardown point; no new type, no new dependency
- [ ] Build + dotnet format + dotnet test normative gates pass — build 0 errors, gates 834/0, format exit 0

- [ ] Release-cycle deployment/acceptance: the ghost-role row is read from the deployed build after this deploy

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
