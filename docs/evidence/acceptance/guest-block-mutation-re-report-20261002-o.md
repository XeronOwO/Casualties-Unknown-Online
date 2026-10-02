# Acceptance record — Guest world-block mutations have no periodic re-report

- Ticket: `guest-block-mutation-re-report` — verdict: **moved to `docs/backlog/done/`** — every row of
  the acceptance matrix is judged and passes (rows 1–4, 6, 8, 9, 10 here; row 5 in batch `20261002-k`,
  row 7 in batch `20261001-y`)
- Batch: `20261002-o` — tickets `guest-block-mutation-re-report` (single-ticket batch)
- Commit under acceptance: `a699630206795b32facf1310066a71d2cd8415ef` (the session's deployed build;
  this batch adds acceptance recipes and records only, no product source) · Deployed artifact verified
  after the batch: ProductVersion `0.1.0+f6aa2162dde02c11301b41db2c420beb30aa3b70`
  (`verify-deploy.ps1` exit 0). The commit that carries this record is one commit later than that stamp
  (a record cannot name its own commit); the deployed assemblies are unchanged across every one of them,
  and the driver reads the recipes from disk at run time.
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

The rows 6 and 10 verdicts come from a second session the same evening (the three clients relaunched
against the same deployment, a fresh lobby and run): the blackout status was re-read `armed=false`
before its windows and `mode=off` restored `subscribersAfter=1` after each, and the member's pending
counters started and ended at `0/0/0`.

## Verdicts

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest mines a block; its report is swallowed | machine | pass | guest broke `(512,967)` `8 → 0` inside the window, guest pending `block` stayed `≥1` after disarm, then `0` with the guest log's `[BlockSync] re-reported 2 unacknowledged block mutation(s) to the host.`; host log `[BlockBreak] …remote damage broke the block at (512,967) without an air-write report…` and all three clients read `(512,967)=0` (`o-row1b-*.json`, `o-host-log-fragments.txt`, `o-cap-log-guest.txt`) |
| 2 | Guest places a block; report swallowed | machine | pass | guest wrote `(510,968)` `0 → 2` inside the window, guest pending `block` held `2`; on re-read host, guest and the third client all read `2` — the swallowed placement converged; a later host quake removed the cell (`o-row2-*.json`, `o-row2-cell-*.json`) |
| 3 | Guest air write (earthquake); report swallowed | machine | pass | host fired a real quake (`[Earthquake] host quake started (23.7s…) — broadcasting`), the guest's own quake state read `earthquakeIntensity=0.301` while the window was armed, guest pending `block` rose to `8`; after the window four sampled cells read identically on host, guest and third client (`o-row3-*.json`, `o-quake-consensus.txt`, `o-host-log-fragments.txt`) |
| 4 | Host writes the same cell after the guest's swallowed write | machine | pass | host wrote `(510,966)=3` first (guest applied the relay); the guest's `=2` write inside the window diverged it to `2` while the host stayed `3`; when the fallback re-reported, the host's value stood and the guest converged to `3` — host `3/3/3`, guest `3`, third client `3`, pending drained to `0`, no oscillation in repeated reads (`o-row4b-*.json`, `o-row4c-*.json`) |
| 6 | Guest reconnect | machine | pass | marked cells `(511,1004)` (guest break) and `(512,1004)` (host break) were air on all three before the re-entry; the guest then ran `leave-world` → `home.leave` → `join-lobby` the same id, the host log carries `Handshake … ignored: not a lobby member` → `Peer … reconnected — presence reused` → `Handshake confirmed end-to-end`, and after the re-entry the same two cells read `0` on host, guest and the third client — the mined cells were not resurrected (see the transient note below the table) (`o2-row6-*.json`, `o2-row6-post-reentry-compare.txt`) |
| 8 | Table cap and its overflow log | machine | pass | the isolated `GuestBlockReportBookkeeping` instance reached the product's own cap: `filled=65536, atCap=65536, refused=true, latch=true, updateExisting=true, cleared=0, latchAfterReset=false`, and the client's real log carries the full-table warning and the reset account line (`o-cap-probe-guest.json`, `o-cap-log-guest.txt`); the evidence is the isolated instance, not the live table |
| 9 | Solo → lobby → join, exactly once | machine | pass | the host started a run with no lobby (solo) and broke `(514,512)` (`1 → 0`); it then created the lobby from inside the world and the guest joined. The host's `Continue` restored the same world from its live snapshot (`Continue restored world w-20261002-db06: mid-run cut … revision 590 … 0 world-block row(s)`), so the world entry the row needs came from the member's own entry: the host log carries `Sending world-entry snapshot group to 76561199526807662.` **exactly once** (one such line in the whole log) and `PlayerJoin sent: local … member 76561199526807662`, and both members' logs carry the same `Generation stream reset to captured baseline (16 bytes: 5BC63874ED935DFC798E76021AB1F8E4)` and `Applied host world params`. The solo-marked cell then reads `0` on host, guest and the third client, i.e. the accumulated difference is in the baseline the joiners adopted (`o3-solo-*.json`, `o3-guest-entry-timeline.txt`, `o3-alt-state.json`) |
| 10 | Partial damage then break, both swallowed | machine | pass | read immediately after the crack (with the host still armed), the guest reported `damage=1` pending while the host and the third client still held the undamaged cell — the crack's report was genuinely swallowed; the guest broke the cell inside a second window, the guest's pending then showed `block=1`, and after the fallback both markers drained to `0` with all three clients reading `(509,1004)=0` (`o2-pending-after-crack.json`, `o2-row10-*.json`, `o2-row10-pending-final.json`) |

Between the two windows of row 10, the swallowed partial report healed on its own: with the host armed
the first read of `(509,1004)` showed host `block=8, damage=-1` against the guest's `damage=40`, and a
later read showed the host holding `block=8, damage=40` — the fallback re-reported the crack and the host
adopted it before the break. This is the reading the first session took too late: it read the damage
counter only after the follow-up break had already cleared the row (the cell had become air, which is
why the same probe had reported `damage=0` there), and the row was recorded `unproven` on that misread.
The second session's read immediately after the crack is the one that names the path.

Row 6's re-entry has one transient worth naming: read immediately after `join-lobby` returned, the two
marked cells showed `host=0 / guest=2 / alt=0` — the guest's regenerated world still held its own
generated block while the host's table already held the mined state. The host's `World join sent to 1
member(s) (… baseline follows: True)` fan-out then delivered the snapshot, the guest's pending counters
were `0/0/0`, and the full 11 × 6 grid comparison (`o2-row6-post-reentry-compare.txt`) reads identical on
all three clients — including both marked cells. A re-entry row must therefore take its verdict read
after the snapshot applied, not on the join call's own answer.

## Residuals for the user

None. Every verdict above is from this run's own probes, logs and readings; no row was handed to the
user to judge.

## Limits

- **Row 10's first reading was a timing error, not an instrument gap.** The pending-damage counter the
  run already had does expose the swallowed crack (`damage=1`), but only while the crack still exists:
  the first session's read came after the follow-up break had cleared the cell's damage row, so the
  counter read `0` and the row was recorded `unproven`. The second session read it immediately after the
  crack and named the path. The lesson is about read order, and the counter needs no new probe.
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
- **Row 9's exactly-once denominator is the entry send, not the world's cell values.** The row was
  staged in a third session: the host played solo first (a run started with no lobby), marked a cell,
  created the lobby from inside the world, and the members joined afterwards. Because the host's
  `Continue` restored the world from its live snapshot, the marked cell's absence is also consistent
  with the restored baseline, so the cell read is supporting evidence at best. What the verdict rests on
  is the send side: `Sending world-entry snapshot group to <member>.` appears **once** in the host's log
  for the member that joined, and the member's log shows it adopted the host's generation baseline and
  params. An acceptance run that wants to count applies must add a per-member apply counter; that gap is
  named here rather than hidden.
- **A member that joins while the host is already in the world waits for a world entry.** The host's
  first world-entry of this session read `World join sent to 0 member(s)` even though a member was in
  the lobby; the snapshot reached the member when its own entry edge fired (`World join received —
  starting a run to follow`). This is the same shape batch `20261002-k` recorded, and it is why the
  row's staging needs the members' entries, not only the host's.

## Batch closing state

- The batch ran in three sessions of the same evening (rows 1–4 and 8; rows 6 and 10; row 9); every
  session was launched and quit by the run, and the machine was left with zero game processes each time.
- Every row of the ticket's acceptance matrix now has a pass: rows 1–4, 6, 8, 9 and 10 here, row 5 in
  batch `20261002-k`, row 7 in batch `20261001-y`. The ticket moves to `docs/backlog/done/` with this
  record's batch named in its status line, and the backlog index row moves in the same change.
- Everything this batch produced is committed and pushed: the three recipes, the scope page, this
  record, the ticket's transition and attempt section, and the lessons. The full gate set ran green
  before each commit (build 0 warnings, 4 610 + 315 tests, `format` clean), and the deployment was
  re-verified against the pushed tree.
