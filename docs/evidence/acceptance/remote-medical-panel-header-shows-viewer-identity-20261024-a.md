# Acceptance record — the remote medical panel's identity header

- Ticket: `remote-medical-panel-header-shows-viewer-identity` — verdict: **moved to `done/`** (rows 1-6 pass)
- Batch: `20261024-a` — this run is the batch's whole scope
- Commit: `da45cdd9` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `0.1.0+da45cdd93e4c328ac6fb0aa89f88618b6a0378c4` (`deploy`/`verify` exit 0 before the clients started, `prelude.log`)
- Run: 2026-10-20 19:03 → 19:12 · Host: physical machine · Guest: sandbox 1 · Third client: sandbox 2
- Dependencies: full preflight (`RESULT: OK`, 11 present), three clients, in-process probes, per-client logs — no frame capture and no OS input (every row is a value row)
- Artifacts: `.acceptance/20261024-a/` — `preflight.log`, `prelude.log`, the `r0-*` … `r5e-*` probe answers and the `m-*` log marks cited per row; the cycle's build evidence is `format-2.log`, `gates-4.log`, `full-3.log`, and the blank-order fix's red→green pair `red-header-blank-order.log` / `focused-header-tests.log`

## What the run judged

The header is judged from the native panel's own fields, read in-process in one pass
(`panel-header.cs`): the two labels, the four ints behind them (`cInfo`), which `Body` the panel
shows, the focus state and target, and `ownInfo` — the identity this client's own 1 Hz snapshot
reports (`RemoteMedicalHeader.OwnIdentity`, the production read). Every client's own values were
read before the rows (`identity-force mode=read`, plus the panel read): host `153,11,48636,2`
(`153cm | 11y | #48636`, v2.02), guest `146,23,96520,2`, alt `151,20,62120,2` — three different
identities, so every row below distinguishes the viewed player from the viewer.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest opens the host's panel | machine | **pass** | `r1-guest-panel.json`: `viewOpen=true`, `target=<host>`, `cInfo=153,11,48636,2`, `bodyStatText='153cm \| 11y \| #48636'`, `versionText='UNI-HEALTH v2.02'` — the host's own values — while `ownInfo=146,23,96520,2` stays the guest's own; the guest log carries `opened native medical view for <host>` and `panel identity header shows the viewed player's own values (153cm \| 11y \| #48636 \| UNI-HEALTH v2.02).` at 19:07:10.768 (`m-r1.txt`, `r1-guest-open.json`, `r1-host-panel.json`: the host's own panel is unchanged) |
| 2 | Close the remote focus | machine | **pass** | Production close (`medical-view-close`): `r2-guest-close.json` `wasOpen=true, openAfter=false`; `r2-guest-panel.json` `cInfo=146,23,96520,2`, `bodyStatText='146cm \| 23y \| #96520'`; log `panel identity header returned to the viewer's own values (146cm \| 23y \| #96520 \| UNI-HEALTH v2.02).` at 19:07:18.285. Two further closes repeat it: the host's close while the viewed player was already dead (`r4-host-closed.json`, log 19:08:53.754 → `153cm \| 11y \| #48636`) and the alt's (`r5e-alt-closed.json`, log 19:11:38.583 → `151cm \| 20y \| #62120`) |
| 3 | Both directions and the third-party view | machine | **pass** | Direction B: the host views the guest — `r3-host-panel.json` `cInfo=146,23,96520,2`, texts `146cm \| 23y \| #96520`/v2.02, `ownInfo=153,11,48636,2` (log 19:07:25.994); the guest's own panel and the alt's stay their own (`r3-guest-panel.json`, `r3-alt-panel.json`). Third party on the guest: `r3c-alt-panel.json` `cInfo=146,23,96520,2`, `ownInfo=151,20,62120,2` (log 19:08:11.245). No viewer ever read its own values |
| 4 | A loaded run | machine | **pass** | The host left the world and continued (`r4d-host-leave.json`, then `continue-run`): the host's own restore is logged (`Queued this run's local character restore` 19:10:25.912 → `Character restore position applied at spawn` 19:10:36.541 → `Applying character restore` 19:10:36.545), and the settled read is the archived identity `r4d-host-panel.json`/`r4d-host-idn2.json` `cInfo=153,11,48636,2`, `153cm \| 11y \| #48636`. The transient read while the world was still loading showed the FRESH roll (`cInfo=0,0,0,0`, text `156cm \| 24y \| #40567`), which is exactly the set the restore replaced. The members re-entered on the host's hand-over (`Sent saved character data to <guest>` 19:10:44.249, to `<alt>` 19:10:45.039): the guest — dead before the continue (`brainHealth=0`) — came back alive at `brainHealth=75.074` with its saved identity `146,23,96520,2` (`r4e-guest-idn.json`, `r4e-guest-health.json`), the alt with `151,20,62120,2`. The alt's view of the host after the loaded run reads `153,11,48636,2` (`r4f-alt-panel.json`, log 19:11:13.156) — the saved values travel to a peer |
| 5 | A snapshot refresh while the focus is open | machine | **pass** | With the host viewing the guest, the guest's own identity was forced to `160,40,77777,9` (`r5-guest-force.json`); 3 s later the host's panel read `cInfo=160,40,77777,9`, `bodyStatText='160cm \| 40y \| #77777'`, `versionText='UNI-HEALTH v2.09'` with `ownInfo=153,11,48636,2` (`r5-host-panel-new.json`, log `shows … (160cm \| 40y \| #77777 …)` 19:07:48.055), and it followed back to `146,…` after the restore (`r5b-*`, log 19:07:56.080). Second instance with the host as the viewed player while the alt views it: forced `155,22,12345,4`, then 8 samples at 0.6 s all read `155,22,12345,4` and never the alt's own `151,20,62120,2` (`r5c-alt-sample-0…7.json`, one takeover line 19:11:22.710); restored to `153,11,48636,2`, 6 samples all read it back (`r5d-alt-sample-0…5.json`, log 19:11:33.742). The header is written only when the values change, so no local value is ever re-shown |
| 6 | A third client views the VIEWER while the viewer has a focus open | machine | **pass** | The host's panel was open on the guest (`r6-host-panel.json`: `cInfo=146,23,96520,2`, `ownInfo=153,11,48636,2`) when the alt opened the host's panel: `r6-alt-panel.json` reads `cInfo=153,11,48636,2`, `bodyStatText='153cm \| 11y \| #48636'`, `versionText='UNI-HEALTH v2.02'` — the HOST's own identity, not the guest's the host's panel was displaying — with `ownInfo=151,20,62120,2`; the alt log carries `shows the viewed player's own values (153cm \| 11y \| #48636 …)` at 19:07:33.883. This is the row the carrier guard exists for: without it the host's snapshot would have reported `146,…` as its own |

## What the run also verified

- The close path hands the header back even when the viewed player is already gone (the guest died
  while the host viewed it): the host's close still restored `153,11,48636,2` (19:08:53.754).
- Opening a second focus from an open one restores the first before taking the second: the alt's
  log shows `returned to the viewer's own values (151…)` immediately followed by `shows … (146…)`
  (19:08:11.242 / .245).
- A loaded run leaves the whole session intact: the members re-entered, the guest's dead character
  came back alive with its saved identity and 75 brain health, and every client's own panel kept
  its own values after their focuses closed.

## Residuals for the user

None: all six rows are value rows judged from the deployed artifact.

## Limits

- The rows are judged on the values the client's own game reports, not on pixels; no frame was
  captured (every row is a `machine` row).
- The first read after the host's Continue landed while the world was still loading and showed the
  fresh roll; the settled read is the evidence for row 4. A read taken in that first second would
  see the pre-restore roll, which is the game's own start order, not the header logic.
- The unknown-identity blank, the stale-borrow drop and the native save's capture window are not
  reachable in this run (a live capture failure, a scene reload, and a save with the panel open);
  they stay unit-tested and are named in the ticket's `Limits`.
- Row 5's "no flicker" half is judged from sampled reads (0.6 s apart) plus the write-on-change
  rule, not from a per-frame recording.
- The mid-focus local-restore adopt seam was not exercised: the restoring client (the host) had no
  panel open during its own restore, and a restore always re-creates the body, which closes any
  focus first. It stays a unit-tested guarantee (ticket `Limits`).
