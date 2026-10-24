# Batch 20261024-n — the CPR rhythm band's two edges, read on the machine

- Ticket: `docs/backlog/done/remote-medical-cpr.md` (stage 4's decided question: the reference's 65–220 a minute band)
- Artifact: `0.1.0+d845e4bc`, deployed and verified against `<game-dir>` before the run
- Session: host + one member; host forced into arrest with `body-force heartRate=0,bloodPressure=5,bloodOxygen=20`; the
  member opened the chart through `medical-open` and pressed the panel's control through `cpr-press`.

## Why this batch presses the way it does

A driver call per press costs about 1.4 s, which is 43 a minute — under the band's 65 floor. So the batch reads the band
from both sides:

- the SLOW edge by pressing once per call (the old cadence, now worth nothing), and
- the FAST edge with `cpr-press count=40`, which dispatches forty presses in ONE frame. The control measures its interval
  from real time, so same-frame presses are the shove the band's fast edge is about.

## The readings

| step | reading |
|---|---|
| start press | `pressed:1` — the session runs |
| SLOW edge: 8 presses ~1.4 s apart (≈43 a minute) | `heartRate 0`, `bloodPressure 0.347` — **no restart, no effect** |
| FAST edge: 40 presses in one frame | `heartRate 53.928`, `bloodPressure 23.151` — **the heart restarted** |

Both edges behave as the rule says: below 65 a minute the effort buys nothing, and a frantic shove still restarts a heart
(because half of a full gain per press, forty times, is far past the threshold) at the rib cost the applier charges —
that rib is unit-pinned (`CprRhythmPolicy.IsAnOverPress` plus the applier's chest-limb write) and was not read from the
patient's chart in this batch.

## What this changes for future batches

Every earlier CPR row was read at about 1.4 s between presses, which the band now scores as zero. Those rows stand for
what they certified (the restart, the oxygen channel, the panel controls) because they ran under the slope rule that was
in force then; a future row that needs a real rhythm reads the FAST edge with `count`, or waits for a rhythm-driving
helper that presses across frames.
