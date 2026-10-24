# Two operators hold the same victim's native chart at once

- Ticket: `docs/backlog/review/concurrent-medical-operations.md` (its row 7, the native-UI half)
- Batch: `20261025-i`, deployed build `0.1.0+7605a140` (both clients report it in the window's build line)
- Instruments: `tools/acceptance/recipes/medical-open.cs` (`IRemoteMedicalPresentation.OpenRemoteMedical`, the same call the
  Online UI makes) and `tools/acceptance/recipes/capture-frame.cs`; the victim is the third member, whose own id came from
  `member-id probe=alt` on that client (`76561199526807662`).

## The reading

| client | `medical-open` for the victim | frame |
|---|---|---|
| host | `ok=true, opened=true, nativeOpen=true, found=true` | the victim's chart draws: `EXPERIMENT 137cm \| 19y \| #15296`, `UNI-HEALTH v2.01`, the limb rows and the head panel with its treatment buttons |
| guest | `ok=true, opened=true, nativeOpen=true, found=true` | the same victim's chart draws, same header and limb rows, with the Online UI window over it |

Both frames were taken while the other client's chart was still open, so two operators held the same victim's native chart at the
same time, and **neither panel froze**: each client kept rendering its own chart and its own UI around it.

## What this does and does not read of row 7

- **Read**: a second operator does not lock the chart away and does not produce a stuck panel — the exact "no stuck panel"
  clause, from pixels on both clients, with the victim being a third body neither operator owns.
- **Not read**: the "no duplicated progress on one body" clause, which needs an operation actually STARTED on the same limb by
  both operators with the progress read on the victim's own client. This batch opened the charts and stopped there; the start
  needs a treated limb and the two clicks that begin it, which is the next step rather than a claim this batch can make.
