# A Steam send-limit refusal floods the log and wedges the host main thread

- Status: Review (code landed 2026-10-01; batch `20261001-r` judged rows 1–4, 6 and 7, and batch
  `20261001-s` re-observed row 6 while trying the dense-load path — row 5, the live 30 s escalation,
  stays `unproven`: a peer that stops draining does not back-pressure a healthy link and the guest's
  own watchdog ends that session first, so the ticket stays open)
- Priority: High
- Category: Networking / Steam transport
- Source: observed live by acceptance batch `20261001-q` (Run E, agent-run, 2026-10-01); not
  user-reported
- Acceptance record: `docs/evidence/acceptance/steam-transport-send-limit-runaway-20261001-r.md`,
  `docs/evidence/acceptance/steam-transport-send-limit-runaway-20261001-s.md`
- Related: `docs/evidence/acceptance/save-run-clock-not-sent-20261001-q.md`,
  `docs/evidence/acceptance/save-mid-run-consistent-cut-20261001-q.md`,
  `docs/architecture/` (transport), decision records for the session/transport split

## What landed

- The send path decides through `PeerSendRefusalPolicy` behind `ISteamSendChannel`: congestion gates the
  peer (250 ms doubling to a 2 s cap), refusals aggregate to one line per 5 s plus one detailed line per
  episode, a success reports its recovery once, and a peer that keeps refusing raises
  `ISendStallSource.PeerSendStalled` once per episode. Non-congestion kinds stay un-gated (a send is how a
  broken session is re-driven) but still aggregate.
- The session answers the edge in `PeerSendStallWatchdog`: the host drops that member with a named reason
  and keeps playing, a guest ends its own session when its own sends stall towards the host;
  `GuestHostSilenceWatchdog` ends a guest's session after 15 s without a host frame. Both publish a
  rendered line the Online UI shows on its delayed status line.
- Red first: with the pre-fix behaviour restored the regression drive made 10 000 channel calls and 10 000
  log lines for 10 000 attempts; with the policy it makes at most 20 of each.
- The live half: refusals are real (`k_EResultLimitExceeded … QueueFull`), bounded (≈200 kB of log over
  six minutes of intermittent refusal), the host never stopped answering, and the rate governor reached
  `pressure Critical` and stretched every adapted stream.

## What remains

- Row 5's live escalation needs a ≥30 s **continuous** refusal episode. Batch `20261001-s` tried again
  with the run's own next step — a dense in-run animal population (589 animals, ≈200–250 kB/s) — plus
  three reversible injections: an organic dense load, the guest's transport pump stopped, and the
  guest's process pinned to one of twenty cores. None produced a single `k_EResultLimitExceeded`: with
  the pump off the host pushed >20 MB into that peer without a refusal (the guest's own silence
  watchdog ended that session after 15 s), and the CPU-starved guest still drained ≈200 kB/s while
  staying in the world. The record's Limits names the mechanism: a peer that stops reading does not
  back-pressure a healthy Steam link, and the streams that carry the load are budgeted
  (`EnemyStateBroadcast` 256 kB/s), so the host's send rate stays inside the guest's drain capacity.
- Next attempt: a peer whose *transport* stops acking while the session stays connected — that is the
  shape the escalation is written for. On this machine the only producer seen is process suspension,
  which collapses the link to `Connecting` and lets the automatic restart clear the episode
  (batch `20261001-r`); a Debug-build refusal injection would judge the session half but not this
  trigger.

## What happened

- During Run E (host + guest; the world's cut that day carried 1783 world-block rows, 301 item rows,
  678 enemy rows — 673 live plus 5 removed —, 243 fluid chunks, 31 world-entity rows and 2 characters,
  revision 1949), the host's rolling log filled with one line per send attempt:
  `SendMessageToUser … failed: k_EResultLimitExceeded (Other); session state:
  k_ESteamNetworkingConnectionState_Connected` — thousands of lines per second.
- The log file reached **1342 MB and grew ≈7 MB/s**, the host's in-process
  evaluator stopped answering (eval calls timed out after 23 s), and the run had to stop the host
  process to protect the machine. The guest stayed responsive and quit cleanly.
- The same window's traffic monitor shows the scale: `Send EnemyStateStream=1910911B/25f
  (p50 ≈76 KiB/frame)`, `Send StateStream=109195B/18f`, plus `BlockPlaced`, `BuildingEntityDamaged`,
  `FluidRegionFull` and per-frame committed batches.

## Why it matters

- A reachable world shape (a dense-animal layer) can wedge a session and consume unbounded disk while
  the send path keeps retrying a refused send; the acceptance driver shares the stalled main thread, so
  the session cannot even be closed through its own channel.
- The player-visible effect is a frozen host with a filling log, not a clean error.

## What to check first

- `k_EResultLimitExceeded` handling on the Steam send path: is the send retried every frame with no
  backoff, no queue bound and one log line per attempt (rather than an aggregated `Warn`/`Error`)?
- The `EnemyStateStream` frame size above: is there a stream-level cap, coalescing or degradation step
  for dense layers, and does the adaptive rate governor see this stream?
- Whether a refused send should degrade the session (throttle, then disconnect with a named reason)
  instead of spinning.

## Acceptance matrix (batch `20261001-r`)

| # | Row | Class | How it is judged |
|---|---|---|---|
| 1 | Sustained refusals produce bounded logging | machine | a fake send channel that always answers `k_EResultLimitExceeded`: 10 000 `SendTo` calls over 10 s of virtual time produce a handful of log entries (one detailed diagnostic per episode plus at most one aggregate per 5 s), never one per attempt |
| 2 | Refused attempts are throttled | machine | the same drive makes ≤ 20 real channel send calls (exponential backoff 250 ms → 2 s), never one per `SendTo` |
| 3 | Recovery resumes and is announced once | machine | the channel starts answering OK: the next allowed attempt leaves and exactly one recovery line names the episode duration and refusal count |
| 4 | Congestion stays visible to the rate governor | machine + logs | refusals remain recorded failed sends (`PacketSender`), so `AdaptivePressureClassifier` reaches Critical and the adapted streams stretch toward their floor; judged from the live run's `[AdaptiveSync]` / traffic lines |
| 5 | Host escalation | machine + residual | refusals still arriving across a ≥ 30 s episode raise the stall edge once: the member is removed with a named reason through the kick path, the host's session stays active, and the host HUD status line carries the reason (the status-line text is read from the plugin's own notify path) |
| 6 | Guest convergence | machine | ≥ 15 s without any frame from the host ends the guest's session locally, with its own notice; a normal run never trips it |
| 7 | Regression | machine | an ordinary two-client run (join, play, leave) shows no refusals and no watchdog action, and the full gates stay green |

Trigger for rows 1–3: unit tests, deterministic. Rows 4–6: live two-client run whose refusal window is produced by blocking the guest's main thread through the HotRepl eval surface (deterministic; fallback is the Run E dense world). Every live step re-checks the host log size and eval latency before continuing.

## Limits recorded before acceptance (independent review, batch `20261001-r`)

- **The escalation is one-shot per congestion episode.** After a stall drop the peer is no longer a
  handshaken member, yet the warm-up pump keeps probing it, and those refusals keep the episode alive:
  a peer that *rejoins* while still saturated is not dropped again for that same streak. The guest's
  own watchdogs end its session instead (its sends stall or the host goes silent), so the host does not
  keep a dead guest forever.
- **"≥ 30 s of refusal" is a recency window, not a stopwatch on a quiet link.** The escalation needs an
  episode at least the stall bound old *and* a refusal inside that bound, so a send path that goes
  silent for the bound does not escalate (verified: one refusal then 1000 s idle → no escalation). A
  refusal at t=0 plus one at t=29 999 therefore escalates at t=30 001 — reachable only if the path was
  quiet in between.
- **Bounded, not negligible.** A permanently stuck peer costs ≈0.5 real send attempts/s (the 2 s
  backoff cap) and one aggregate `Warn` per 5 s per peer (measured: 718 lines / 3 598 197 suppressed
  attempts in one hour ≈ 17 k lines/day, ≈0.1 MB/h) — six orders below the 7 MB/s this ticket removed,
  but a host that keeps a stuck peer for days pays for it.
- **A backwards clock jump rebases the policy.** `Environment.TickCount` wraps after ~24.9 days of
  machine uptime; the stored deadlines are dropped on that jump instead of being read as an enormous
  delay (the pre-review failure mode: gated for tens of days with no report and no escalation).
- The IP-direct transport is out of this change: its write blocks on the socket and a failure closes the
  peer, so the refusal-flood shape is Steam-only (recorded, not assumed).

## Evidence

- Batch `20261001-q` artifacts (under the directory named by `acceptance-artifacts-dir`):
  `e-runfacts-lines.txt`, `e-host-log-leave-with-guest.txt`, `e-host-cut-line.txt` (cut revision 1949)
  and the archive row counts the S3 record cites.
- The host's 1342 MB log was deleted after the incident; the lines quoted above are the captured
  excerpts.
