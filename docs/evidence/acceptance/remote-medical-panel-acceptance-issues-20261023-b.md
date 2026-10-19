# Acceptance record — remote medical panel: mood cadence / breathing icon / ECG

- Ticket: `remote-medical-panel-acceptance-issues` — verdict: **moved to `done/`** (all four rows pass)
- Batch: `20261023-b` — sibling records `remote-medical-stage-1-injection-session-20261023-b.md`, `remote-fentanyl-injection-and-medical-panel-desync-20261023-b.md`
- Commit: `d960f05f` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `0.1.0+d960f05f885ba255f656607f1905a2ad3b922167`
- Run: 2026-10-19 21:38 → 22:01 · Host: physical machine · Guest: sandbox 1 (the viewing client); third: sandbox 2
- Dependencies: full preflight (`RESULT: OK`), three clients, window capture, in-process probes
- Artifacts: `.acceptance/20261023-b/`, cited below

The rows were judged against the panel the GUEST opens on the HOST through production's own
`RemoteMedicalCoordinator.Open`; the display body is `MedicalDisplay_<steamid>` and every machine read
comes from that client's own display copy, never from the host's body.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest injects fentanyl into the host; while viewing, the host's mood rises promptly with the committed dose, not at 1 Hz | machine + visual | **pass** | Sessions op 4/5/6 applied fentanyl natively. During op 4 the operator's display read opiate 73.905 / 94.787 / 115.107 and opiateHappiness 48.981 / 60.692 / 92.157, the host's own body 73.834 / 94.787 / 115.039 and 52.370 / 60.692 / 94.607 (`i7-*`); the display's moodles moved with it (`overdose2,happy6` → `overdose3,happy7`). A 1 Hz-only panel could not show three distinct values and the per-frame `happy` intensity change between two reads |
| 2 | Host breathing stops; the guest's panel shows the breathing-stop icon, not "通气不足" | machine + visual | **pass** | Host forced to `respiratoryRate=0` (the game derives `breathing=false`, `p2b-host-breathing-stop.json`); guest display read `breathing=false, respiratoryRate=2.938`, moodles `[cantbreathe3,hunger0,hot0,wet1]` (`p2b-guest-panel-breathing-stop.json`), the host's own panel the same `cantbreathe3` (`p2b-host-panel-breathing-stop.json`); frame `p2b-guest-panel-breathing-stop.png` shows the red 停止呼吸 icon. After restoring respiratory 100 the icon is gone (`p3-guest-panel-breathing-restored.json`) |
| 3 | Host heart stops; the guest's panel ECG reads the host and stops with it | machine + visual | **pass** | Host forced to `heartRate=0`; guest display `heartRate=0`, `heartProg` identical over 2 s (delta 0), moodles `cardiacarrest3` (`p4b-guest-panel-heart-stop-a/b.json`); the panel's ECG visualizer answers the display body (`ecgDisplay=1`, the consciousness overlay stays local `ecgLocal=1`, `p4b-guest-panel-heart-stop-a.json`); frame `p4b-guest-panel-heart-stop.png` shows the flat waveform and the red 0 heart readout. Restored to the measured 76.5 and the display converged (`p5b-guest-panel-heart-restored.json`) |
| 4 | The other bottom icons show the viewed player's stats, not the viewer's | machine + visual | **pass** | Paired state: host `sicknessAmount=20` → guest display moodles exactly `[confused2,hunger0,thirst0,sick0,hot0,wet0]`, the host's own panel the same set (`p6b-guest-panel-sick.json`, `p6b-host-panel-sick.json`); frame `p6b-guest-panel-sick.png` shows the sick icon; after `sickness=0` it is gone from the display (`p7b-guest-panel-sick-gone.json`). The fentanyl state repeated the equality for `hypotension/hypoventilation/overdose/happy` (`i7-guest-panel-after.json` vs the host's `i7-host-health-after.json`) |

## Residuals for the user

None. The frame for row 2 was captured with the Online UI window closed, after the user's own
observation that the UI overlay covered the panel.

## Limits

- The breathing and heart stops are forced states (`body-vitals-force`) written on the game's own
  `Body` fields, restored afterwards; they are not played-out medical events.
- The panel's identity header and heartbeat alarm are NOT rows of this ticket. Two user-found findings
  from this session were filed separately while the batch ran:
  `docs/backlog/todo/remote-medical-panel-header-shows-viewer-identity.md` and
  `docs/backlog/review/remote-medical-panel-alarm-follows-viewed-player.md`.
