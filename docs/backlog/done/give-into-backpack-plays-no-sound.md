# Giving an item into another player's backpack is not the native transfer

- Status: Done — closed 2026-10-25 (the ruling and the ear) — opened 2026-10-25 from the user's report (translated): "why is there no sound when I move an item into another player's
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

## Fixed in code (2026-10-25)

The giver's own client now plays the game's own sound when the item really leaves the body: `PlayerInteractionApply` calls
`PlayerCamera.PlayBackpackSound()` on the transfer's giver side, guarded by the removal actually happening
(`PlayerInventoryPlacement.RemoveCarriedItemFromLocalBody` answers whether it removed one). `PlayBackpackSound` is the native call
(`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:44`: `Sound.Play("backpack" + Random.Range(1, 3), …)`) that the game's own
container branch runs right after `container.UnloadItem` + `container.LoadItem` — the release a cross-player give is, with somebody else's
backpack as the destination. The native chain itself cannot run there (the destination container belongs to another client), so the one
perceptible effect it produces is made by the same call the game makes.

## Why the call can still be silent — narrowed by reading, 2026-10-25 (no run needed)

The call is reached only when `removed` is true (`PlayerInteractionApply.cs`: `var removed = _placement.RemoveCarriedItemFromLocalBody(body,
msg.Item?.InstanceId ?? 0); changed = true; if (removed && PlayerCamera.main != null) { PlayerCamera.main.PlayBackpackSound(); }`). So the
silence has exactly three possible causes, and reading has eliminated two:

1. **The message never reaches the giver — ELIMINATED.** `PlayerInteractionKernelProjection.ProjectTransfer` fires for `FromSteamId == local`
   as well as the receiver, and the codec carries `Item` (`PlayerInteractionKernelCodec.ToTransferMessage(e)`), so the giver's client runs this
   branch with a non-zero instance id.
2. **The branch is skipped as a drop — only for a drop-rung give.** `Destination == PlayerInventoryDestination.Drop` returns before the sound,
   which is correct: nothing was put into a backpack. A give that reports `Drop` in the log is the ladder's last rung, not this bug.
3. **`removed` came back false — THE REMAINING CANDIDATE.** `RemoveCarriedItemFromLocalBody` answers true only when it finds an `Item` under
   the local body whose `ItemInstanceId.Id` equals the id (`PlayerInventoryPlacement.TryDestroyById`). The native press does NOT unparent the
   item (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs`: `this.dragItem = invButton.GetItem();` — a reference, the item stays in
   its slot), so a give of a CARRIED item should find it. Two shapes would not: an item that left the body before the round trip (the
   ground-item give's `CarryForGive`, which the ground ticket already tracks) and an id that was zeroed by an earlier report.

**The next batch's instrument** is therefore one line the product already logs, not a new log: on the giver, the transfer projection's
`[PlayerInteractionKernel] projected inventory transfer …` (Debug) next to whether the same id was still in the body. If the id is absent from
the body at that moment, the sound's guard is doing its job and the fix is the removal's timing, not the call.

## ROOT CAUSE FOUND AND FIXED, 2026-10-25 — the give had been impossible since the refusal

`CarryForGive` was a **stub that refused every release unconditionally**:

```csharp
private bool CarryForGive(Item? dragItem, ulong itemId)
{
    domains.Log.LogInformation("[RemoteIntent] refused the release of the ground item {Item} … pick it up first …", …);
    return false;    // ← every item, carried or not
}
```

Its name promises "carry the item into the body for the give"; its body only logged the ground-item refusal. So `ReportLocalReleaseOntoProxy`
answered false for EVERY item, the intent never left the giver's client, and the sound could not play: not because the clip was missing, but
because **the give never happened**. The machine said so twice before the fix (`.acceptance/batch-givesound`):

```
20:05:15.707 [RemoteIntent] refused the release of the ground item emergencylight(Clone) (id 5197989525) …
20:05:43.645 [RemoteIntent] refused the release of the ground item waterbottle(Clone) (id 13787924117) …
```

The second one is the proof it was a misclassification, not a ground item: the waterbottle had just been created through the native pickup
(`[PickUpResult] waterbottle → slot (slot 1)`, `[ItemSpawn] … already present locally`) and was sitting in the giver's own slot.

**The fix** asks the authority instead of guessing from the gesture: `CarryForGive` answers true when
`domains.Items.OwnsCarriedItem(domains.Session.LocalSteamId, itemId)` — the same shared custody predicate the custody change introduced — and only
a genuine world item keeps the refusal (that path is the sibling ticket `give-from-the-ground-swallows-the-item`). `IItemControl` gained the one
member so the adapter can ask it.

**Machine evidence after the fix** (`.acceptance/batch-givefix`, deployed and verified):

```
20:10:20.422 [RemoteIntent] GiveToBody captured for item 5197989525 of 76561198863287957 (slot 0, body 76561198281246659)
20:10:20.620 [PlayerInteractionKernel] projected inventory transfer 76561198863287957 -> 76561198281246659
20:10:20.628 [StandingItem] materialized emergencylight (id 5197989525) for 76561198281246659 …
```

and the host's custody projection caught the hand-over and healed:

```
20:10:18.600 [ItemCustody] rows=260 objects=260 mismatches=0
20:10:20.624 [ItemCustody] rows=260 objects=261 mismatches=1     ← the giving client's object for one frame
20:10:21.630 [ItemCustody] rows=260 objects=260 mismatches=0     ← converged
```

**What this fixes and what it does not.** The give is reachable again, so the apply's `removed` guard and the native `PlayBackpackSound()` call
run on the giver. Whether the CLIP is audible is row 2 and stays the user's judgement — that row is judged next time they are at the keyboard,
not claimed here.

## Why rows 1 and 2 are not judgeable from the log today — and the one change that makes them so

Read while preparing the click batch, and it is the reason those rows keep coming back to the user's ears:

**`PlayBackpackSound`'s clip is not captured.** `SoundPlayPatch` reports a real `Sound.Play` call only inside a named call-identity scope
(`SoundPlayPatch.cs`'s own list: `Body.Attack`, `ThrowItem`, `TryExertSound`, `FootStep`, `PantSound.Update`/`TryGrowl`, `LockpingMinigame.Update`,
the local item-use scopes, `Body.HandleVisuals`, `PlayerCamera.ApplyWoundItem`, `FluidManager.DrinkLiquid`, `Body.CombineLiquids`), and the
classification is the pure `CharacterSoundPolicy`. `PlayerCamera.PlayBackpackSound` — the method the container branch and this give both call — is
**not** one of those scopes, so its `Sound.Play("backpack" + Random.Range(1, 3), …)` never reaches a `CharacterSoundMsg` and never appears in a log
line.

The consequence for the ticket is exact: row 1 asks for "the SAME native call in the log for a local move and for a give", and row 2 for the clip
"read by the same instrument". Neither instrument exists for this clip.

**The one change that makes both judgeable**: give `PlayBackpackSound` a call-identity scope of its own in the capture list, with
`CharacterSoundPolicy` classifying it as a local feedback sound (it is one: it plays where the move happened, on the mover's client). It does not
change what any client plays — it makes an existing play observable — and it is the difference between "the user says they hear nothing" and "the
log says the clip was or was not played on this client".

Until that lands, the two rows stay the user's judgement and this ticket says so rather than leaning on a reading that cannot exist.

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

## Closure (2026-10-25)

The question this ticket opened ("why is there no sound when I move an item into another player''s backpack?") was answered by the native code and
then RULED on by the user, in two steps:

1. The release now keeps the game''s own tail: `HandleReleaseDragging` ends with `PlayUISound(PlayerCamera.UISoundType.Click, 1f)`
   (`PlayerCamera.cs:1495`), and CUO consumed the give release before that tail ever ran. `PlayerCameraDragUsePatch` plays it on both consumed
   branches (the give and the ring-centre use) — the game''s call, the game''s clip.
2. The cloth rustle is NOT part of a cross-player give. `Body.PickUpItem` plays `PlayBackpackSound` only when the item''s parent is a container
   (`Body.cs:1393-1397`), and a slot-to-slot move has no container on either side. CUO had added its own `PlayBackpackSound` on the giver''s
   removal (`PlayerInteractionApply`), which the user heard as "the container sound that should not be there"; it is deleted (`7f9619d0`).

The user''s verdict on the machine, after both changes: "tested — the sound is fine now".

Rows 1-4 are therefore resolved by decision rather than by the audio instrument this page said was missing: the observable call is the UI click
(already inside `SoundPlayPatch`''s family through `PlayerCamera.PlayUISound`, which the patch sees), and the cloth clip is not played at all, so
there is no clip left to instrument for this gesture.