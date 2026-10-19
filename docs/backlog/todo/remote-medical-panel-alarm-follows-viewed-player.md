# The remote medical panel's heartbeat alarm follows the viewer, not the viewed player

- Status: Todo
- Priority: High
- Category: Remote medical / WoundView display / heartbeat alarm
- Source: User report (2026-10-19, during acceptance batch `20261023-b`): hearing the medical panel's critical "滴滴滴" alarm on the host whose own panel was not open, while the sandbox clients were muted.
- User ruling (2026-10-19): the alarm must follow the VIEWED player, not the viewer.

## Problem

The native heartbeat sound is played by `Body.Update` for the LOCAL body:

- `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:907-921` — on each beat
  (`heartProg > 0.3`), it picks `heartthump`, `heartthump-heavy` (when the local body
  `isCriticallyDying`) or `heartthump-heavy-monitor` (the same, and the LOCAL
  `PlayerCamera.main.woundView.activeSelf` is true), and plays it when
  `woundView.activeSelf || isCriticallyDying`.
- `Body.cs:334-336` — `isCriticallyDying` is the local body's own state
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

## Acceptance rows

| # | Scenario | Expected |
|---|---|---|
| 1 | Viewer critical, remote panel on a healthy peer | No panel alarm for the patient; the viewer's own dying feedback is handled per the rule above (no monitor alarm from the viewer's state) |
| 2 | Viewer healthy, remote panel on a critical peer | The patient's `heartthump*` plays at the patient's rate, monitor variant when the patient is critically dying |
| 3 | Close the remote focus | The alarm stops in the same frame; the local panel's native behaviour returns |
| 4 | Both directions and the third-party view | The alarm follows the displayed body in each case; no double sound |
| 5 | Local panel (no remote focus) | Unchanged native heartbeat/alarm |

## Non-goals

- No new alarm UI; no CUO-drawn monitor.
- The prefab/static panel pieces are out of scope.
