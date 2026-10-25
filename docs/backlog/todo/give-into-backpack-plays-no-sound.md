# Giving an item into another player's backpack is not the native transfer

- Status: Todo — opened 2026-10-25 from the user's report (translated): "why is there no sound when I move an item into another player's
  backpack? A normal item move has a cloth-rustle sound", followed by
  the standard it must be held to: "I want the native effect reproduced as far as possible, why are you cutting corners".
- Priority: High
- Category: Cross-player item transfer / native parity
- Source: user report (2026-10-25).

## Goal

A give into another player's backpack IS the game's own transfer: the same native call the local move makes, on a real item and a
real body slot, so every side effect the game produces — the friction/clothing sound, the item's presentation, the container
refresh, the wear strip — happens BY CONSTRUCTION. The missing sound is one symptom of the transfer not being native; this ticket is
about the call path, not about playing a clip.

## First batch (2026-10-25, `8516c7aa` reverted)

The change that let the native body run was **rejected by the run**, not by a test, and the revert is the finding:

- The release fell through to the native `HandleReleaseDragging` body as designed, and the log shows the native action running
  (`[PickUpResult] emergencylight → slot (slot 1)`) — but it resolved the **host's own inventory ring**, not the displayed clone, so
  the giver's item was moved into his own body slot and then, when the give's intent made it the guest's, destroyed and re-materialised
  as a **standing item on the ground**: `[ItemTrace] op=3 … OnItemDestroyed … committed` then
  `[StandingItem] materialized emergencylight …`. The user's report of the same moment (translated): "I dragged the flashlight, dragged it a
  few times in a row, and the flashlight was gone".
- So outcome 2 of the design is right and the one-line fall-through is not enough: the native body has to be pointed at the PROXY's
  container, which is what `InvButtonBodyPatch` does for a body — a `get_item`/`InvButton` answer for the displayed backpack's slots.
  That is the next work item, and it cannot be done by letting the body run against whatever the ring happens to hold.
- The revert restores the silent give (the reported symptom) rather than shipping an item loss.

## Current behaviour (read from the code and the game's own source, 2026-10-25)

**The give never reaches the game's transfer.** The release's patch (`PlayerCameraDragUsePatch.Prefix`) resolves the give target, calls
`ReportLocalReleaseOntoProxy` and then `ClearDrag` + `return` — so the native body that would run the transfer never does. That is why
there is no sound: the clip is not missing, the CALL is.

The native chain for a drop onto a container's slot, quoted from `reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:1490`
(`TryPerformInventoryAction`):

```
Container container;
if (item && item.TryGetComponent<Container>(ref container)) {
    if (container.CanHoldItem(this.dragItem)) { … } else { … }
    container.UnloadItem(this.dragItem, null);
    container.LoadItem(this.dragItem);
    this.PlayBackpackSound();          // <- the friction/clothing sound the report names
    return true;
}
```

and `PlayBackpackSound` itself (`PlayerCamera.cs:44`) plays `Sound.Play("backpack" + Random.Range(1, 3), … )` — the game's own clip, with
its own random pitch. `Container.LoadItem` (`Container.cs:116`) is the move: parent under the container, `rb.simulated = false`, position
snapped, and the refusals it states by name (stacking, a container already inside a container).

So the ticket's second outcome applies: **the native call is replaced by the intent**, and the fix is to let the native branch run.

## Shape the fix takes

The displayed backpack's slots are REAL `InvButton`s over REAL `Container` clones (`CloneInventoryRenderer` instantiates the prefab), so
the native `TryPerformInventoryAction` can run against them unchanged. The give branch therefore reports its intent (the authoritative
move, which still travels as it does today) and then FALLS THROUGH to the native body instead of clearing the drag: the game's own
`UnloadItem` → `LoadItem` → `PlayBackpackSound` runs on the proxy, which is the presentation and the sound the player expects, and the
authoritative result arrives from the owner as before.

The drag window stays CLOSED for this path on purpose: `Container.LoadItem`/`UnloadItem` are patched to report into that window, and an
open window would turn the give's own native load into a second intent. The native call is observable instead by its own Debug line
(one per load, high-frequency → Debug), which is the instrument row 1 reads.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | The operator's client shows the SAME native call in the log for a local move and for a give (the function the game itself runs), captured in one batch |
| 2 | The transfer sound is present on the operator's client at the moment the item lands, read by the same instrument for the local move and the give (audio capture, or the game's own clip trace) |
| 3 | The target's own client shows the item in its slot with the game's own presentation, and the target's client plays whatever it normally plays when its inventory changes |
| 4 | A refused give plays the game's own deny sound, not the transfer sound — again from the native path |

## Limits

- A proxy transfer cannot invent the target's own audio: what the TARGET hears follows its own client's rules, and the ticket says
  which client each row was read on.
- If the native call turns out to be unreachable for a proxy (a private method or a hard collider dependency), the ticket must record
  that evidence and the exact difference from the native effect before any substitute is designed.
