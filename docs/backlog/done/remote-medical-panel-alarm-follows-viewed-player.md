# The remote medical panel's heartbeat alarm follows the viewer, not the viewed player

- Status: Done — accepted in batch `20261023-c` (rows 1-6 pass); record `docs/evidence/acceptance/remote-medical-panel-alarm-follows-viewed-player-20261023-c.md`
- Priority: High
- Category: Remote medical / WoundView display / heartbeat alarm
- Source: User report (2026-10-19, during acceptance batch `20261023-b`): hearing the medical panel's critical "滴滴滴" alarm on the host whose own panel was not open, while the sandbox clients were muted.
- User ruling (2026-10-19): the alarm must follow the VIEWED player, not the viewer.

## Problem

The native heartbeat sound is played by `Body.HandleCirculation` for the LOCAL body:

- `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:888-924` — on each beat
  (`heartProg > 0.3`), it picks `heartthump`, `heartthump-heavy` (when the local body
  `isCriticallyDying`) or `heartthump-heavy-monitor` (the same, and the LOCAL
  `PlayerCamera.main.woundView.activeSelf` is true), and plays it when
  `woundView.activeSelf || isCriticallyDying`.
- `Body.cs:334-340` — `isCriticallyDying` is the local body's own state
  (blood oxygen < 50, blood pressure < 70, fibrillation > 60, cardiac arrest, …).

CUO redirects the ECG **visual** (`ECGVisualizer.get_body`) to the display body while a remote
medical view is open, but nothing redirects the thump sound: it stays on the viewer's own body,
and the monitor variant is selected by the LOCAL panel being open, whatever body that panel
displays. So a viewer in a critical state hears the monitor alarm while looking at a healthy
patient; the panel shows the patient's ECG, the alarm describes the viewer.

Live sequence in batch `20261023-b` (the user heard it):

| time | event |
|---|---|
| 21:53:31 | host opens its own remote medical panel on the alt |
| 21:53:47 | the clamp row's oversized update commits 42.06 ml of fentanyl into the host; its blood oxygen collapses (`isCriticallyDying` true) |
| 21:53:47–21:54:24 | the host plays `heartthump-heavy-monitor` — "滴滴滴" — while the panel displays the healthy alt |
| 21:54:24 | host `/heal @s` restores it; the alarm stops |
| 21:59 | all remote panels closed; silence confirmed |

## Design (decided by the user)

1. While a remote medical focus is open, the viewer's own body must not drive the panel's
   heartbeat alarm: suppress the native local-body thump for that client (the same close path
   restores native behaviour).
2. The alarm that plays must describe the DISPLAYED body: its heart rate drives the beat, and
   its `isCriticallyDying` state selects the monitor variant, so a healthy displayed patient
   is silent and a critical one alarms. Closing the focus stops the alarm.
3. No new UI and no parallel panel: the sound stays the game's own `heartthump*` clips, played
   locally through `Sound.Play`.
4. With no remote focus, the native behaviour is untouched.

## What landed (2026-10-19)

The native call site the panel mirrors is named before the change: the remote medical focus opens
the real native `WoundView` (`RemoteMedicalCoordinator.Open` → `PlayerCamera.WoundViewButton`), so
the native monitor condition is genuinely satisfied. The change adds no gesture, no entry and no
wire member; it redirects one native presentation effect at its own call site.

| Mechanism | Change | Evidence |
|---|---|---|
| Native thump (`Body.HandleCirculation`'s string `Sound.Play`, Body.cs:907-924) | Refused while a remote focus is open by `RemoteMedicalNativeHeartbeatPatch`, for the viewer's own body and for a host-simulated clone alike; the refusal is reported at Debug with the exact clip | `RemoteMedicalHeartbeatTests.NativeHeartbeatPatch_TargetsTheStringSoundPlayOverload`, `SuppressionPrefix_RefusesOnlyWhileTheRemoteFocusIsOpen`; the batch record's log lines |
| Panel alarm | `RemoteMedicalHeartbeat.Play`, called from the coordinator's per-frame pump right after `AdvanceMedicalDisplay`, so the alarm rides the ECG's own phase; `heartthump-heavy-monitor` (or `heartthump-heavy` in an unchipped run) only while the displayed body is `isCriticallyDying`; the native fibrillation volume damping; the AudioClip overload, so the suppression prefix cannot catch it | `RemoteMedicalHeartbeatTests.Beat_*`, `Coordinator_PlaysTheAlarmFromThePhaseItJustAdvanced`; the batch record |
| Focus lifecycle | The per-focus beat latch is reset in `RemoteMedicalView.Close` before its early return; every open passes through it | `ClosePath_ResetsThePanelLatch_BeforeTheEarlyReturn` |
| Display state the alarm reads | `heartProg` advanced by the existing display pump, `heartRate`/`isCriticallyDying`/`fibrillationProgress` from the 1 Hz character snapshot. The wire's native `TotalBleedSpeed` is now kept instead of rebuilt from limb bleeds — the limb-only sum dropped the native `bleedingSpeedMultiplier` (~0.01-0.02×), the internal-bleeding and the regen terms, so it read two orders of magnitude high, and it feeds `isCriticallyDying` | `DisplayBody_BleedSpeedRidesTheWire_NotALimbSum`; row 6 in the batch record |
| Game clips only | The game's own `heartthump*` clips loaded exactly like the native string overload, played with the native 2D/pitch-stable/reverb-free/mixer-bypassing arguments | `NativeHeartbeatPatch_TargetsTheStringSoundPlayOverload` pins the string overload; the cue's call is read in `RemoteMedicalHeartbeat.Play` |

Interpretation recorded for acceptance: the design's "a healthy displayed patient is silent and a
critical one alarms" is implemented literally — the panel plays no sound while the displayed body
is not `isCriticallyDying`, and the normal `heartthump` clip is only recognised so the native
attempt at it can be refused.

The panel's own `WoundView.Update` critical audio loop already reads the display body (the
`WoundView.body` patch predates this ticket), so it described the viewed player already and stays
native; this ticket changes the `Body.HandleCirculation` thump beside it.

## Acceptance rows

| # | Scenario | Expected |
|---|---|---|
| 1 | Viewer critical, remote panel on a healthy peer | No panel alarm for the patient; the viewer's own dying feedback is handled per the rule above (no monitor alarm from the viewer's state) |
| 2 | Viewer healthy, remote panel on a critical peer | The patient's `heartthump*` plays at the patient's rate, monitor variant when the patient is critically dying |
| 3 | Close the remote focus | The alarm stops in the same frame; the local panel's native behaviour returns |
| 4 | Both directions and the third-party view | The alarm follows the displayed body in each case; no double sound |
| 5 | Local panel (no remote focus) | Unchanged native heartbeat/alarm |
| 6 | A patient critical only by the bleeding clause (`totalBleedSpeed > 0.02` and `bloodVolume < 30`) | The panel's displayed `totalBleedSpeed`/`isCriticallyDying` agree with the patient's own, and the alarm follows — added by the independent review, which found the display projection rebuilding that value from limb bleeds |

## Non-goals

- No new alarm UI; no CUO-drawn monitor.
- The prefab/static panel pieces are out of scope.
