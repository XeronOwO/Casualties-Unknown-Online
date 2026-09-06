# Remote medical panel acceptance issues (mood cadence / breathing icon / ECG)

- Status: Todo
- Priority: High
- Category: Remote medical / WoundView display / real-time injection
- Parents:
  - `docs/backlog/review/remote-fentanyl-injection-and-medical-panel-desync.md`
  - `docs/backlog/review/remote-player-medical-panel.md`
  - `docs/backlog/review/remote-medical-stage-1-injection-session.md`
- Source: User acceptance findings (2026-09-06). Record only; no code change in this cycle.

## Reported issues

1. **Fentanyl mood update rate is 1 Hz**
   - Guest opens the host's medical panel and injects fentanyl into the host.
   - The guest only sees the host's mood/happiness update at the 1 Hz character
     snapshot cadence, once per second.
   - The rapid mood increase after fentanyl is not reflected in real time.
   - Expected: the remote medical view should show the authoritative mood
     progression while the injection is active / shortly after, not only at 1 Hz.

2. **Breathing-stop icon is wrong on the remote medical panel**
   - The host's breathing stops.
   - The guest sees the host's medical panel bottom icon showing
     "通气不足" / insufficient ventilation.
   - Other bottom icons sync normally.
   - Expected: the breathing-stop state should be presented with the correct
     host-side icon/state, not a generic or viewer-derived "通气不足" state.

3. **ECG on the remote medical panel is still the viewer's own**
   - The guest opens the host's medical panel.
   - The ECG waveform shown is still the guest's own ECG, not the host's.
   - After the host's heart stops, the ECG still continues beating normally.
   - Expected: while viewing a remote player, the ECG must read the remote
     display body / host heart state, and must stop/change when the host's
     heart stops.

## Acceptance matrix placeholder

| Scenario | Expected | Current observed |
|---|---|---|
| Guest injects fentanyl into host; guest watches host mood | mood rises promptly with the committed dose | mood updates at 1 Hz |
| Host breathing stops; guest watches host medical panel | correct breathing-stop icon on host panel | shows "通气不足" |
| Host heart stops; guest watches host medical panel | ECG stops / reflects host heart state | ECG is guest's own and keeps beating |
| Other bottom icons | sync with host | currently sync normally (isolated issue) |

## Non-goals

- No code change in this cycle; this ticket is a record.
- Not proposing a specific implementation design yet.
