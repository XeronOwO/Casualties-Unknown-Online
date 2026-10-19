# Acceptance record — the remote medical panel's heartbeat alarm

- Ticket: `remote-medical-panel-alarm-follows-viewed-player` — verdict: **moved to `done/`** (rows 1-6 pass)
- Batch: `20261023-c` — this run is the batch's whole scope
- Commit: `bcee1d38` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `0.1.0+bcee1d381043190d4aa1d46a12be1a7aaff7cf12` (`deploy`/`verify` exit 0 before the clients started, `prelude.log`)
- Run: 2026-10-19 23:17 → 23:25 · Host: physical machine · Guest: sandbox 1 · Third client: sandbox 2
- Dependencies: full preflight (`RESULT: OK`), three clients, in-process probes, per-client logs — no frame capture and no OS input (every row is a sound-mechanism row)
- Artifacts: `.acceptance/20261023-c/` — `prelude.log`, `r1b.log`, `r2.log`, `r3.log`, `r4.log`, `r6.log`, the `r*-*.json` probe answers and the `m-*.txt` log marks cited per row

## What the run judged

The fix is judged at the mechanism the player hears from: the client's own log names the exact clip the
native thump block attempted (and that the suppression refused), and the panel's own cue logs every alarm
it plays with the displayed body's rate. `heartbeat-read.cs` reads, in the same pass, the panel's displayed
body and the local body (critical state, heart phase, bleeding value, consciousness) and a live census of
`heartthump*` AudioSources (`Sound.Play` creates one per play; `PlayedSound` destroys it when the clip
stops). Every forced state was restored and read back; all three clients ended healthy and conscious
(`final-*-vitals.json`), were quit through the game, and the run left no game process
(`session-environment -Mode status`: `active=cuo`, `game-running=false`).

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Viewer critical, remote panel on a healthy peer | machine | **pass** | The host's own body forced critical (`septicShock=90`, `r1-host-force.json`: `critical=true`), the guest healthy; the host opened the guest's panel (`viewOpen=true`, display `critical=false`). Over 6 s the host log carried **0** panel-alarm lines and 27 refusals, among them `suppressed native heartbeat heartthump-heavy-monitor` — the viewer's own critical clip never reached the speakers; no heartthump source was sounding at the read (`r1b.log`, `r1-host-heartbeat.json`, `m-r1.txt`) |
| 2 | Viewer healthy, remote panel on a critical peer | machine | **pass** | The host (patient) forced critical (`critical=true`, display `heart=102.9`); the guest healthy (`local critical=false`) opened its panel. The guest log carried **10** `panel alarm heartthump-heavy-monitor` lines in 6 s, each naming the patient's rate (`heartRate=102.2 bpm`, fibrillation 0 → the native volume 1.625), and **9** refusals of the viewer's own normal `heartthump`; the census showed the monitor source playing (`r2.log`, `r2-guest-heartbeat.json`, `m-r2.txt`) |
| 3 | Close the remote focus | machine | **pass** | The focus was alarming (4 cue lines in the 3 s window). The production close path was used (`medical-view-close.cs`) and 3 s later: cue 0, refusals 0, `viewOpen=false`, and the suppression prefix passed all three clips (`r3-guest-closed-heartbeat.json`). The local panel then played its native monitor (row 5) (`r3.log`, `m-r3-open.txt`, `m-r3-closed.txt`) |
| 4 | Both directions and the third-party view | machine | **pass** | Direction B: the host (healthy) viewed the critical guest and logged **10** monitor cue lines; the guest, with no focus, logged **0**. Third party: the alternate client viewed the critical host and logged **7** monitor cue lines, its own body healthy; no non-viewer client cued (`r4.log`, `r4-host-heartbeat.json`, `r4-alt-heartbeat.json`, the `m-r4*` marks) |
| 5 | Local panel (no remote focus) | machine | **pass** | The guest's own critical body with its LOCAL panel open (no remote focus): cue 0, refusals 0, the prefix passed `heartthump`, `heartthump-heavy` and `heartthump-heavy-monitor`, and the census showed the native monitor sounding — the native path is untouched (`r3.log`, `r3-guest-local-heartbeat.json`, `m-r5.txt`) |
| 6 | A patient critical only by the bleeding clause | machine | **pass** | 6a (below the native threshold): owner native `totalBleedSpeed=0` (`limb 1.0 × bleedSpeedMult 0.01 − regen`), display `totalBleedSpeed=0`, `critical=false`, 0 cue lines. 6b: owner `totalBleedSpeed=0.07926`, `critical=true`; display `totalBleedSpeed=0.07923`, `critical=true`, **8** monitor cue lines at the patient's `96 bpm`. The old limb-only rebuild would have shown 1.0 in 6a and 0 in 6b (`r6.log`, the `r6b2-*` answers, `m-r6a.txt`, `m-r6b2.txt`) |

## What the run also verified

- The panel's own native critical siren (`WoundView`'s looping `healthpanel-alert`) is a **separate** native
  clip from the heartbeat family and already reads the displayed body (`woundview-audio.cs`: clip
  `healthpanel-alert`, loop, `critical=true`, body `MedicalDisplay_…`). The new cue adds the patient's
  heartbeat beside it, which is the same pair a local critical body produces — no duplicate of the viewer's
  alarm and no second copy of the same native loop.
- The suppression reports the exact attempted clip, so the "viewer's own alarm" half of the defect is
  observable: every refusal names the clip and the viewer's own body is the only client-side source of the
  `heavy-monitor` variant (the other simulated bodies were healthy in rows 1, 3 and 4).

## Residuals for the user

None: the ticket's rows are all machine-judged. The audible balance between the panel's own
`healthpanel-alert` siren and the patient's heartbeat is a listening judgement the run cannot capture,
recorded under Limits rather than asked as a row.

## Limits

- Audibility is not captured: the run proves which clip is played, from which body, at which rate, and that
  the wrong clip is refused; the physical volume/mix is the user's ear.
- The critical states are forced on the game's own public Body fields and restored afterwards (septic shock
  90; hunger -5 with limb-0 muscle 10; blood volume 20 with a 1.0 limb bleed and with internal bleeding 20);
  they are not played-out medical events. The septic clause was chosen for the viewer because the hunger/limb
  clause knocks the body unconscious, which closes the native panel.
- The suppression window is the remote focus: a panel closed through a path that bypasses the native toggle
  (`PlayerCamera.ToggleWoundView`) is corrected by the coordinator on its next frame, so the native sound can
  resume one frame after such a close. The batch's close path is the production close, judged above.
- The 6b2 owner reading is taken after the native pass settled; the same-frame reading right after the force
  still carries the previous value (the field is recomputed each circulation frame), which is why the row
  cites the settled pair.
