# The remote focus hides the local-only panel surfaces, read on both panels

- Ticket: `docs/backlog/review/remote-medical-panel-hide-local-only-actions.md` (rows 1–5 read; row 6 still open)
- Batch: `20261025-c` — one live two-client session, artifact `0.1.0+c08c99ad…` (both clients' own load line)
- Instrument: `panel-controls` (reads the four surfaces the hide set names, plus the remote-focus state and the displayed body)

## Why a new probe

The ticket's criterion is PRESENTATION, and the implementation hides three surfaces by deactivating their object but the HUD
swap control by disabling its graphic (its object also carries the hand slots, so it must stay active). A probe that reads
only `activeInHierarchy` therefore reports the HUD switch as "shown" while it is invisible — the first reading of this batch
did exactly that and was corrected by the probe, not by a code change.

## The readings

Remote focus (guest viewing the host; `remoteFocus:true`, displayed body present):

| surface | state |
|---|---|
| nap control (`WoundView.napbutton`) | **hidden** |
| sleep-state icon (`WoundView.sleepImage`) | **hidden** |
| workout list (`WoundView.workoutList`) | **hidden** |
| HUD main/off-hand switch (`PlayerCamera.handSwapImage`) | **graphic disabled** (not presented) |

Own panel (after closing the focus and reopening the panel on the viewer's own body; `remoteFocus:false`,
`viewOpen:true`):

| surface | state |
|---|---|
| nap control | **visible** |
| sleep-state icon | **visible** |
| HUD switch | **graphic enabled** |

The same read on the host's own panel (independent of the guest) agreed: nap and sleep icon visible, swap graphic enabled.

## Rows

- **Row 1 passes**: none of the four surfaces is presented while the focus shows another player's body.
- **Row 2 passes**: the viewer's own panel shows the nap control, the sleep-state icon and a working HUD switch (the workout
  list is inactive in this body's state, which is the game's own condition rather than a hidden surface).
- **Row 3 passes by construction, with the presentation reading beside it**: the mouse path is closed because the HUD swap
  graphic and the panel surfaces are not presented, and the keyboard path is closed by the patch itself —
  `[HarmonyPatch(typeof(PlayerCamera), "SwitchHands")]` with `Prefix() => !RemoteMedicalView.IsOpen` refuses the hand switch
  while the focus is open, which is the method the `switchhands` key handler calls.
- **Row 4 passes**: the allowed remote operations are unchanged — the CPR control was driven to a `Good` verdict in this same
  session with the guest's chart open on the host (batch `20261025-a`).
- **Row 5 passes**: closing the focus and reopening the viewer's own panel restored the nap control, the sleep-state icon and
  the HUD switch graphic.
- **Row 6 is not read**: inventory reporting around a blocked window needs a hand switch and an item move on either side of
  the focus, and no instrument for that sequence was driven in this batch. The ticket stays open on it.

## Row 6's attempt, and why it is still open

A second pass added `remote-hand` (every non-local `Body` with its hand slot and both slots' item ids) and read the host's
clones: both clones reported `handSlot:0` with `s0=none s1=none` before **and** after a `body-call kind=switch` on the guest,
because the guest held nothing — `item-provide create bandage` answered with an empty result, so no item id was accepted and
the slots had nothing to move. The row therefore still cannot be read: it needs a held item first (an item id this build
accepts) and then the switch on either side of the focus. The probe is committed so the next pass starts from it instead of
rebuilding it.
