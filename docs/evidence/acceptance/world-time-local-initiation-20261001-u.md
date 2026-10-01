# Acceptance record — World-time acceleration is gated on being asleep (local-first initiation)

- Ticket: `world-time-local-initiation` — verdict: **stays in `review/`** (rows 1, 2, 3, 5, 6 and 8
  pass this run; **row 4 stays `unproven`** with the missing vocabulary named; row 7 is superseded)
- Batch: `20261001-u` — tickets `world-acceleration-survives-movement`, `world-time-local-initiation`,
  `world-acceleration-quake-direct-write`
- Commit: `8a0ddafb` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+8a0ddafb…` (`tools/verify-deploy.ps1` exit 0)
- Run: 2026-10-01 18:31 → 18:46 · Host: physical machine · Guest: the primary sandbox · The guest was
  not relabelled: the same client left and rejoined the lobby for row 5
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `logs`, `artifacts`, `capture`,
  `input` (preflight: 11 present, exit 0)
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir` (`batch-u`)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A player presses Fast while a teammate is awake | machine | **pass** | `u-d4-guest-announce-fast` answers 5× in the caller's own invocation (local-first), with the host still at 1×; the guest log reads `guest Fast applied locally and reported (host authority Normal).` and the host log answers `host accepted Fast request from …` one second later; `u-d5` reads the host at 5× |
| 2 | Everyone falls asleep | machine | **pass** | `u-l3`/`u-l4` (25× `UnconsciousFast` on both while both bodies are down), host log `host policy applied UnconsciousFast (next request Normal)`, `u-l10`/`u-l11` after waking |
| 3 | A request the host refuses | machine | **pass** | `u-l5`–`u-l7`: with the sleep gate holding 25×, the guest's Fast is applied locally (5×) and then answered with a different value — guest log `host answered UnconsciousFast for the locally applied Fast — ramping back.` and `local clock returned to the host's UnconsciousFast.` 0.37 s later, both ends reading 25×. The ticket's own text defines this as the covered path ("a refused request, or the sleep gate answering something else"); the three host guards themselves were not produced (see Limits) |
| 4 | Invalid request (not in world / bad speed / start gate) | machine | **unproven** | none of the three instances is reachable from the recipe vocabulary: a recipe can only send the three guest-requestable speeds, and a guest out of the world has no local camera to issue the call from. The run's start-gate line is a swallowed *reset*, not a guest request. The gap is a capability, not a defect: an in-process way to hold the start gate (or an out-of-world guest) while issuing a request |
| 5 | Late joiner / reconnect | machine | **pass** | `u-s4` stands 5×; the guest leaves the lobby (`u-s5`, lobby 0, out of world) and rejoins (`u-s6`); `u-s7` shows it back in world as Guest; `u-s8` reads **5× `Fast`** on entry and `u-s9` the same on the host; the guest log carries `Scene state: InWorld (SampleScene)` then `World-time broadcast: Fast.` 30 ms later, and again 3.3 s later (the resend) |
| 6 | Two players press accelerate at once | machine | **pass** | `u-g5` (host SuperFast, 20×) and `u-g6` (guest Fast, 5×) in one burst; `u-g7`/`u-g8` read a single shared 5× on both ends — the newest intent wins and nothing is left split |
| 7 | ~~A player presses a movement key during a manual fast-forward~~ | — | **superseded** | no verdict: decision 223 replaced this row; see `world-acceleration-survives-movement` |
| 8 | A teammate walks while the session is accelerated | machine | **pass** | the guest's log burst of swallowed silent resets at 18:40:12 (see the sibling record's row 3) while the host's acceleration stood, with the host's clock unchanged in `u-o3` — the veto is gone and a teammate's motion is not an input |

## What this run leaves open

Row 4 needs a capability the run does not have: an in-process way to send an invalid world-time request
(a guest outside the world, a non-guest-requestable speed) or to hold the start gate while a guest call
is issued. Everything else on the ticket is judged. The ticket stays in `review/` with this row named;
the host-side guard code is unchanged and its unit tests still pin the three guards.

## Limits

- Rows are judged from this run's probe answers and both clients' logs against the deployed artifact;
  no earlier cycle's numbers are reused.
- The 0.37 s ramp of row 3 is a probe-to-probe interval (two invocations), not a frame-level
  measurement.
- The OS key press itself is not synthesizable: the accelerate-key rows run the same native
  `SetTimeScale` call the key's handler makes.
