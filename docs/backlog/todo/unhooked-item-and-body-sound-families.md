# Local-only item and body one-shot sounds outside the ingest family

- Status: Todo
- Priority: Low-Medium
- Category: Character/item audio sync / report coverage
- Source: the whole-family audit of `review/host-eating-sound-not-heard-on-guest.md` (2026-09-26): fixing the reported meal meant giving two native paths their own capture scopes, and the same census showed every other local-only one-shot clip of that family still reports nothing.
- Related: `review/host-eating-sound-not-heard-on-guest.md` (the ingest half, landed), `review/sync-player-pain-vocalizations-and-bark.md` (the dedicated one-shot event family), `docs/architecture/remote-inventory-native-parity.md` (§3.4 and the stage-3 leave: "the item sounds inside a replayed call are heard where the call runs"), `todo/unhooked-damage-block-callers.md` (the same "the census found more callers than the fix covered" shape)

## The gap

The character-sound capture is call-identity scoped, and this cycle's fix added the two scopes the ingest
sounds needed (`CharacterItemUse` around the local `Body.UseItem` / `Body.UseItemInHand` action,
`CharacterBurp` around `Body.HandleVisuals`). Every other local-only one-shot clip still plays on the
client that ran the native call and nowhere else. The census (decompiled sites; `reversing/` line numbers
are stable):

| Family | Clips and native sites | Who hears it today | Why it is not carried yet |
|---|---|---|---|
| Medical / limb item use | `syringe` (Item.cs:784/830/1375/1539/1565/1613/1734/1760/1926/7123), `splint` (515/1483/1509), `goo` (616/1443/1464/1589/2553/2588/3947/3973), `boneweld` (696), `drainuse` (1658), `tweezeruse` (1700), `spray` (2100/2124), `laser` (4659), `wrenchhit` (4864), `cream` (`Liquids.cs:1074/1100`, the `onHealthUse` delegate reached from `WaterContainerItem.ApplyToLimb`) | The acting client only | These play at `limb.body.transform.position` — the PATIENT's limb, which may be another player's body — so the carrier needs a subject identity (whose body the sound belongs to) rather than the actor-follow the current event implies. The remote-medical family already judges operations on the target's client, so this must be decided with that domain, not in the audio layer. |
| World-liquid drink | `drink` (`FluidManager.cs:314`, the water branch's own play) and the `onDrink` clips (`Liquids.cs:1501`) for groundwater / lumalgae / oil / sap | The drinking client only | Reached from `Body.HandlePhysics` (Body.cs:3112) → `FluidManager.DrinkLiquid` (FluidManager.cs:286), i.e. outside BOTH capture scopes; only a state report (`FluidDrinkPatch`) wraps that call today. It needs its own scope or an explicit local decision — the container-drink half of "drinking" already rides this cycle's item-use scope. |
| Tool / utility item use | `combine` (Item.cs:4297/6700), `flashlighttoggle` (3998/4022), `error` (5672), `centrifuge` (5688), `drop` (242/2614) | The acting client only | Device/utility feedback whose state (craft result, light state, item drop) already syncs through its own domain. Decide per clip whether the peers hearing it is information or noise; `drop` in particular is the actor's own release feedback for an item whose world state already replicated. |
| Inventory gestures | `switch` (`Body.SwitchHands` Body.cs:1131, `Body.SwapSlots` Body.cs:1427), `combine` (`Body.CombineItems` Body.cs:1284), `waterpour` (`Body.CombineLiquids` Body.cs:1250) | The acting client only | Deliberately local: they are the actor's own inventory feedback and the resulting state (hand/slot layout, combined item, liquid amounts) already syncs. The remote-inventory page's stage-3 record says the same for the replayed-call cases (`combine`, `waterpour`, `batteryinsert`) and leaves them to the acceptance run. Keep as local unless the user's session says otherwise. |
| Body sickness / effort | `vomit1..3` and the 2D `vomitwarning` / `bloodvomitwarning` (Vomiter.cs:86/125/144/155), `stretch` (Body.cs:2510), `dogshake` (Body.cs:2553), the climb sounds (Body.cs:477) | The acting client only | A different component from the eat path (`Vomiter` runs its own coroutines), so it needs its own scope patches and a decision on the warning clips (which are 2D screen feedback, not world sound). |
| Remote-driven replay | an ingest use replayed by the item's owner through the inventory intent path (`RemoteIntentApplier.ApplyUseItem` → `Body.UseItem` under `RemoteApply`) | The owner's client only | The capture scope is deliberately local-action only (a remote-driven mutation must not report as the local player's action — the same rule every write-report patch follows). Carrying it needs an echo decision: whether the sound belongs to the owner (who ran the call) or the operator (who caused it), and whether the operator's own client would then double-play. |

Censused and deliberately OUT of this ticket (each already has a carrier or is not an actor sound):
`"pills"` at Item.cs:1214 and the container drink's `"drink"` / `"pills"` (`WaterContainerItem.cs:214`)
ride THIS cycle's ingest whitelist; `"speech"` / `"speechbad"` / `"talkSoundCustom"` (`Talker.cs:390/394/404`)
belong to the speech bubble family (`TalkerPatch` → `SpeechMsg`, and the eat only sets the string — the
sound plays later in `Talker.Update`); `waterflow1..4` (`FluidManager.cs:411`) is ambience, not an actor
sound.

The census method matters: the first cut of this ticket was built by enumerating literal `Sound.Play(`
calls in the file that DEFINES a use action, which missed every clip the called COMPONENTS play (the
container drink). Any future row must follow the delegate's callees, not just its text.

## What done looks like

1. Every row above is either carried by a decided mechanism, or recorded as deliberately local with the
   reason on the ticket — no row left unstated.
2. The medical/limb row is decided together with the remote-medical domain (whose client runs the call,
   whose body the sound belongs to), not by adding clips to the actor's event.
3. The census is gate-pinned the way this cycle's ingest census is: adding a new one-shot clip to a scoped
   native path without a classification (or a recorded local decision) fails a gate rather than silently
   staying local.
4. Whatever wire change a carried row needs bumps `ProtocolVersion.Current` in the same change.

## Non-goals

- Not re-deriving the ingest fix: that family landed in `review/host-eating-sound-not-heard-on-guest.md`.
- Not a general "every `Sound.Play` becomes a message" sweep — the remaining rows may legitimately stay
  local, and stating that with a reason is a valid outcome.
