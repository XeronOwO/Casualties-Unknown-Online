# Acceptance record — Split the body-verb service

- Ticket: split-the-body-verb-service — verdict: moved to `docs/backlog/done/`
- Batch: `20261105-a` — this ticket is the batch's only subject; the `review/` tickets still waiting were not served
  by this session (they wait on a user decision or a visual judgement, which is recorded on each of them, not here)
- Commit: `6308da4a` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+6308da4a` (the full hash
  is in this run's local `verify-deploy.ps1` output; `verify-deploy.ps1` exited 0)
- Run: 2026-11-05 22:50 → 22:53 (Asia/Shanghai) · Host: physical machine, Chinese UI · Guest: sandbox, English UI
- Dependencies: steam, game, sandboxie, hotrepl, input, logs, artifacts (preflight exit 0, 11 present)
- Artifacts: the directory named by `acceptance-artifacts-dir`, run folder `20261105-a` (the JSON files cited below)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | `/heal @a` after the split | machine | pass | the console read `heal.answer.reset_applied` then `heal.answer.clause_revived`; the body read `alive=true conscious=true brainHealth=100 consciousness=100 bloodVolume=100` after a kill had read `alive=false brainHealth=0` (`console-tail.json`, `health-healed.json`) |
| 2 | `/kill @a` after the split | machine | pass | the console read `kill.answer.applied`; the body read `alive=false brainHealth=0` with `bloodVolume=100 heartRate=70.178` unchanged, which is the native one-field write (`kill.json`, `health-killed.json`) |
| 3 | a member's request, granted and not granted | machine | pass | not granted: the guest's log reads `/heal refused locally: the host's announced roster does not name <member> — nothing was applied or sent`; granted: after `/op`, the host's log records the member's reset at 22:52:57 and the guest's console reads `Reset <host-id>: full heal applied.` (`guest-heal-refused.json`, `host-op.json`, `guest-heal-granted2.json`, `guest-console-tail.json`) |
| 4 | the recorded ceiling | machine | pass | `docs/architecture-debt.json` reads `{}`, and the source-shape gate ran green (812 gates) |

## Setup the rows needed

The host created a lobby and started the run; the two body rows ran on the host's own body in that world, and the
member row needed a guest in the same world. The guest joined the lobby while the host was already in a world, so the host
left the world through the game's own menu path and started the run again — the guest then entered the world with a body
(`member-id` read `sessionActive=true`, `body-read` read `inWorld=true`). The roster grant was the host's own `/op`, and
the member's first request after that still read the old local refusal because the announcement had not arrived yet; the
guest's log shows the announcement at 22:52:54 and the first request that reached the host at 22:52:57.

## Limits

- One batch, one ticket: the other waiting tickets were not judged, so this record makes no claim about them.
- The SDR transport blocker recorded on 2026-11-04 did NOT reproduce in this run: the host's relay read
  `k_ESteamNetworkingAvailability_Current` (21 valid relays), and the pair's session read
  `k_ESteamNetworkingConnectionState_Connected` (`transport-host.json`, `transport-pair.json`). The 2026-11-04 reading
  remains the diagnosis for that day; it is not a standing blocker today.
- Row 3's "not granted" half is the local refusal, which the guest's own log proves sent nothing. The host's refusal of a
  hand-built frame (a member that bypasses its own check) was not produced by a console line; it is covered by
  `CommandConsoleMemberHealResetTests`, not by this run.
