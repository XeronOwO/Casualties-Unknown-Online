# Acceptance record — Guest world-block mutations have no periodic re-report

- Ticket: `guest-block-mutation-re-report` — verdict: stays in `docs/backlog/todo/` with
  `- Status: Todo — Rejected (batch 20261002-o: rows 6, 9 and 10 unproven; rows 1–4 and 8 pass)`
- Batch: `20261002-o` — tickets `guest-block-mutation-re-report` (single-ticket batch)
- Commit under acceptance: `a699630206795b32facf1310066a71d2cd8415ef` (the session's deployed build;
  this batch adds acceptance recipes and records only, no product source) · Deployed artifact after the
  batch's own redeploy: ProductVersion `0.1.0+1334927bfdb45c7aee120f74e144eca21b6ed805`
  (`verify-deploy.ps1` exit 0). The commit that carries this record is one commit later than that stamp
  (a record cannot name its own commit); the deployed assemblies are unchanged across all three, and the
  driver reads the recipes from disk at run time.
- Run: 2026-10-02 22:00 → 22:40 (+08:00) · Host: physical machine · Guests: two Sandboxie sandboxes
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `sandbox-alt`, `hotrepl`, `capture`, `input`,
  `logs`, `artifacts` (preflight 11/11 present)
- Artifacts: `<batch>/o-*.json` probe results, `o-host-log-fragments.txt`, `o-quake-consensus.txt`,
  `o-cell-scan*.txt`, `o-cap-log-guest.txt` in the directory named by acceptance-artifacts-dir
- Recipes this batch added: `tools/acceptance/recipes/block-read-at.cs` (one absolute cell's block and
  damage), `block-set-at.cs` (one absolute cell's write through the game's own `SetBlock`),
  `block-break-at.cs` (one absolute cell's native damage roll) — the relative `block-read`/`block-hit`
  cannot address a fixed cell once the three bodies drift apart.

## Capability spikes (runbook §1)

| Spike | Result |
|---|---|
| S1 `net-receive-blackout mode=status` (host) | pass — `ok=true, subscribers=1, armed=false, parked=false` (`o-blackout-status.json`) |
| S2 blackout `on` / state / `off` (host) | pass — `subscribersAfter=0, parked=true` while armed, `session active` and `steamInit=true` in the armed state, `subscribersAfter=1, parked=false` after `off` (`o-blackout-on.json`, `o-host-state-armed.json`, `o-blackout-off.json`) |
| S3 `block-report-pending-count` (all three) | pass — resting `block=0, damage=0, drops=0` before and after the cap probe (`o-pending-*.json`, `o-pending-after-cap-*.json`) |
| S4 `block-report-cap-probe` (guest) | pass — `cap=65536, filled=65536, atCap=65536, refused=true, latch=true, updateExisting=true, cleared=0, latchAfterReset=false`; the guest log carries the once-per-episode `[BlockSync] pending block-report table is full (65536 cells) …` warning and `[BlockSync] cleared 65536 pending block report(s) …` (`o-cap-probe-guest.json`) |

## Verdicts

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest mines a block; its report is swallowed | machine | pass | guest broke `(512,967)` `8 → 0` inside the window, guest pending `block` stayed `≥1` after disarm, then `0` with the guest log's `[BlockSync] re-reported 2 unacknowledged block mutation(s) to the host.`; host log `[BlockBreak] …remote damage broke the block at (512,967) without an air-write report…` and all three clients read `(512,967)=0` (`o-row1b-*.json`, `o-host-log-fragments.txt`, `o-cap-log-guest.txt`) |
| 2 | Guest places a block; report swallowed | machine | pass | guest wrote `(510,968)` `0 → 2` inside the window, guest pending `block` held `2`; on re-read host, guest and the third client all read `2` — the swallowed placement converged; a later host quake removed the cell (`o-row2-*.json`, `o-row2-cell-*.json`) |
| 3 | Guest air write (earthquake); report swallowed | machine | pass | host fired a real quake (`[Earthquake] host quake started (23.7s…) — broadcasting`), the guest's own quake state read `earthquakeIntensity=0.301` while the window was armed, guest pending `block` rose to `8`; after the window four sampled cells read identically on host, guest and third client (`o-row3-*.json`, `o-quake-consensus.txt`, `o-host-log-fragments.txt`) |
| 4 | Host writes the same cell after the guest's swallowed write | machine | pass | host wrote `(510,966)=3` first (guest applied the relay); the guest's `=2` write inside the window diverged it to `2` while the host stayed `3`; when the fallback re-reported, the host's value stood and the guest converged to `3` — host `3/3/3`, guest `3`, third client `3`, pending drained to `0`, no oscillation in repeated reads (`o-row4b-*.json`, `o-row4c-*.json`) |
| 6 | Guest reconnect | machine | unproven | not staged in this run — the in-place re-entry shape (member leaves, host invites the same world back) was not executed; see Limits |
| 8 | Table cap and its overflow log | machine | pass | the isolated `GuestBlockReportBookkeeping` instance reached the product's own cap: `filled=65536, atCap=65536, refused=true, latch=true, updateExisting=true, cleared=0, latchAfterReset=false`, and the client's real log carries the full-table warning and the reset account line (`o-cap-probe-guest.json`, `o-cap-log-guest.txt`); the evidence is the isolated instance, not the live table |
| 9 | Solo → lobby → join, exactly once | machine | unproven | not staged in this run — no solo segment was created; see Limits |
| 10 | Partial damage then break, both swallowed | machine | unproven | the guest rolled `40` (block stayed `9`, damage row `40`) and then `999` (block `9 → 0`) inside the window, and the break's report did converge afterwards (host log names `(509,965)`), but the guest's pending counter never showed a partial-damage entry (`o-row10-damage.json`, `o-row10-break.json`, `o-row10-pending.json`) — the single-partial-hit path into the damage re-report is not proven by these reads |

## Residuals for the user

None. Every verdict above is from this run's own probes, logs and readings; no row was handed to the
user to judge.

## Limits

- **Row 10's partial-damage reporting needs its own instrument.** A single `DamageBlock` roll (`40`)
  left `damage=40` on the guest's cell but produced no entry in the counters the run's pending probe
  reads (`block/damage/drops` all `0` after the window). The damage table's own accounting may attach
  at a different point than this probe exposes; the run could not tell "no pending entry exists" from
  "the entry lives elsewhere", so the row stays unproven rather than failed.
- **The guest-side answer line is Debug; the re-report line is not.** The guest log did carry
  `[BlockSync] re-reported 2 unacknowledged block mutation(s) to the host.` and a later `re-reported 4 …`
  at the default `Information` level (see `o-cap-log-guest.txt`), but `[BlockSync] host answered (x,y) —
  dropped the pending report` is written at Debug and did not appear. The drain was read behaviourally
  (counters returning to zero, three-end convergence, the host's own `BlockBreak` line); a run that wants
  the per-cell answer line must set the log level live first.
- **Row 8's evidence is an isolated instance by design.** A real 65 536-cell fill would arm the fallback
  pump and put 65 536 reports on the wire; the probe used the product's own bookkeeping type and its
  real logger. A review that wants live-table evidence needs its own instrument.
- **The blackout is a blunt window.** It drops every inbound frame on the armed client for its duration;
  every window in this run was a few seconds, was opened immediately before the marker writes and was
  disarmed immediately after, and S3 was re-read after the S4 probe to show the live pending table was
  left at zero. Other domains' healing during a window was not separately observed.
- **The recipe files ride the tree, not the plugin.** The session ran against the deployment stamped
  `a6996302`, which is the commit that staged the three recipes this run used (`net-receive-blackout`,
  `block-report-pending-count`, `block-report-cap-probe`); the driver reads recipes from disk at run
  time, so a recipe edit never changes a plugin assembly. This batch's own three probes
  (`block-read-at`, `block-set-at`, `block-break-at`) were used from the working tree during the run
  and are committed here; the run's behavior is therefore reproducible from this commit even though the
  deployed assembly bytes are unchanged. The acceptance driver gate passes with all recipes (4/4).
- **Rows 6 and 9 were not staged.** This run focused the session on the swallow/report family and the
  capability spikes. The in-place re-entry shape and the solo→lobby→join exactly-once shape remain
  candidates to stage in a following session, as the runbook itself records.

## Batch closing state (for the next session)

- The three clients were left running for the next session's continuation; they were launched by this
  run and may be quit with the driver's `quit` action.
- The tree carries uncommitted acceptance work: the three new recipes, the batch scope page, this
  record, and the ticket/index transition. The full gate set had not been run at the time of writing.
