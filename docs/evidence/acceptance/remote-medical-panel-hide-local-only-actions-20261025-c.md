# The remote focus hides the local-only panel surfaces, read on both panels

- Ticket: `docs/backlog/done/remote-medical-panel-hide-local-only-actions.md` (rows 1–5 read; row 6 still open)
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

## Row 6's attempt, and how it was read

The first pass could not read it: the guest held nothing, `item-provide create` refused with `no-free-slot` ("every body slot
is occupied or unusable"), and both of the host's clones read `handSlot:0 s0=none s1=none` before and after a switch — because
there was nothing to move.

The second pass found the cause and the row then read cleanly. The refusal came from the slot guard's `canPickUp`, which is
false while the viewer's own panel is open: closing the panel and creating again answered `ok slot=0`. From there, with
`remote-hand` reading the host's clone of the guest (its hand slot and both slots' item ids) beside the guest's own local
slots:

| step | guest, locally | host's clone of the guest |
|---|---|---|
| item created (`bandage`) | slot 0 | `s0=bandage s1=none` |
| hand switch, **before** the focus | item in slot 1 | `s0=none s1=bandage` |
| focus opened on the host, then closed | — | — |
| hand switch, **after** the focus | slot 1 | `s0=none s1=bandage` |
| second item created (`tweezers`) | `s0=tweezers s1=bandage`, hand 0 | `s0=tweezers s1=bandage`, hand 0 |
| hand switch after that | hand 0, tweezers in slot 0 | `s0=tweezers s1=bandage`, hand 0 |

Every step kept the peer's view of BOTH slots exactly equal to the viewer's own slots, the switches around the focus
included, and the clone's hand slot equalled the local hand slot. **Row 6 passes**, and with it the ticket has no unread row.
