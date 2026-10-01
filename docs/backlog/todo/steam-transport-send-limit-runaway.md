# A Steam send-limit refusal floods the log and wedges the host main thread

- Status: Todo
- Priority: High
- Category: Networking / Steam transport
- Source: observed live by acceptance batch `20261001-q` (Run E, agent-run, 2026-10-01); not
  user-reported
- Related: `docs/evidence/acceptance/save-run-clock-not-sent-20261001-q.md`,
  `docs/evidence/acceptance/save-mid-run-consistent-cut-20261001-q.md`,
  `docs/architecture/` (transport), decision records for the session/transport split

## What happened

- During Run E (host + guest; one world at layer 1 holding 1783 world-block rows, 301 item rows, 673
  live enemies + 5 removed, 243 fluid chunks and 664 `shadecrawler` `BuildingEntity` instances), the
  host's rolling log filled with one line per send attempt:
  `SendMessageToUser … failed: k_EResultLimitExceeded (Other); session state:
  k_ESteamNetworkingConnectionState_Connected` — thousands of lines per second.
- The log file reached **1342 MB and grew ≈7 MB/s** (14.35 MB measured in 2 s), the host's in-process
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

## Evidence

- Batch `20261001-q` artifacts (under the directory named by `acceptance-artifacts-dir`):
  `e-runfacts-lines.txt`, `e-host-log-leave-with-guest.txt`, `e-host-cut-line.txt` (cut revision 1949)
  and the archive row counts the S3 record cites.
- The host's 1342 MB log was deleted after the incident; the lines quoted above are the captured
  excerpts.
