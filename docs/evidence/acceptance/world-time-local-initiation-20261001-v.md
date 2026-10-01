# Acceptance record — World-time acceleration is gated on being asleep (local-first initiation)

- Ticket: `world-time-local-initiation` — verdict: **moved to `done/`** (row 4's three instances pass
  this run; rows 1-3 and 5-8 were judged in batch `20261001-u`, and row 7 is superseded)
- Batch: `20261001-v` — ticket `world-time-local-initiation`
- Commit: `0a62fb96` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+0a62fb96…` (`tools/verify-deploy.ps1` exit 0 on the redeployed build, `v-verify-deploy-2.log`;
  the pre-fix `040c355b` deploy it replaced is visible in `v-deploy.log` / `v-verify-deploy.log`)
- Run: 2026-10-01 19:20 → 19:26 · Host: physical machine · Guest: the primary sandbox · Third client:
  the alternate sandbox (its own account)
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `sandbox-alt`, `hotrepl`, `logs`, `artifacts`,
  `capture`, `input` (preflight: 11 present, exit 0 — `v-preflight-final.txt`, re-run after the redeploy)
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir` (`batch-v`)

## The capability this run adds

- `tools/acceptance/recipes/world-time-request.cs` (landed `0a62fb96`): sends one `WorldTimeRequest`
  through the production send a guest's own report uses (`IWorldTimeControl.SendRequest` ->
  `WorldTimeChannel.SendRequest`; the private `WorldTimeSync.BeginLocalFirst` local-first half itself is
  not reachable from the evaluator), with no `PlayerCamera` needed. Its first cut ended at the
  `Func<string>` cast and returned the delegate, so the driver's parse failed before any row could be
  judged; the live smoke found it and the invocation was added in `0a62fb96`.
- `tools/acceptance/recipes/gate-arm.cs` (landed with this record): re-arms the production start gate
  (`IWorldControl.StartStartGate`) once a member has left the world, so the hold the guard reads exists.

## Verdicts

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 4a | Invalid request — not in world | machine | **pass** | `v-alt-request-not-in-world.json` (third client: `sent` true, `sessionActive` true, `inWorld` false) → `v2-host-latest.log` 19:21:38.591 `[WorldTime] refused request from … (not an in-world member) — answered with the authoritative Normal.`, and the answer `v2-alt-latest.log` 19:21:38.608 `World-time broadcast: Normal.` |
| 4b | Invalid request — start gate | machine | **pass** | `v2-alt-leave-world.json` (`inWorld` false) and `v2-gate-arm.json` (`armed` true, `startGateActive` true, `remainingMs` 30000) hold a real gate; `v-guest-request-at-gate.json` (`sent` true, `inWorld` true) → `v2-host-latest.log` 19:24:10.292 `Start gate armed — waiting for 1 member(s) to finish loading.`, 19:24:18.498 `[WorldTime] ignored Fast request from … — the start gate owns the world clock.`, released by the gate's own fallback at 19:24:40.301 `Start gate forced after 30 s …`; the answer is `v2-guest-latest.log` 19:24:18.526 `World-time broadcast: Normal.` (28 ms after the refusal) |
| 4c | Invalid request — bad speed | machine | **pass** | `v-guest-request-bad-speed.json` (`requested` `unconsciousfast`, `sent` true, `gateWaiting` false) → `v2-host-latest.log` 19:25:10.839 `[WorldTime] refused invalid guest request UnconsciousFast from … — answered with the authoritative Normal.`; the answer is `v2-guest-latest.log` 19:25:10.870 `World-time broadcast: Normal.` (31 ms after the refusal); `v-host-time-scale-read.json` and `v-guest-time-scale-read.json` both read `timeScale` 1 / `curTimeScale` `Normal` |
| 5 | Build, tests, gates, format | machine | **pass** | `v-build-final.log` 0 warnings / 0 errors; `v-focus-gate-2.log` `AcceptanceDriverGateTests` 4/4; `v-gates-final.log` normative gates 300/300; `v-full-suite-final.log` full suite with build 4,573 + 300, 0 failed; `v-format-final.log` exit 0 |

## What this run leaves open

Nothing on this ticket. The setup's own force is recorded above: the start-gate instance's hold is a
re-arm of the production gate (a member left the world), not a slow loader's natural window.

## Limits

- The three judged requests travel the production send path; the OS key press itself is not synthesizable
  (batch u's limit stands).
- The gate hold lasts 30.0 s: one attempt sent 26 s after arming was still answered by the gate guard
  (`v2-host-latest.log` 19:24:36.174) — kept as the timing evidence, not as a judged instance; the judged
  line is the one sent 8 s after arming.
- The requester's `World-time broadcast` lines share their channel with the host's 5 s resend; each judged
  answer is the line named with its timestamp beside the host's refusal.
- Rows are judged from this run's probes and logs against the deployed artifact; no earlier cycle's
  numbers are reused.

## Residuals for the user

None.
