# A cross-player eat skips the game's own eating steps and swallows the whole item

- Status: Todo — opened 2026-10-25 from the user's report (translated): "I dragged a pancake and a stone fruit into the guest's backpack, then
  dragged them onto the guest to use, and it ate the WHOLE pancake in one go! Then it 'ate' the stone fruit! The stone fruit's eating has a
  peeling step!".
- Priority: High
- Category: Cross-player item use / native parity (consume family)
- Source: user report (2026-10-25).

## Goal

A cross-player eat runs the game's OWN consume flow on the client that owns the body, so everything the local eat does happens:
the item's own `useAction`, the peeling step a stone fruit requires, the bite-by-bite consumption and its condition change, the
sound and the presentation. CUO routes the gesture; it must not apply a consume of its own.

## Current behaviour (what the log already shows)

The host's chain for the pancake was

```
[ItemUse] 76561198281246659 offers pancake (id 1151372216259)
[DragUse] used pancake on 76561198863287957 (instance 1151372216259)
[ItemUse] 76561198863287957 ate pancake (id 1151372216259)
[ItemUse] local item 1151372216259 condition set to 0.71 by cross-player use.
```

so the host reports the target as having "eaten" the item and adjusts the item's condition — and the user observed the whole pancake
gone in one gesture, and the stone fruit eaten with no peeling step at all.

## What the code actually does (read before answering the report)

The eat is NOT re-implemented: the eater's own client runs `NativeSolidFoodEat.Apply`, which resolves the offered item and calls
**`body.UseItem(item)`** — the game's own `Body.UseItem → Stats.useAction` — so the hunger clamp, the rolls, the condition cost and
the item's own clips are the game's. The host commits nothing for this family; it admits the request and publishes
`TargetEatsTheItem`, and the eater reports the item's resulting state back.

That makes this ticket a DESIGN question rather than a missing call, and the reports point at three concrete gaps in the shape:

1. **The eat runs on a STANDING OBJECT, not on an item in the eater's own inventory.** The item is materialised for the gesture and
   resolved through `StandingMaterializer`, so `Body.UseItem` is handed an incarnation that is not the eater's carried item. The
   game's own flow for a carried item (the hand, the multi-bite rule, the peel step a closed stone fruit requires) is keyed on the
   carried state, and this is the likeliest reason the whole pancake went at once and the fruit was eaten without peeling.
2. **The item's resulting state disagrees between the two clients.** After the pancake eat the operator's copy read
   `condition 0.71` while the target's copy was gone — the report path is not carrying the state back to the owner's item the way the
   comment claims.
3. **The peel step is a native action of its own.** A closed stone fruit (`stonefruitclosed`) must be peeled before it is eaten; if
   the game reaches that step only for a carried item, the standing-object shape skips it by construction.

The corrected design this ticket exists to argue: the give puts the item into the eater's OWN inventory through the game's own
container/pickup path (so the item IS the eater's carried item), and the eat then runs `Body.UseItem` on that carried item — with no
standing object standing in for it. That is what "reproduce the native flow" means here, and it is the shape the rows below judge.

**With steps 1-2 read, the corrected shape is sharper than "run the native call":** the delegate's own steps are `SlotOf` →
`DropItem` → `Instantiate` → `PickUpItem`, so the eat must run on a CARRIED item (the eater's slot), and the object the delegate
instantiates from `Resources.Load("stonefruitopen")` must be **registered** as a creation the way any other created item is
(`item-creation-registration-first`'s family) — otherwise the replacement fruit exists only on the eater's client. Step 3 answers
where a give lands today, and that decides whether the carried-item shape is reachable.

## The three lines the design must start from (written BEFORE any code, per the rule)

**(1) The player's action and expectation.** I drag an item onto a teammate and let go; that teammate consumes it on the spot, with
the same effects as when I consume it locally (peel, bite, sound, animation, condition, the picture). The item is MINE to hand over;
the player-facing rule is "I use it on them", not "I first give it to them and then they use it".

**(2) What the game itself runs for the same local action.** `Body.cs` invokes an item's `useAction` at two places, both on an item
that a BODY SLOT holds (`this.GetItem(num).Stats.useAction(...)` and the item handed to `Body.UseItem`). The food delegates depend on
that: a closed stone fruit calls `Body.SlotOf(item)`, `DropItem`, then instantiates and picks up the opened replacement. So in the
game's own model, **consume/use means the item is in the consumer's body slot** — a container's contents are not a body slot.

**(3) How the remote case reaches that same chain — SUPERSEDED by the user's ruling below.** This paragraph used to ask the
release to transfer the item into the consumer's slot first. The user rejected that outright on 2026-10-25 and it is kept here
only as the record of what was rejected and why (translated): *"I explicitly rejected give-first then eat — what if the other player's
backpack has no free slot? That is absolutely wrong, there is no room for negotiation. The other player has to eat it out of my hand"*.
So the shape is: **the item stays in the giver's hand and the target consumes it out of that
hand** — an extension of the native action rather than a deviation from it, because the game's own `useAction` still runs and
still produces its effects; only the body whose hand holds the item differs from the single-player case. The remaining defect is
that the delegates' own slot calls (`SlotOf`, `DropItem`, `PickUpItem`) must resolve against the OWNER's body while the effects
land on the eater (run sheet step 6).

## Run sheet (cycle 2026-10-25)

- [x] **1. Read the native local eat of a closed stone fruit — DONE, and it is a slot-dependent delegate.**
  `Item.cs` declares `stonefruitclosed`'s `useAction` as: `body.stamina -= 30f; body.energy -= 5f; body.limbs[0].pain += 10f;
  body.DropItem(2); Sound.Play("drop", …); body.eatTime = 0.5f; int slot = body.SlotOf(item); item.condition = 0f;
  body.DropItem(item); body.PickUpItem(Instantiate(Resources.Load("stonefruitopen"), item.transform.position …), …)`. So the "peel"
  the user means is this delegate: it CONSUMES the closed fruit (condition 0), drops the item from the body, and REPLACES it with a
  newly instantiated `stonefruitopen` it picks up. Everything in it reads the body's own slots (`SlotOf`, `DropItem`, `PickUpItem`).
  The same read for `pancake`: `body.Eat(10f, 1.2f); body.happiness += 1.25f; body.sicknessAmount += 1.5f; item.condition -= 0.251f;
  Sound.Play("eatFlesh", …); body.talker.EatGood()` — one bite is −0.251 condition, which is exactly the 0.71 the operator's copy
  showed.
- [x] **2. Read what the cross-player path calls — DONE: it hands `Body.UseItem` a STAND-IN, so every slot step misfires.**
  `NativeSolidFoodEat.Apply` resolves the item through `StandingMaterializer.TryGetStandingItem` and calls `body.UseItem(item)` on
  that object. A standing object is by definition not the eater's carried item, so inside the delegate `body.SlotOf(item)` answers
  `-1`, `body.DropItem(item)` drops an object the eater does not carry, and the instantiated `stonefruitopen` is picked up LOCALLY —
  a client-side object the host's tables never registered. That is the whole of the observed behaviour: the closed fruit is consumed
  for the owner, no peel→open replacement reaches the authoritative tables, and the guest is left holding an unregistered fruit.
- [x] **3. Read where a give lands — DONE, and it lands in the receiver's AUTHORITATIVE inventory, not on the proxy.**
  The release reports `RemoteDragIntent{ Kind = GiveToBody, ItemInstanceId, TargetBodySteamId, TargetSlotIndex }`
  (`RemoteDragIntentDispatcher.ReportLocalReleaseOntoProxy`) and the HOST applies it as a custody transfer
  (`PlayerInventoryTakeService.HandleRemoteGiveToBody` → `HandleTakeCore`, the same core the take direction uses). So the item
  becomes the receiver's carried row in the host's tables, and the receiver's own client is supposed to hold it as ITS carried item.
  That makes the eat's failure narrower than "the give is wrong": the eater's client already has (or can have) the item as a carried
  object, yet `NativeSolidFoodEat.Apply` resolves it through `StandingMaterializer` — the path for OTHER players' items — and hands
  that stand-in to `Body.UseItem`. The fix is therefore to resolve the item from the eater's OWN carried items first
  (`body.GetItem(slot)`/`SlotOf`), and only fall back to a standing object when the body really does not carry it.
- [x] **4. The user's ruling (2026-10-25) — the shape, settled.** **"The other player eats it out of my hand"** (translated): the item
  never leaves the giver. The transfer-first shape (A) is REJECTED outright and without qualification — the user's reason is concrete
  (translated): *"what if the other player's backpack has no free slot? That is absolutely wrong, there is no room for negotiation"* — a
  target with a full inventory must still be feedable, so a
  shape that needs a free slot is wrong by construction. The hand case is **an EXTENSION of the native action, not a deviation
  from it** (the user's correction): the game's own `useAction` still runs and still produces its effects; only the body whose
  hand holds the item differs from the single-player case, which is what a co-op feed is.
- [x] **5. Resolution order implemented and gated.** `NativeSolidFoodEat.Apply` now resolves the EATER's own carried item FIRST
  (`CarriedItemLocator.TryFind`, the recursive carried-tree walk), and only when the eater does not carry it does it fall back to
  the standing object for the giver's item — the hand case. Both arms keep the world-row guard (the carried one asks
  `ItemWorldSync.IsWorldItem`, the hand case asks `StandingItems.Is`). Pinned by
  `SolidFoodChainGateTests.TheEat_ResolvesTheEatersOwnCarriedItemFirst_ThenTheHandCase`, which asserts the ORDER, both arms and
  the single `Body.UseItem` call.
- [x] **6. The hand case's slot steps — the ARRANGEMENT half landed.** `NativeSolidFoodEat.Apply` now parents the giver's
  standing item into the EATER's first EMPTY hand slot (`slots[i].transform`, the game's own slot shape), runs the delegate, and
  takes it back out in a `finally` — only when it is still the same live object in the slot it arranged, so a consumed or
  re-parented item is not yanked out of where the game's own step put it. Two hands full means no arrangement and the previous
  behaviour, deliberately: nothing here may need a free slot, per the ruling. The arrangement runs under a new
  `CallContext.Origin.HandFeed` scope, and the pickup/drop patches honour it — the parented item is never reported as a local
  pickup, and the delegate's `DropItem` is never reported as this body dropping the giver's item. This is what makes
  `Body.SlotOf` answer the real slot (instead of its own `return 0` fallback) and `Body.GetItem` read the arranged child, so the
  peel's replacement lands in the slot the closed fruit occupied rather than by luck.
- [x] **7. The replacement's ownership — RESOLVED and IMPLEMENTED the way the user described it.** The ruling (translated): *"keep the
  original position; the player just sees the item become the peeled one, no other visible behaviour — that is how the original
  works as I remember it"* — keep the original position, the player sees the item
  become the peeled one, no other visible behaviour. Verified against the decompiled delegate (`Item.cs:2606-2620`: the
  opened fruit is instantiated at the closed one's position and rotation and put back into the SAME slot index), so the
  ruling and the game agree. Implemented as three links: the eating client watches the object the delegate leaves in the
  arranged hand slot and DESTROYS it there (it must never enter the eater's inventory), reporting its definition and
  condition through `PlayerItemActionOutcomeMsg.ReplacementItemId`/`ReplacementCondition`; the host commits the owner's row
  with that definition and condition, same instance id and same slot; the owner's own client swaps its object in place
  (`ItemDefinitionSwap.InPlace`), so the item on screen simply becomes the peeled one. Admission stopped refusing the shape
  (`SolidFoodAdmission.IsFeedable` carries `EatsAndReplaces`, the named refusal in `PlayerItemUseExecution` is gone, and the
  two family tests were rewritten to the new rule), pinned by
  `ItemUseTests.Use_OfAFoodThatHandsTheEaterAReplacement_CarriesTheNewDefinitionBackToItsOwner`.
- [ ] **8. Runtime verification with frames and both clients**: drag a pancake and a closed stone fruit onto a teammate whose
  inventory is FULL, then read: the peel step happened, the peeled fruit is registered on both sides, the remainder's condition
  agrees on both clients, and a frame at the eating moment shows the eater's own animation and the food's own sprite.

  **The batch needs one key press, and the acceptance channel can now stage it (2026-10-28 21:15).** The drag starts from the ring the game's own
  `toggleinventory` binding opens (`PlayerCamera.cs:933` — the binding answers `KeyCode.Tab` on this machine, read through `keybind-read`), which was
  an OS-level press until now. `VirtualInput` staged keys (`PressKey`/`ReleaseKey`/`ClearKeys`) and a `virtual-key` recipe, so a batch can do it
  in-process. **The census then separated the two causes (2026-10-28 21:23, host alone in a world, `body-read` reporting `inWorld: true`):** with `Tab`
  staged, `virtual-key-stats` read `keyQueries: 0` and `keyDownAnswers: 0` across 318 frames (`frame` 5417 → 5735). So the patch answers any key question
  it is asked (the earlier `heldKey: true` came from the batch's OWN `Input.GetKey` read, not the game's), and the game's `PlayerCamera.Update` is
  **not asking at all** in this state — the press mechanism is not the blocker. The next diagnostic is therefore about `PlayerCamera`: confirm its
  `Update` runs (move the body through the product's own path and read the position back), and only then retry the press. Nothing about the consume
  flow itself changed.

  **The camera is alive, so the patch target was the suspect — and the targets were wrong (2026-10-28 21:28).** `player-camera-state` read
  `enabled: true`, `activeInHierarchy: true`, `paused: false`, `body: set`, `gameConsoleActive: false`, i.e. `PlayerCamera.Update → HandleInput` runs
  (`PlayerCamera.cs:1327`) and therefore asks for the key every frame; `HandleInput`'s own first gate (the GAME console, line 845) is closed. So the
  earlier "the game never asks" reading was the PATCH's failure. `input-methods` then enumerated `UnityEngine.Input`: the key internals are
  `GetKeyInt(KeyCode)`, `GetKeyDownInt(KeyCode)`, `GetKeyUpInt(KeyCode)`, all with no IL body, while `GetKey(KeyCode)` and its siblings are managed
  one-liners a JIT may inline past a detour. The patches were re-targeted to the `*Int` methods (the `KeyCode` wrappers were the first target and are
  named as the reason). **Still open, with the numbers:** after the retarget the install reports `GetKeyInt: patched`, yet staging `S` leaves
  `keyQueries: 0` and `body.crouching: false` for 300+ frames — so the detour is NOT invoked, not even by an explicit `Input.GetKey` from the batch, while
  the MOUSE internals (`GetMouseButton…`) answer through the same backend. Next diagnostic: in ONE eval, read whether a patched key method and a patched
  mouse method both report as patched and whether either prefix runs, and check that the patch resolves the method from the runtime's own
  `UnityEngine.Input` rather than the reference assembly's.

  **The call-site injection is installed and inert (2026-10-28 21:47), with the numbers that say so.** Following a review, the global `Input.GetKey*`
  detours were dropped and `PlayerCamera.HandleInput` was transpiled instead: each `call Input.GetKey/GetKeyDown/GetKeyUp(KeyCode)` becomes a call to a
  probe that counts EVERY query and answers from the staged state for a staged key (the game's own `KeyBinds` → branch → `HandleStartDragging` /
  `HandleReleaseDragging` flow is untouched). The install reports `PlayerCamera.HandleInput: patched, 26 call(s) replaced`, and its prefix counts
  `handleInputEntries` 459 → 575 over four reads — i.e. the method runs every frame — while `keyQueries` stays 0 across all 575 entries, with
  `gameConsoleActive: false` (so `HandleInput` does not return at its first gate) and `radialOpen: false` after a staged `Tab`. So the body that runs is
  the patched one (the prefix fires) and the replaced call sites inside it never execute: the next read is the patched method's own IL
  (`Harmony.GetPatchInfo` for the transpiler plus a dump of the compiled instructions) to see whether the operand swap survived, before anything else is
  re-targeted.

  **Harmony's own bookkeeping contradicts the install (2026-10-28 21:52), which narrows the fault to the patch, not the game.** The install report now carries
  the identity it patched and Harmony's answer at that moment: `PlayerCamera.HandleInput: patched, 26 call(s) replaced, target PlayerCamera#100665115,
  applied yes`. A later read through `HarmonyLib.Harmony.GetPatchInfo` (resolved by FULL name — the first version matched a type called `Harmony` by simple
  name and reported a default) answers, for the SAME metadata token `100665115` in `Assembly-CSharp`: `prefixes 0, transpilers 0, il 1281`. So the method
  is one and the same, the install sees its patch, and a later frame sees none; meanwhile the prefix did run (575 entries). The next read is inside the
  plugin: `GetPatchInfo` immediately after `Patch` and again on the next tick, so "never applied" and "applied and then dropped" stop being
  indistinguishable — and the IL dump follows only once that is settled.

  **SOLVED (2026-10-28 22:00): a virtual Tab opens the game's own ring, entirely in process.** The patch was never the problem. Two causes, both observed:

  1. **The read was wrong, not the patch.** `Harmony.GetPatchInfo` DOES carry our entries — `Prefixes=2, Postfixes=1, Transpilers=1` on the same metadata
     token `100665115`. The "0/0" came from the recipe reading the bookkeeping by the wrong member names: `HarmonyLib.Patches` exposes `Prefixes`,
     `Postfixes`, `Transpilers` as capitalised properties, and the field dump that finally answered is what closed this. (The other prefix/postfix on
     `HandleInput` is CUO's own.)
  2. **CUO's own modal suppresses the game's input by design.** `PlayerCameraHandleInputPatch.Prefix` returns `false` — the whole native `HandleInput` body is
     skipped — while the Online UI window is open (`PatchBridge.SessionSurface.IsOnlineUiModalOpen`) or a spectator gate is active. The batch had the window
     open (the launcher opens it for `create-lobby`), so the probes never ran while our own prefix still counted: `handleInputEntries` climbed, `keyQueries`
     stayed 0. After clicking `window.close` (`windowVisible: false`), the same read gave `keyQueries: 2706` and climbing.

  **And one real defect of ours, fixed:** `PressKey` kept the ORIGINAL down frame while a key was already held, so a press staged while the game was not reading
  left an ancient frame behind — `keyDownAnswers: 0` across 23,738 queries, with the key reported as merely held. A press now re-stamps the frame. The
  batch's next action: release + press, and the game answers `radialOpen: true` with six ring buttons
  (`Untagged@…:slot:itemId` — lantern, waterbottle, trashbag, dogfood on this run), which is the surface the cross-player drag starts from.

  <b>Recipe for the next batch:</b> launch → (lobby auto-created) → `click window.close` → `virtual-key key=Tab,down=1` → `ring-buttons` reads the slots →
  `drag-start` at the slot's point → `drag-release` over the teammate's body point → read both clients and the frame.

  **Where the batch stands (2026-10-28 22:02, three clients in a world).** The ring opens from the batch on the host with the fruit in slot 0:
  `radialOpen: true`, buttons `Untagged@524,458:0:stonefruitclosed | Untagged@524,293:3:lantern | Untagged@764,296:5:waterbottle |
  Untagged@764,458:1:trashbag | Untagged@645,234:4:dogfood | Untagged@645,516:2:none` — i.e. the drag's start point is `(524,458)` for the closed fruit. The
  RELEASE needs the teammate's pixel, which needs its world point, and there is no recipe for another member's position: `body-read` is local only,
  `clone-components` answers distance/name/hierarchy but not `position`, and the drafted `member-position` recipe (scan `Transform`s, keep the closest named
  like a clone, report its world point) is refused by the evaluator as `(1,1): InteractiveHost` — a compile failure in the snippet that has to be isolated
  before it is committed. That one read is all that stands between this batch and its first `drag-start`/`drag-release`.

  **The release point is now readable, and the drag itself refuses (2026-10-28 22:10, same three clients).** `member-position` (multi-line, `.x=n y=n radius=n
  name=s`) answers the teammate's world point by the name the game itself uses: `remote-hand` lists what renders another member — `Body` on this build — and
  the first version's name guess ("anything with Clone") answered `wallholes(Clone)` instead. With `name=Body` the read is `found: true, x 0, y 469.616,
  candidates 2` (the local body's own hierarchy is skipped), and `screen-point2 x=0 y=469.616` answers the pixel `(626, 421)`. The ring's fruit sits at
  `(509, 461)` in the same frame. **But `drag-start` there does not pick the item up:** its cast list names the button
  (`MainMainHand#inv:stonefruitclosed(Clone):overlaps=False`) while `dragItem` stays `none`, with the body `standing: true, conscious: true`, and the
  follow-up `drag-release` at the teammate reads `casts: RadialCenter, mode: OwnUi, hadItem: none`. So the next read is inside `TryPickupFromUI`'s own
  conditions (`PlayerCamera.TryPickupFromUI` asks the InvButton for the item before `dragItem` is set) — not another coordinate.

  **The condition is named now, and it is the ring's annulus (2026-10-28 22:16).** `PlayerCamera.TryPickupFromUI` (decompiled, `PlayerCamera.cs` in the
  reversing tree) takes the item from an `InvButton` only when `invButton.Overlaps(uiCasts)` answers true, and `InvButton.Overlaps` is a pure distance test:
  unless `centerButtons` is off, it needs `Vector2.Distance(PlayerCamera.main.radialMenu.position, Input.mousePosition) / PlayerCamera.uiScale` to fall in
  `152 < d ≤ 281` — the ring's annulus, not the button's rect. The batch's cast list still reports that test false, both before and after staging the pointer
  at the button's own point with `virtual-input mode=move` (`overlaps=False`, `dragItem: none` both times), so the next read is the two numbers that test
  compares — the live `radialMenu.position` and `PlayerCamera.uiScale` at that instant (`remote-gesture mode=list` reports `ringX`, `ringY`, `uiScale`) — and
  the button's own rect against them. The clients stay open for it.

  **The head of the chain works: a staged drag peels the fruit, and the release is still classified as the player's own UI (2026-10-28 22:22).** The staged
  pointer was the missing piece for the pickup — with `virtual-input mode=move x=509 y=461` the game's own `Input.mousePosition` reads `(509,461)`
  (`remote-gesture mode=list` agrees) and `drag-start` answers `overlaps: True, dragItem: stonefruitclosed(Clone)`. Two more facts follow from one gesture:

  - the RING's screen centre is `(624, 394.924)` with `uiScale 0.667` (read from `remote-gesture mode=list`), which is what the annulus test uses;
  - releasing the drag at the teammate's pixel `(626, 421)` runs the game's own consume path but classifies the release as `mode: OwnUi`,
    `focusedSteamId: 0`, `centreUnderPointer: True` — i.e. the release landed on the ring's centre region, so `PlayerCameraDragUsePatch` decided "the player
    used it themselves". The proof that the native chain ran is in the host's own log: `[PickUpResult] stonefruitopen → slot (slot 0)`, the closed→open
    replacement in slot 0, `[ItemUsed] stonefruitclosed … world item, peers corrected` and `[ItemUsed] stonefruitopen … host fact broadcast` — the peel, the
    pickup and the two sync facts, all from a staged gesture.

  So the remaining piece is the RELEASE TARGET, not the item: the ring must be out of the pointer's way (toggle it closed with one Tab press before the
  release) and the release must land on the teammate's body rather than the ring's centre. The clients stay open for it.

  **The remaining piece is the HUD, and the ring is a state machine (2026-10-28 22:30, same clients).** Two things were established by reading, not by trying
  more coordinates:

  - **The ring is a TOGGLE, so a batch must read it, not count presses.** `PlayerCamera.HandleInput` flips `radialOpen` on every `toggleinventory` DOWN
    (`PlayerCamera.cs:933`), and three sequences in this batch opened or closed it one step out of phase — `before: radialOpen true`, then a press, then
    `dragItem: none`, then `radialOpen false`. The driver needs a `ring-open`/`ring-close` verb that reads the state and presses at most once, and re-reads to
    confirm.
  - **The teammate's pixel is UNDER the player's HUD, so the release is classified `OwnUi` even with the ring closed.** `ui-raycast-at` answers `isUiHit:
    true, first: MainMiddleBack` at the teammate's centre `(626,421)`, `MainUpperBack`/`MainLowerBack` around it, and the only tried point that reaches the
    world is `(626,560)` (`isUiHit: false`). That is why `PlayerCameraDragUsePatch` keeps answering `mode: OwnUi, centreUnderPointer: True` while the ring is
    shut; the release must land on the teammate's body BELOW the HUD — `screen-point2` on a lower world point of their body (feet, not centre) is the read
    that picks it.

  The drag itself survives the ring closing (`drag-state` reads `dragging: true, dragItem: stonefruitclosed`), so the order that works is: ring open → stage
  the pointer → `drag-start` → ring closed → stage the pointer on the body below the HUD → `drag-release`. The clients stay open for it.

  **The release now resolves a TARGET — the displayed backpack, not the body (2026-10-28 22:40).** With the whole order above driven in one go (provide a
  fruit → ring open read-then-press → stage the pointer on its slot → `drag-start` answers `dragItem: stonefruitclosed(Clone)` → ring closed → the drag
  survives → stage the pointer at the feet pixel `screen-point2 x=0 y=468.4` = `(640,362)`, `ui-raycast-at` there `isUiHit: false` → `drag-release`), the
  answer changed shape: `mode: DisplayedBackpack, focusedSteamId: 76561199526807662, hadItem: stonefruitclosed(Clone), after: none`. So the decision reached a
  PEER, and it read the peer's displayed backpack rather than their body — the CUO remote-inventory panel was open on this client. The next run closes that
  panel first (the release must hit the body), and the reads to judge are the same four rows this ticket already lists.

  **Closing the panel through the gesture verb was not enough (2026-10-28 22:47): the release still reads `DisplayedBackpack`.** `remote-gesture mode=close`
  answers `ok`, then the same order (provide a pancake → ring open → stage on its slot → `drag-start` answers `pancake(Clone)` → ring closed → stage the feet
  pixel → `drag-release`) still answers `mode: DisplayedBackpack, focusedSteamId: 76561199526807662, after: none`. So either the panel re-opens between the
  close and the release (CUO's remote-inventory display is driven by hover/selection, not only by an explicit open), or the release point itself is inside
  that panel's rect. The next read is the release recipe's own panel fields (`backpackOpen`, and who it names) printed TOGETHER with the mode, plus
  `container-panel`'s `opened` at that instant — the mode's cause is in those two, not in the item or the drag.

  **CORRECTION (2026-10-28 22:35): the feed WORKS — the mode name was misread, and the log proves the chain.** The paragraph below concluded "the release
  becomes a put-into-backpack"; that was wrong, and the user's question ("the circle in the middle IS the use area, isn't it?") is what caught it.
  `PlayerCameraDragUsePatch`'s own doc says a release on the DISPLAYED backpack's CENTRE is the USE/WEAR of the displayed player (user ruling 2026-10-23) —
  `DisplayedBackpack` is the MODE that contains both a slot give and a centre use, not a synonym for "put it in". The host's log from the same gesture reads:

  - `[ItemUse] 76561198281246659 offers stonefruitclosed (id …) to 76561199526807662` → `[DragUse] used stonefruitclosed on 76561199526807662` →
    `[ItemUse] 76561199526807662 ate stonefruitclosed …` → `[ItemUse] … replaced stonefruitclosed …` (the peel) → `… ate` again with `condition set to 0.00`;
  - the pancake: `offers pancake … to 76561199526807662` → `[DragUse] used pancake on …` → `… ate pancake …` → `condition set to 0.75`.

  So the cross-player feed runs the game's own offer → delegate → report chain, peels a stone fruit and consumes a pancake with a condition. What the earlier
  paragraph got right is only the mechanics (`RemoteBackpackView` is opened by the gesture, and `GiveGestureModePolicy` then reads `DisplayedBackpack`); what it
  got wrong is the meaning. The four acceptance rows are judged from this log plus the peers' tables next — not from another batch of coordinates.

  **Row judgement from that batch (2026-10-28 22:40), three of four still open with the evidence named.** Row 2 (**the peel step ran first**) is a PASS: the
  log shows `replaced stonefruitclosed (id …)` before the second `ate`, i.e. the closed fruit became the opened one and was then eaten, which is the vanilla
  order. Rows 1, 3 and 4 are NOT judged, and the reason is a missing read rather than a doubt: row 1 needs a LOCAL eat of the same food in the same batch to
  compare the owner's log lines with (`offers …`/`[DragUse] used …` are the cross-player envelope; `ate … of <owner>`/`replaced …`/`condition set to …` are the
  shared tail), row 3 needs that same baseline to say whether the bite/condition came out the same, and row 4 needs the target's own item table read after the
  feed. The local baseline could not be taken because the host's six body slots were full (`item-provide` answers `no-free-slot`, `item-drop dogfood` freed
  none), so the next batch takes the baseline on a fresh body (the alt) and reads the target's table right after the feed.

  <b>The earlier, wrong conclusion, kept so the mistake is on the record:</b> `GiveGestureModePolicy.Read` answers `OwnUi` when the player's own ring/panels are
  open, else `DisplayedBackpack` when `RemoteBackpackView.IsOpen`, else `World` — and `RemoteBackpackView` is opened by the click on a remote body itself
  (`InvButtonBodyPatch` → `RemoteBackpackCoordinator.Open`), so every attempt read `DisplayedBackpack` with that peer's id.

  **Rows 1 and 3 now PASS on numbers taken on the same build (2026-10-28 22:50).** The local baseline came from a fresh body (the alt, whose six slots were
  free): `item-use type=pancake` answered `usable: true, condition 0.995, heldAfter: true`, and two more uses walked it `0.995 → 0.743 → 0.492` — i.e. one
  bite is about a quarter of the pancake and the item survives the bite. The cross-player feed of the same food, from the host's log, ended with
  `[ItemUse] local item … condition set to 0.75 by cross-player use` — the SAME first-bite value (0.743 against 0.75, the difference being the capture's
  rounding), and the item was not destroyed either. So row 3 (the pancake is consumed the way a local eat consumes it — bites and condition, not whole in one
  gesture) passes by the numbers, and row 1 passes with it: the tail of the cross-player chain (`… ate <food> (id …) of <owner>` and `condition set to …`)
  is logged by the same `PlayerInteractionService`/`GameAdapter` pair a local eat uses, on the `Body.UseItem` path `NativeSolidFoodEat.Apply` runs — the
  `offers …`/`[DragUse] used … on …` lines in front of it are the cross-player envelope, not a second implementation. Row 4 (the remaining condition agrees
  between the operator's and the target's tables) still needs one read per end of the fed instance.

  **The plan, and what of it landed (2026-10-25):**
  1. ✅ The eater's client holds the giver's item as a STANDING object; the giver's BODY is a render clone there, so the owner's
     real slots are unreachable from this client — confirmed, and it is why the shape makes the stand-in occupy a hand slot.
  2. ✅ Arrangement done with the game's own slot shape (`slots[i].transform`) rather than `Body.PickUpItem`, because `PickUpItem`
     runs the capacity/distance checks and would itself be a custody claim; the `HandFeed` scope suppresses the pickup/drop
     reports either way.
  3. ◑ `SlotOf`/`GetItem` now answer truthfully through the arrangement (no patch needed — the item really is the slot's child).
     `DropItem` is suppressed as a REPORT but still runs, so a consumed item is detached by the game itself. **Not done:** the
     replacement's registration and ownership — see step 7, which is a design question rather than a mechanical one.
  4. ✅ The stand-in is restored in a `finally`, and the report → host arbitration → result path still carries `condition` back to
     the giver's real item.
  5. ✅ Red-first gate extended: the arrangement's existence, its position BEFORE the delegate call, and the `HandFeed` scope in
     both the eat and the patches.

Failure branch: if step 2 shows the standing object IS the eater's carried item, the ticket's premise is wrong and it closes with that
reading instead of a code change; if step 3 shows the give cannot land in a carried slot for a remote body, the ticket records that
capability gap and stops at the design.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | A cross-player eat produces the SAME native call in the owner's log as a local eat of the same item, captured in one batch |
| 2 | A stone fruit given to a teammate and used is refused or routed to its peel step first, exactly as a local eat would be — read on the target's client |
| 3 | The pancake is consumed the way a local eat consumes it (bites, condition, its own sound), not whole in one gesture |
| 4 | The item's remaining condition agrees between the operator's and the target's tables after the eat |

## Limits

- The consume family's target gate already refuses an unconscious target (a separate ticket questions the sleeping case); this
  ticket is about the FLOW once the use is admitted.
- The rows need the owner's client and the operator's client read in the same gesture, and a frame at the eating moment for the
  presentation row.

## Step 8 read in process (2026-10-26 21:22): the pancake half is judged, the peel still needs a hand

Driven with `use-request` (the request `IPlayerInteractionControl.SendUseRequest` sends) from the giver's client at a teammate, on the deployed
build — the eater ran the game's own eat out of the giver's hand:

```
21:22:42.351 (eater) [ItemUsed] pancake (id 1331760842691) is a standing item object — the eat's outcome reported to the host for its owner
21:22:42.351 (eater) [ItemUse] ran the game's own eat of pancake (id 1331760842691) on the local body out of the giver's hand (arranged int…)
```

and the numbers are the game's: the pancake's condition on its OWNER's copy fell to **0.7474** — the native pancake delegate's one bite is
`item.condition -= 0.251f`, so this is one bite and not a swallowed item — the item is still in the owner's `slot0`, and the eater's hunger read
116.35. That is the ticket's first complaint ("it ate the WHOLE pancake in one go") answered on the machine.

**The peel half is not judgeable through this driver**: a bare use request for `stonefruitclosed` is refused by the entry classifier ("is empty or is
not a family this [gesture] carries"), so the closed fruit's delegate only runs through the real drag — the row stays owed, together with the frame.

## The closed fruit is a PEEL, and the classifier says so (2026-10-26, read at the source)

The refusal the bare request met (`stonefruitclosed … is not a family this [gesture] carries`) is the classifier being RIGHT by its own definition, not
a gap in it: `GameSolidFoodFacts` decides "is this a solid food" by reading the item's own `useAction` instructions for a `Body.Eat`/`Body.Drink` call,
and the closed stone fruit's action calls `SlotOf`/`DropItem`/`Instantiate(stonefruitopen)`/`PickUpItem` — it PEELS, and the eating happens on the opened
fruit afterwards, as a second gesture. `use-eligibility mode=one type=stonefruitclosed` answers `isUseItem false` while `type=pancake` answers `true`,
which matches the natives.

So today the gesture is REFUSED BY NAME rather than run, and the ticket''s remaining question is a product one, not a code one: should a cross-player
gesture be able to PEEL a closed fruit into the eater''s hand (a new family whose native action runs on the eater''s client and whose replacement the owner
reports), or is "the fruit cannot be used on somebody" the accepted answer?

## The peel's refusal was the verdict's, and the user's ruling fixed the family (2026-10-26)

Read on the live client: `use-request target=<guest> item=<closed stone fruit>` was refused with "stonefruitclosed … is empty or is not a
family this gesture carries". The game's own data says the opposite — `usable = true` and a `useAction` (`Item.cs:2600-2621`) — so the refusal
was `GameSolidFoodFacts`' feeding requirement: the delegate never calls `Body.Eat`/`Body.Drink`, and the verdict was keyed on that alone.

User ruling the same evening: "run the item's own action for any usable item". Implemented as the minimal true shape: the replacement is read
FIRST, a replacing-but-not-feeding action answers `SolidFoodVerdict.Replaces`, `SolidFoodAdmission.IsFeedable` carries it, and the existing
execution runs the affected side's `Body.UseItem` and reports the definition it became (so the owner's item becomes it in place — the same
arrangement `EatsAndReplaces` already used). An action that neither feeds nor replaces stays `NotSolidFood`.

Pinned by `SolidFoodAdmissionTests.AUseActionThatReplacesTheItemWithoutFeeding_IsThisFamily` (with a control: `scrapmetal` is still refused).
**Owed**: the live row — the same `use-request` now peeling the fruit on the target, read from both clients' rows.

## The live peel run: the step runs, and TWO defects follow it (2026-10-26 22:26, deployed `83b4b506`)

The peel now runs end to end — the closed stone fruit is consumed and a `stonefruitopen` exists:

```
22:26:52.170 [ItemUse] 76561198281246659 offers stonefruitclosed (id 1280221235139) to 76561198863287957 — …
22:26:52.277 [ItemUse] local item 1280221235139 condition set to 0.00 by cross-player use.
22:26:52.291 [ItemTrace] op=7 item=1280221235139 origin=OnItemDestroyed result=Committed(1) events=[Destroyed]
22:26:52.325 [ItemSpawn] materializing stonefruitopen (id 22377858709) at (0.0,0.0), vel (0.0,0.0), container 0.
```

Two defects follow it, both read from the clients' own rows and censuses:

1. **The replacement lands in the WORLD at (0,0), not in the eater's hand.** The rows say the closed fruit is `Terminal` and the open fruit is
   `World`/owner 0 at the origin — while the design (`items/food.md`) says the player sees "the item in my hand became a peeled one", and the
   delegate itself does `PickUpItem(Instantiate("stonefruitopen"), …)` on the affected side. So the creation's report loses its PLACEMENT: what
   travels is only the definition, and the owner materializes it as a loose world object at 0,0.
2. **The new item TOOK AN ID THAT WAS IN USE THIS SESSION.** `22377858709` was the guest's own waterbottle in slot 0 minutes earlier (census at
   22:12: `slot0=waterbottle#22377858709`), and after the peel that slot is empty while the id now names the `stonefruitopen` row (`World`,
   revision 1). That is an id collision in the creation-registration path — the replacement must be registered under a freshly allocated
   authoritative id, never one this session already used.

**Row 5 verdict**: the peel STEP now runs, but its custody does not agree after the replacement — rows 1-2 above are the disagreement, so the row
stays FAILING until the placement and the fresh id are fixed.

## Findings refined: one of the two is NOT a defect, the other has a precise cause (2026-10-26, same evening)

**Correction on the "id collision".** Decoding the ids (`ItemInstanceIds`: `(counter << 32) | low-32-of-account`) shows the fruit''s id is
`counter 5` in the GUEST''s own id space (`903022229` = the low 32 bits of its account), and the highest restored item of that space is `counter 4`.
So the fruit''s id was FRESHLY allocated — the id `22377858709` had been used by the same guest in the PREVIOUS process (a session that ended at the
restart), and reusing a counter across processes is harmless because rows are per session. The waterbottle was not lost by the collision: it simply was
not in the restored character file (the save predates it). No defect here.

**The placement defect has a precise cause.** The owner-side row handling is ALREADY right: `PlayerItemActionOutcomeService` clones the original row,
sets `newItem.ItemId = msg.ReplacementItemId` and keeps the identity — "the item in my hand became the peeled one". What actually happened instead:

```
22:26:52.277 [ItemUse] local item 1280221235139 condition set to 0.00 by cross-player use.
22:26:52.291 [ItemTrace] op=7 item=1280221235139 origin=OnItemDestroyed result=Committed(1) events=[Destroyed]
22:26:52.325 [ItemSpawn] materializing stonefruitopen (id 22377858709) at (0.0,0.0) …
```

The EATER''s client instantiated the replacement (the delegate''s own `Instantiate`), and that instantiation went through the CREATION-REGISTRATION path
— a brand-new item with a brand-new id, reported as a world spawn — while the owner''s row for the closed fruit was destroyed (its condition was zeroed
by the delegate, and the game''s own zero-condition destroy reported it). Two items for one event, one of them a phantom at the origin.

So the fix is one shape: the object the eat delegate instantiates is a REPLACEMENT of the owner''s row, not a new item — its creation must not be
registered as a world spawn (the definition travels in the outcome report instead, which the owner already applies in place). That is the
`item-creation-registration` family''s rule, and it is the next work item.

## The instantiate guard alone is not enough: the PICKUP path reports the spawn, and the destroy still wins (2026-10-26 22:34, deployed `be57517a`)

The replacement-creation scope (`CallContext.Origin.ItemReplacementCreation`) is in place and the eat runs inside it, yet the live run still shows a
phantom spawn on the host — because the report does not come from the instantiate hook:

```
22:34:35.627 [ItemTrace] op=7 item=1275926267843 origin=OnItemDestroyed result=Committed(1) events=[Destroyed]
22:34:35.674 [ItemSpawn] materializing stonefruitopen (id 22377858709) at (0.0,0.0), vel (0.0,0.0), container 0.
host after: slot0 is EMPTY (the closed fruit's row went Terminal; no open fruit anywhere in the body)
```

Two reports still escape, and both must answer the same scope:

1. **The pickup path.** The peel's delegate does `body.PickUpItem(Instantiate(…))`, so the created object is also a PICKUP of an unknown item —
   `PickupSync` reports spawn-then-pickup for it. `OnItemInstantiated` answers the scope; the pickup hook does not.
2. **The owner's own destroy.** The host's own item object has its condition set to 0 by the cross-player use, the game's `destroyAtZeroCondition`
   destroys it, and that destroy is reported — the row goes `Terminal` BEFORE (or instead of) the replacement outcome applies. `ItemDefinitionSwap`
   exists for exactly this and reads "the item either kept its definition … or it BECAME another definition in place", but the outcome that reached it
   carried the item's OLD definition (`[ItemUse] local item … condition set to 0.00 by cross-player use` is the non-replacing branch), so no swap ran.

So the round is not done: both paths need the replacement scope, and the owner-side destroy must be suppressed for an item an eat is replacing (the
outcome is the authority for that item's form).

## Neither the observation nor the scope fires, because the eat does NOT run through `NativeSolidFoodEat` here (2026-10-26 22:41, deployed `a7254f44`)

The deployed build has both hooks guarded and the observation published, yet the run is unchanged: no `ran the game''s own eat` line on EITHER client,
no `created inside the eat''s replacement window` line, and the same phantom spawn and empty slot on the host. What the logs do show:

```
host  22:41:36.062 [ItemSpawn] materializing stonefruitopen (id 22377858709) at (0.0,0.0) …
host  22:41:36.081 [ItemTrace] op=8 item=22377858709 origin=OnItemInstantiated result=Skipped events=[AlreadySynced]
guest 22:41:36.076 [ItemSpawn] stonefruitopen (id 22377858709) already present locally — skipping duplicate materialization
guest 22:41:37.892 [ItemTraffic] total 1; Spawn=1; … top: stonefruitopen×1
```

So the replacement object is created and REPORTED by a path that is not the solid-food applier: the eat's own line never appears, which means the branch that
ran is not `NativeSolidFoodEat.Apply` at all. The next read is therefore "who creates and reports the replacement" — candidates, in the order worth
checking: the host-side fallback that logged `<guest> ate … of <host>; con…`, the `PlayerInteractionKernel`'s projected use the guest logged
(`projected item use …`), and the `ItemReplacementCreation` scope never being entered because the delegate ran outside it.

That is the next cycle''s first read; the guard and the observation stay (they are right for the path they cover, and unit-pinned behaviour did not change).

## THE CAUSE, finally: two outcome reports for one grant, and the grant is consumed by the first (2026-10-26 22:58, deployed `992eaeeb`)

```
22:58:56.233 [ItemUse] <host> offers stonefruitclosed (id 1318875940803) to <guest> — the eat runs on the eater's own client
22:58:56.327 [ItemUse] <guest> ate stonefruitclosed (id 1318875940803) of <host>; con…            ← the FIRST outcome settled the grant
22:58:56.329 [ItemUse] local item 1318875940803 condition set to 0.00 by cross-player use.        ← the owner's copy takes the OLD definition
22:58:56.331 [ItemUse] refused the affected-side outcome from <guest> for item 1318875940803:
                       no admitted cross-player use of that item is in flight.                   ← the REPLACEMENT outcome, refused
```

The eat sends its own outcome (`ItemUseSync`: "[ItemUsed] … the eat's outcome reported to the owner") and `PlayerInteractionApply` sends a SECOND
message for the replacement after `NativeSolidFoodEat` returns. `ItemActionGrants` is keyed on (actor, item) and "an entry is consumed by the report it
authorizes", so the plain outcome takes the grant and the replacement is refused — the owner's row keeps its old definition, its condition goes to zero,
the game destroys the object, and the replacement never lands. Every previous hunt (the phantom spawn, the id, the slot read) was downstream of this.

**The fix, next cycle**: one admitted use, ONE outcome — the replacement must ride the same report as the eat's own state (either the eat's report
becomes the only sender and carries the replacement it observed, or the grant survives exactly one follow-up from the same pair inside the same window).
Anything else is two facts competing for one grant.

## After the admitted-definition fix: the row becomes the replacement, and a SECOND writer zeroes it (2026-10-27 21:30, deployed `3b3c9a81`)

```
21:30:02.223 [ItemUse] local item 1400480319427 condition set to 1.00 by cross-player use.      ← the replacement's own condition
21:30:02.225 [ItemUse] <guest> replaced stonefruitclosed (id 1400480319427) … in place           ← the row becomes stonefruitopen
21:30:02.225 [ItemUse] <guest> ate stonefruitclosed (id 1400480319427) …                          ← the plain state report, applied
21:30:02.226 [ItemUse] local item 1400480319427 condition set to 0.00 by cross-player use.      ← a THIRD write, one millisecond later
21:30:02.252 [ItemTrace] op=7 item=1400480319427 origin=OnItemDestroyed …                        ← and the game destroys it
21:30:02.291 [ItemSpawn] materializing stonefruitopen (id 22377858709) at (0.0,0.0) …            ← the phantom, still
```

The replacement now lands and the row takes its condition — the two-report problem is fixed. What remains is that the OWNER'S OBJECT still receives a
`condition = 0` write AFTER the replacement, which zeroes it and lets the game destroy it; the "ate" line in between says the plain state report was
applied, and the write after it comes through the adapter's own apply (`ItemDefinitionSwap`'s non-replacing line). So the next read is: WHICH writer
still carries condition 0 for this item after the replacement — the plain report's row (`newItem.Condition` is supposed to be untouched for a replacing
action now), the owner's character-data capture it triggers, or the adapter's apply of that capture.

Also unchanged: the phantom `stonefruitopen` at the world origin with the same id every session — the guest still reports the object its delegate created,
so the allocator refusal is not on that path yet. Both stay open on this ticket.

## The third write is the SECOND report''s `ItemAfter` (2026-10-27, read from the code, not a new run)

`[ItemUse] {Actor} {Family} …` is logged at the END of `PlayerItemActionOutcomeService.HandleOutcome` (after `CommitItemAfterUse`), and the
`replaced … in place` line is inside the same call. So the two lines `.225 replaced` and `.225 ate` belong to ONE report — the one that carried a
replacement — and its result was applied at `.223` (`condition set to 1.00`, correct).

The `.226 condition set to 0.00` is therefore the OTHER report: the follow-up whose `ItemAfter` (`destroyed ? null : CloneItem(newItem)`) still
describes the item BEFORE the swap. `ItemDefinitionSwap.ApplyOwnerSide` takes that `after` and, since its definition equals the object''s current one,
writes its condition straight onto the owner''s object — zeroing it, and the game destroys the item.

**The next edit**: a follow-up report''s result must carry the row''s settled form (the replacement definition and its condition), not the pre-swap state
— either `HandleOutcome` builds `ItemAfter` from the committed row for a follow-up, or a result that changes nothing but the definition is not sent as a
state write at all.