# Acceptance record — Steam send-limit refusal handling (batch 20261001-r)

- Ticket: `steam-transport-send-limit-runaway` — verdict: **stays in `review/`** (rows 1–4, 6 and 7 hold;
  row 5, the live 30 s escalation, stays `unproven` on a named trigger gap, not on the code)
- Batch: `20261001-r` (one host + one guest session) — first hit on the ticket; the ticket was the only
  `todo/` item at the batch's start
- Commit: `1b088511` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+1b088511d555cf09ca19b336c2f3715f94c7df4e` (`tools/verify-deploy.ps1` exit 0, "Deployment
  matches this tree's build output")
- Run: 2026-10-01 16:56 → 17:06 · Host: physical machine (Steam) · Guest: the primary sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `inputs`, `logs`, `artifacts`
  (preflight: 11 present; the third client was not needed)
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Sustained refusals produce bounded logging | machine | **pass** | unit: `SteamTransportRefusalTests.SustainedRefusals_CostBoundedSteamCallsAndLogLines` — 10 000 `SendTo` calls over 10 s of virtual time make ≤ 20 channel calls, ≤ 20 log entries and ≤ 2 session/relay queries (pre-fix red on the same drive: 10 000 calls, one full diagnostics line each). Live corroboration: the whole ~6-minute refusal window added ≈200 kB to the host log (62 556 B at entry → ≈400 kB at exit, debug-level adaptive lines included) while Run E's log grew ≈7 MB **per second** |
| 2 | Refused attempts are throttled | machine | **pass** | live: `r-host-refusal-lines.txt` — `SendMessageToUser to <peer>: 5 refusal(s) and 76 suppressed attempt(s) over 5000 ms (queue full for 5000 ms)`: 76 attempts were held back while 5 reached Steam, and the 91 captured lines cover ~6 minutes of intermittent refusal, never one line per attempt |
| 3 | Recovery resumes and is announced once | machine | **pass** | live: the same file carries one `Sends to <peer> recovered after … s (refused N, suppressed M).` per episode (`0.3 s … 5.9 s`), and sends resume afterwards; unit: `Recovery_SendsAgain_AndAnnouncesTheEpisodeOnce`, `ResetRefusals_ReopensTheGate_SoTheNextSessionStartsClean` |
| 4 | Congestion stays visible to the rate governor | machine + logs | **pass** | live, with the host at debug level: `r-host-adaptive-lines.txt` — 64 `[AdaptiveSync]` lines name `pressure Critical` and stretch the streams (`EnemyStateBroadcast 10Hz -> 5Hz`, `PlayerStateBroadcast 10Hz -> 5Hz`, `FluidRegionFullStream 2857ms -> 6667ms`, `TraderStateStream 14286ms -> 15000ms`, `FluidRegionDiffStream 3Hz -> 2Hz`) |
| 5 | Host escalation | machine + residual | **unproven** | no ≥30 s **continuous** refusal episode could be produced in this session — see Limits. The policy half is unit-pinned (`StallEscalation_FiresOncePerEpisode_WhileRefusalsContinue`, `LapsedEpisode_DoesNotEscalate_AndTheNextRefusalStartsFresh`, `Stall_RaisesTheEscalationEdge_OncePerEpisode`) and the session half is integration-tested (`PeerSendStallWatchdogTests.HostStallEdge_DropsTheMember_KeepsTheHostSession_AndPublishesTheName`), so the gap is evidence, not code |
| 6 | Guest convergence | machine | **pass**, named setup | live: `r-guest-silence-line.txt` — `No frame from the host <host-id> for 15000 ms — ending the session locally.` (17:01:36), the guest's session then read `active: false` while the host's stayed active. The silence itself was produced by stopping the guest's own transport pump (its receive path was dead), **not** by a silent host — the watchdog can only observe receipt, so the row's condition holds, but the cause is not the row's implied one |
| 7 | Regression | machine | **pass**, named scope | live: before any injection the session ran normally with **zero** `SendMessageToUser` lines (host log 62 556 B, `steamInit: true`, both clients in world, evals answering); both clients left through the driver's `quit` with no orphan processes. The "leave the lobby in-game" half of the row was not re-run after the injections. Offline: `dotnet format` exit 0, build 0 warnings / 0 errors, normative gates 300/300, main suite 4573/4573 |

## How the refusal window was produced

The host's own Steam path was driven into `k_EResultLimitExceeded` (`QueueFull`) three ways, each
reversible and each recorded in `run-r-log.md`:

1. **Guest process suspended** (`suspend-process.ps1`, `NtSuspendProcess`) — this refuses immediately and
   is what produced the first 31 refusal lines, but the P2P session drops to `Connecting` and
   `AutoRestartBrokenSession` lets one send per restart succeed, so recoveries appear every 1–6 s and a
   30 s continuous episode cannot accumulate.
2. **Guest transport pump stopped** (`guest-pump-off.cs` flips `SteamService.IsInitialized` through its
   backing field, restored by `pump-on.cs`) — the session stays up and the game stops draining, but at
   this session's host→guest rate (measured ≈56–62 kB/s by `r-host-traffic-windows.txt`) the sender's
   queue never stayed full: refusals were intermittent (31 → 38 lines over the following minute).
3. **Host world flooded** (`host-flood.cs`, the game's own `floodfill player 0` ×10, plus
   `host-burst.cs`, `explode` ×8) — did not raise the send rate above ≈62 kB/s, so it did not change the
   picture.

## Limits

- **Row 5's trigger is the batch's open gap.** Run E's condition was a dense world pushing ≈1.5 MB/s at a
  peer that could not keep up; a fresh light session sends an order of magnitude less, and neither
  stopping the peer's receive path nor flooding the world kept the sender's queue full for the 30 s the
  escalation needs. The next attempt should reproduce the Run E world shape (its cut carries 0 enemy rows,
  so the density has to be regenerated in-run) or generate load through the game's own spawn commands —
  both are beyond this session's remaining budget.
- **Row 6's silence was injected on the guest side** (its own pump), so it proves the watchdog's contract
  ("no frame received for 15 s → local end + notice") rather than the host-goes-silent direction end to
  end; that direction is covered by the same watchdog's unit/integration tests only.
- **A peer whose session keeps restarting is not escalated** — by design, and now observed: each auto
  restart lets one send succeed, which clears the episode. That is the correct reading of "reconnecting"
  versus "stuck", but it means a suspended (fully unresponsive) process is not covered by the escalation
  path; its guest ends its own session instead once it can receive again.
- Rows are judged from this session's evidence only; a rare race is not excluded by one run.
