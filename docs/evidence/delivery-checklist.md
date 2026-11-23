# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: the late-join claim, the handshake and character hand-over lines and the run-id rule are written in the rebuild ticket's row 44 section.
- [x] Native parity — evidence: the join goes through the menu's own join controls; the host's own `Handshake confirmed end-to-end with <id>` line decides for that exact member.
- [x] Native parity probe — evidence: `.acceptance/late-1` PASS 66.2 s — the handshake named the late member and the run id was unchanged on both sides.
- [x] Entry mapping — host running (runId) → member clears its session → world exit → menu join → member Guest/inWorld with the same runId → host run unchanged → member `World join received` → host handshake line.
- [x] Mechanism inventory — `state` on both clients, `ReadMembersAsync`, `EnsureMemberAsync`, `ClearMemberSessionAsync`, member `body-items`, log tails.
- [x] Whole-family audit — evidence: the "`||` over two log lines" shape is the same family as the chime rows; the hand-over line stays a reading because it exists only when a saved character does.
- [x] Self-check table — evidence: three questions answered in the ticket's row 44 section (`||`, host screen, player's own join).
- [x] Verification design — evidence: a red names the handshake, the hand-over and the member's own state.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: the row drives the pair through the driver and reads logs; no recipe both stages and judges.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests only, nothing to deploy; `.acceptance/late-1` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
