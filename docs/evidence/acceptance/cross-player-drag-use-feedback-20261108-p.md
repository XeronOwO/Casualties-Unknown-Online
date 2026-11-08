# Cross-player drag use: the cue read on a live session (batch 20261108-p)

- Artifact: `0.1.0+9869d734` (acceptance channel; `verify-deploy` answered "Deployment matches this tree's build output").
- Session: `launch-clients.ps1` → lobby `109775243851228196`, host + Steam1 + Steam2, all in world 12:52:06.
- The host is the operator; Steam1 is the teammate whose displayed backpack is open.

## The fixture, step by step (host)

1. Steam1 teleported beside the host and settled at `(-2.085, 505.615)`.
2. `remote-gesture mode=open owner=76561198863287957 item= cast=0 moved=0 limb=0 frame=` → the displayed backpack opened (`backpackOpen` true, focus the teammate).
3. `ring-buttons` → six buttons; the display's ring centre was `(642, 403)` in that layout.
4. `virtual-input mode=move x=642 y=403 button=0` → the pointer on the centre; `remote-gesture mode=probe` read `radialMenuScale 1`, `radialOpen true`, `pointerDistanceOverScale 17.628` (inside the 135 radius), `uiScale 0.667`.
5. `drag-start type=paincream` → the held item is a pain-cream bottle (an item the release would use; the item stays in its slot).
6. `virtual-input mode=press-key button=1` → the `iteminteract` bind (`KeyCode.Mouse1`, 324) is held; the acceptance channel now transpiles the tracker's own read (`CueInputPatch`, committed in `9869d734`).
7. `drag-cue` → `{"showing":true,"targetSteamId":"76561198863287957","text":"<the localized use-on sentence>","backpackOpen":"True"}` — the text field read "use on 76561198863287957" in the Chinese catalogue (translated).

## The two negative readings

- **An ineligible item.** The same centre, with a lantern held instead: `drag-cue` → `showing false` — the cue refuses an item the release would not use, which is the policy's own clause.
- **The gesture's end.** After `virtual-input mode=release-key button=1` and `mode=release button=1`: `drag-cue` → `showing false` — no stale cue outlives the drag.

## What this settles

The cue's decision (`DragUseCuePolicy.Decide`) takes five inputs: a live session and world, a held item, the held `iteminteract` read, the item's family, and the displayed backpack's centre. All five read true in one live session on the deployed build, and the target is the teammate on whose panel the pointer sits. The label's sentence is the localized `cue.use_on` with the overloaded target; the overlay resolves the display name at draw time, so the probe prints the steam id inside the sentence.

## Limits, named rather than buried

- The drag was staged with `drag-start` (a field write); the earlier batches proved the game's own bracket sets the same field, and the tracker reads the field, not the bracket's origin. The held key is the part the acceptance channel had to add.
- The frame captured at that moment does not show the label: the CUO Example window sat over the cursor area, and the overlay draws beneath it. The state reading above is the evidence the overlay paints from.
- The "cursor leaves the centre while the gesture is still held" half was not read on its own this batch; the gesture-end reading covers the clearing, and the ineligible-item reading covers the hidden shape.

## The stacked pair: the cue's name and the release's pick agree

Both members were teleported onto the same spot beside the host (`(1.085, 505.615)` each; the host at `(0, 511.331)`), so the
ambiguity the naming rule exists for was real. The display was opened for Steam1 (`remote-gesture mode=open owner=76561198863287957`
→ ok), its ring centre read `(658, 615)`, the pointer placed there (`pointerDistanceOverScale 4.38`).

The key was held FIRST (`virtual-input mode=press-key button=1`) and the drag started after it (`drag-start type=dogfood`): the
order matters, because the game's own frame clears a drag the key does not hold.

- `drag-cue` → `showing true, targetSteamId 76561198863287957` — Steam1, the member whose displayed backpack is open.
- `drag-release x=658 y=615` → `mode DisplayedBackpack, hadItem dogfood(Clone), after none`.
- Host log: `[ItemUse] 76561198281246659 offers dogfood (id 1409070254019) to 76561198863287957 — the eat runs on the eater's own client.`
- Steam1's client: `[ItemUse] ran the game's own eat of dogfood (id 1409070254019) on the local body out of the giver's hand (arranged into slot 1).`
- Steam2's client carries the item only as a clone render — no use, no eat.

So with the two members on the same spot, the cue named Steam1 and the release acted on Steam1: the naming and the pick agree.

Limits: the display was opened explicitly for Steam1 (the auto-open path, which chooses WHOSE display opens, was not exercised here); the
probe's sentence carries the steam id, while the overlay resolves the display name at draw time.
