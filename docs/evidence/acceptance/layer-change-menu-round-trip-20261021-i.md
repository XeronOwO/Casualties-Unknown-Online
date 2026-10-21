# Acceptance record — A layer change pulls every member back to the menu instead of loading the group together

- Ticket: `layer-change-menu-round-trip` — verdict: **moved to `done/`** (all four rows pass; status field:
  `- Status: Done (batch 20261021-i, 2026-10-21: …)`).
- Batch: `20261021-i` — this ticket (the fix cycle's own rerun: one normal descent and one descent with the
  new edge dropped on one member).
- Commit: `a495c742` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+a495c742c64757c34947bc30d60e5ed04368fcf7` (`prelude-verify-deploy.txt`;
  `tools/verify-deploy.ps1` exit 0).
- Run: 2026-10-21 22:09 → 22:14 local · Host: physical machine · Guests: sandbox `<sandbox-guest>` and
  `<sandbox-alt>` (three clients).
- Dependencies: the cycle's preflight read `11 present`; the batch's own
  `session-environment (ensure-cuo) exit=0` and the deploy verification above.
- Artifacts: `20261021-i/` under the directory named by `acceptance-artifacts-dir` — the prelude, the
  per-client frames (`a-r01-*.png`, `b2-r0*-*.png`), the native scene probes
  (`a-r0*-*-read.json`, `b2-r0*-*-read.json`), the carried-item censuses
  (`a-*-items-{before,after}.json`), the log windows (`acceptance-a-*-log.txt`, `b2-*-log.txt`), the drop
  answers (`b2-drop-{arm,off}.json`), and the run logs (`acceptance.log`, `b2.log`).

## Session A — the normal descent

One native `skiplayer` at 22:10:06.5 (layer 0 → 1). Host: `[LayerDescent] … 2 member(s) told to generate
their own new layer` at 06.520 and `Start gate expects 2 member(s)` at the same instant. Both members
accepted the edge at 06.553; the exit they reported was `Scene state: InMenu (SampleScene)` — the actual
scene, not the old hard-coded `PreGen` — and neither requested a menu return (the guest's log carries
`[LayerDescent] the host left the world — this is the descent this member is following, not a menu exit`).
The host committed the new baseline at 07.145; the members' generations reset to it (the guest's
`Generation stream reset … ADCF7065…` equals the host's), they settled at 14.40/14.42 and reported InWorld,
the host entered at 14.637 and the gate released at 14.650.

## The rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Within one second of the host's descent each member's screen shows the loading screen, never the menu/cover; a frame per client and the native scene probe | visual + machine | **pass** | `a-r01-guest.png`, `a-r01-alt.png`, `a-r01-host.png` are the first frames after the command (~1 s): every one is the game's loading screen (black with `正在生成可站立地面…`), the Online UI window in front of it; none is the cover. The probes at the same round (`a-r01-*-read.json`) read `scene:"SampleScene"`, `world:true`, `generating:true`, `loadingActive:true`, `depth:1` on all three. No `a-r*-*-read.json` in the session reads `PreGen` (the guest's own A log has no PreGen line), and the guest's local scene report at 06.561 names `SampleScene`. |
| 2 | The member enters the new layer in the same window as the host (≤ 2 s), roster `aligned=True`, carried items the same | machine | **pass** | Host entry `Scene state: InWorld (SampleScene)` 22:10:14.637; guest `World ready — start playing` 14.650; alt 14.650 — 13 ms skew. Both members: `roster adopted: 82 rows (0 kept, 82 reused, 0 built, …) — aligned=True` (guest 14.804, alt 14.802). The members' carried items are the same three ids before and after (`5197989525, 5861509230, 1155667183555`; order differs), the host's layer reset reads `3 item record(s) remain (carried items cross the boundary)` at both boundaries. |
| 3 | No member-side `Leaving the world to the main menu`, no `PreGen` probe, no `WorldReady never arrived`, no new warning | machine | **pass** | The session A guest/alt logs contain no `RunMenuReturnCoordinator: Leaving the world to the main menu.` and no `PreGen`; no `WorldReady never arrived` on any client; no `[ERR]` in any A window. The only non-Unity warnings in the A window are the existing `[TrapEvent] SpikeStabbed … already consumed locally — duplicate dropped` diagnostics (2 host / 3 guest / 3 alt at 22:10:14.5), the trap-event dedupe answering a duplicate relay; the rest are the game's `[Unity] Sprite Tiling` asset warning. |
| 4 | With the new edge dropped on one member, that member still recovers through the ordinary `WorldJoin` and the group still loads together | machine | **pass** | `b2-drop-arm.json`: `msg=LayerDescent, armed=true, dropped=1` (the off answer confirms exactly one frame dropped). The guest's log has no `[LayerDescent]` line at all: it sees the host's InMenu at 22:13:02.728, `Leaving the world to the main menu` at 02.738, sits in `PreGen` (probes `b2-r01/r02-guest-read.json`), receives `World join received — starting a run` at 10.574, generates, reports InWorld at 19.244 and plays at 21.339. The host's `Start gate armed — waiting for 1 member(s)` (10.571) held the already-entered alt (its probe at 16.5 read `timeScale:0`, `loadingActive:true`) and released the group at 19.408; the alt's later probes read playing. |

## The reading behind the fix

The pre-fix batch (`20261021-h`) is the ticket's attribution: the member was in `PreGen` 1 ms after the
host's InMenu report (`[Reentry] the host left the world — this member follows it to the menu` →
`Leaving the world to the main menu`), generated only after the host's own generation had finished, and
entered ~10 s behind. This batch's session A shows the same boundary with the member never leaving the
world scene, generating from the host's baseline in parallel and entering in the same 13 ms window.

## Limits

- The frames carry the batch's own Online UI window over the loading screen (the entry setup leaves it
  open); the game's loading label is visible behind it, which is what the row judges. A frame has no
  embedded capture timestamp: "within one second" is the capture order in `acceptance.log` (the command
  returned at t+8.9 s and the first frame was written immediately after) plus the guest's 06.553/06.561
  log lines.
- Session A's layer held 82 enemies (the earlier batches' 375/956 worlds are denser); the descent's
  loading choreography does not depend on the roster size, and the count is named so the row is not read
  as a denser-world proof.
- The degraded session B2's member went through the menu by design (row 4 is the fallback's own row); its
  entry trails the host by the menu round trip, and the group waits on the gate for it.
- No warning baseline diff was taken across artifacts: the record names every non-Unity warning in the
  normal session instead.
- `net-drop-message`'s modes are `arm|status|off`; the first attempt used `on` and was corrected to `arm`
  in the same batch (`b2-drop-arm.json` is the answer the row cites).
