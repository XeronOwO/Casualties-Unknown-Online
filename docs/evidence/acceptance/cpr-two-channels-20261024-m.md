# Batch 20261024-m — CPR's two channels read on the panel: compressions restart the heart, breaths carry oxygen

- Ticket: `docs/backlog/done/remote-medical-cpr.md` (stage 2's rule half, its entry, and their rows)
- Artifact: `0.1.0+8b45fee7`, deployed and verified against `<game-dir>` before the run
- Session: host + one member; host forced into cardiac arrest with `body-force heartRate=0,bloodPressure=5`; the member
  opened the chart through `medical-open` and pressed the panel's controls through `cpr-press` (their `onClick`).
- Logs: BOTH clients were read — the host's shared install log and the sandboxed member's own
  `BepInEx\logs\latest.log`.

## The rows, read

| step | reading |
|---|---|
| compress control before a session | `found:true, active:true, interactable:true` |
| start press | `pressed:true` — the session starts |
| breath control after the start | `found:true, active:true, interactable:true` — the second channel appears only with a running session |
| 40 compressions + 10 breaths | the member's log: **40** `[Cpr] compression … sent` and **17** `[Cpr] breath … sent` lines |
| the host after them | `alive:true, conscious:true` — the heart restarted again |
| oxygen from a LOW start | `body-force bloodOxygen=20` → **22.868** after two breaths |
| oxygen near the ceiling | 92.311 → **100** after five breaths (the game's own cap, `100 - hemothorax*0.3`, holds it) |

So both channels are reachable from the panel and both move the patient's own data: compressions reach the restart
threshold, breaths add oxygen up to the game's cap. The two intervals are measured separately (a breath's from the
previous breath), which is what the two controls exist for.

## Two process facts this batch fixed

- **`body-force` now reads and sets `bloodOxygen`** (added with an argument and in the answer), because an oxygen row
  cannot be judged from a read that does not report it. Its header lists the new argument with the others.
- **The breath interval shows the cadence rule again**: two breaths a driver round-trip apart (well over the policy's
  slow line) earn a fraction of the gain, exactly as the compressions did in batch 20261024-j; the row is still valid —
  the direction and the cap are what it certifies — but a HUMAN rhythm is what a "feel" row would time.

## Still owed on the ticket

- The AED/CPR restart arbitration (stage 3's second half).
- The operator's side (stage 4): strength/intelligence gates and the reference's feedback.
- The session-level rib case (the harness host snapshot lacks the chest limb the branch needs).
