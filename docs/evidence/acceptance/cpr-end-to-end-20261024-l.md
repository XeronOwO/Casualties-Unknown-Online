# Batch 20261024-l — CPR end to end on the machine, and why the two earlier reads failed

- Ticket: `docs/backlog/done/remote-medical-cpr.md` (stages 1a–1c and their row)
- Artifact: `0.1.0+e15a8521`, deployed and verified against `<game-dir>` before the run
- Session: host + one member, two clients; host forced into cardiac arrest with
  `body-force heartRate=0,bloodPressure=5` (lands and holds), the member opened the chart through
  `medical-open` (the same call the Online UI makes), and pressed the panel's CPR control through `cpr-press`
  (its `onClick`, which is a player's click).

## The row, read

| step | reading |
|---|---|
| chart opened on the member | `opened:true`, `nativeOpen:true` |
| CPR control read | `found:true, active:true, interactable:true` |
| start press | `pressed:true`; the host logs `[MedicalOps3] started Cpr operation 1` |
| 40 compressions at the batch's cadence | the member's own log: **40** `[Cpr] compression …s sent to operation 1` lines |
| the host's own fields after | **`heartRate` 0 → 81.151, `bloodPressure` 5 → 131.191, `conscious` false → true** |

So the whole chain works: the control appears for an arrested teammate, pressing it starts the session, compressions
build perfusion, the threshold restarts the heart (the applier writes `HeartRate = PerfusingHeartRate` and clears
fibrillation), and the game's OWN model restores pressure and consciousness from there — no derived value is written.

## Why the two earlier reads said "nothing happened"

Both were the SAME arithmetic, not a defect:

- **Batch 20261024-j (3 presses)** and **batch 20261024-k (1 start + 10 presses)** each sent presses about **1.4 s
  apart** — one driver round-trip per press. `CprRhythmPolicy` scales the gain by the cadence, and 1.4 s sits near the
  slow line (`TooSlowInterval = 1.6`), so each press earned about a fifth of a full gain: 10 presses ≈ 11 of the 45
  perfusion the restart needs, and 3 presses ≈ 3.
- The first read (batch j) also pre-dated the fix that stopped writing the simulated `BloodPressure`; that write was
  genuinely wrong and is now corrected (perfusion progress + restart).

The member's own log settled the "are the compressions even sent" question in one read: the sandboxed client writes its
own `BepInEx\logs\latest.log`, at the gitignored `sandbox-guest-root`, not the shared install log. **A row that spans two
clients must read BOTH logs**; the shared file is the host's only.

## Still owed on the ticket

- Ventilation (stage 2): the oxygen half of the rhythm.
- Side effects (stage 3): rib breaks on over-pressing, and the AED/CPR restart arbitration when both run.
- The operator's screen (stage 4): the strength/intelligence gates and the reference's feedback.
- A batch at a HUMAN cadence: this one proves the mechanism at 1.4 s; a player pressing at ~0.55 s reaches the restart
  in about seven presses, which is the intended feel and has not been timed on the machine.
