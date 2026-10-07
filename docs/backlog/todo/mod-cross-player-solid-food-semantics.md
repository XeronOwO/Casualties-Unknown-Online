# Cross-player solid food from the game's own data

- Status: Todo — **cut 2026-10-08** out of `mod-cross-player-native-semantics.md` when that ticket's
  consume (drink) chain landed and its "Eat / drink" row split: the drink half had the native shape the
  migration is built on, the solid-food half does not. Its first step, the read-only side-effect
  investigation the design demanded, landed 2026-10-08 (findings below); the code has not started, and
  the investigation's one scope change is that the new object category must be named in code before any
  materialization.
- Priority: High
- Category: Mod platform / cross-player item use / architecture
- Parent: `docs/backlog/todo/mod-cross-player-native-semantics.md` (Part B)
- Related: `docs/backlog/todo/mod-content-ceiling.md` (Part 2 stage 2, its pre-cut statement),
  `docs/evidence/selfchecks/players/cross-player-native-drink-semantics-selfcheck.md` (the sibling
  chain that landed), `docs/evidence/selfchecks/players/cross-player-native-topical-semantics-selfcheck.md`,
  `docs/decisions/active.md` (184: the affected side judges on its own picture)
- Source: the 2026-09-25 mod-ceiling inventory (Part 2); this branch was named in the parent's chain
  table and cut on 2026-10-08 because its native shape is a different one.

## Why this ticket exists

`RemoteConsumeCatalog.Food` is the last hand-transcribed content table of the cross-player consume
family: 25 item ids, each with hunger/thirst/weight/happiness/sickness/radiation deltas and a condition
cost, transcribed out of the items' decompiled `useAction` delegates. The drink half of the same file —
and the whole `RemoteDrinkMedicineCatalog` beside it — was deleted on 2026-10-08
(`mod-cross-player-native-semantics.md` Part B), so the ceiling this table imposes (no mod food, no
vanilla food the table never listed, a game update silently moving the arithmetic) is now the family's
only remaining one.

## What the game itself offers (measured 2026-10-08, `reversing/Assembly-CSharp/Assembly-CSharp/`)

| Native fact | Where | What it gives us |
|---|---|---|
| `ItemInfo.useAction` is `delegate void Use(Body body, Item item)` | `ItemInfo.cs:188`, field at `:109` | the per-item behaviour is a DELEGATE, not data; the body it feeds is the first parameter |
| The native use dispatch | `Body.cs:2475-2481` (`if (item.Stats.usable) item.Stats.useAction(this, item)`), called from `PlayerCamera.cs:1646` | one entry point, and its target is always the caller's own body |
| The eating body methods | `Body.cs:2259-2294` `Eat(float hungerAmount, float weightGain)`, `Body.cs:3680-3685` `Drink(float amt)` | the bulk of a food effect is two native calls with numeric arguments; `Eat` also clamps at 125, converts overflow into sickness and rolls vomit/burp |
| The rest of a food effect | e.g. `Item.cs:2380-2389` geofruit, `:2408-2414` bread, `:2430-2437` cake, `:3614-3693` browncap | direct body-field writes (happiness, temperature, sickness, radiation, adrenaline, septic shock), a per-item `item.condition -= X`, item transforms (a bitten sprite, a bucket becoming an empty bucket) and a `Sound.Play` at the eating body's position |
| No food data exists | `ItemInfo.cs:87-185` (no nutrition field), `LiquidItemInfo.cs:1-15` (capacity/autoFill/defaultContents only) | the amounts are literals in the delegate; the only exception is the `NonDescriptCan` component (`NonDescriptCan.cs:67-83`), which is not an `ItemInfo` |
| The table is incomplete | **41** vanilla items whose `useAction` calls `body.Eat` / `body.Drink` (plus the component-driven `nondescriptcan`), against the table's 25 ids | mod food and the unlisted vanilla food have no cross-player semantics today |

The fact that decides the design: **a food delegate writes the eating body AND the item it is handed**
(`item.condition -= 0.34f`, `item.GetComponent<SpriteRenderer>()`, `body.DropItem(item)` in the two
bucket cases), and the affected side's client has no item instance. The drink chain did not have this
problem: `WaterContainerItem.Drink(Body body, float amount, string sound)` is a single divertible call
whose effect is a liquid delegate that takes a `Body`, so the operator could measure it and the patient
could run it.

## The design — settled 2026-10-08 by the user: objects follow data

**The principle.** If the synchronized data says an item exists, the item must also exist as a local
game OBJECT on every client that holds that data. Data and object are meant to correspond; today they do
not for items the local machine never had to draw or simulate (an item inside another player's
inventory has a data row on every client and no object anywhere except on its holder's). That gap is
what this ticket closes, and closing it is a platform change rather than a food-only change.

**Shape of the change (to be written up before code).**

1. **Materialize on the data signals.** Every place the item data learns about an item — the full table
   on world entry and reconnect, each committed batch's create/destroy/state-change, and container
   contents recursively — gets a corresponding object create / update / destroy on the client. The
   signals already exist; only world items are wired to them today (`ItemApplication`,
   `ItemReconcile`, `ItemSnapshotService`).
2. **Objects are kept aligned by an explicit apply path.** Data changes arrive; something writes them
   onto the objects. This path is mandatory rather than optional — without it the objects go stale while
   the data stays fresh. World items already have it (the position stream plus the item application);
   the rest need it.
3. **The objects are not the display proxies.** `RemoteCloneRender`-marked presentation copies of
   another player's items are a rendering concern with their own lifetime and are deliberately excluded
   from every authoritative report path; the new objects are the data's local incarnation and must be
   kept in a separate category from them, with that separation stated in the code, not implied.
4. **Then the food chain is ordinary.** With the item object present on the eater's client, the eat is
   the game's own `Body.UseItem` → `item.Stats.useAction(body, item)` run against the eater's own body
   and that object: the clamps, the random rolls, the talker reactions, the condition cost and the
   container swap all happen natively. The resulting change rides the existing report → host
   arbitration → broadcast path, so the feeder's own item (whose ownership never moved) receives the
   result. No new wire shape, no ownership transfer, no inventory-space precondition, and a meal that
   is interrupted or cut by a disconnect simply reports nothing and costs nothing.

**Side-effect investigation comes FIRST (next cycle, read-only).** Standing objects mean objects the
local machine never had before, and they enter the game's own item list (`Item.allItems`). Before any
code: enumerate every path that walks that list (physics and collision, rendering, sound, save/restore,
the game's own sweeps, and CUO's reconciliation/kill logic), and for each record what it does with an
object that is present but neither held nor in the world; confirm the reconciliation cannot mistake one
for a stale row and kill it; measure the scale (how many objects a session adds, what each batch has to
update, any per-frame cost); and hunt for counter-evidence — any path that would let a player click,
pick up, count or report one of these objects as their own. The result of that investigation, not an
assumption, decides whether the rule holds as stated or needs a scope limit.

**Rejected on the way here (recorded so they are not re-proposed).**

- *Give the item away first (a real transfer).* Rejected by the user: a bite of your own sandwich would
  become a handover, and a full inventory would block being fed.
- *Hand the delegate a scratch item.* Rejected by the user: it has no slot on the eating body, so the
  container-swap delegates (`stonefruit`, `bucketofchicken`, `popcorn`, `rosepod` — `Item.cs:2606-2618`,
  `:2790-2800`, `:2834-2844`, `:1437-1441`) would read `SlotOf` = -1 and silently skip their swap.
- *Materialize only at the moment of the meal.* Rejected by the user as the wrong shape: the object
  should exist whenever the data does, not be conjured for one action.

## The side-effect investigation (read-only, 2026-10-08) — findings

The design's first requirement was to know what already happens to an item object that exists but is neither held nor in the world. What follows is that answer, path by path, with its evidence; it is what decides the rule's scope in §6.

### 1. The load-bearing finding: a standing object is a third category that today's classifiers do not know

CUO classifies every local item by its **transform chain**. `ItemWorldSync.IsWorldItem` walks up looking for `InventorySlot`, `Body` or `Limb`, and `ItemWorldSync.IsStandaloneWorldItem` adds "and no `Container` above it" — that is the whole test. Every existing family passes it because of where the game (or CUO) parents the object:

| Family | Parent | `IsWorldItem` |
|---|---|---|
| inventory item | the body's slot transform — `Body.PickUpItem`: `item.transform.SetParent(this.slots[slot].transform)` | false |
| worn item | the limb — `Wearable`: `gameObject.transform.SetParent(limb.transform)` (`Wearable.cs:26`), `Body.cs:1508` | false |
| container content | the container — `Container.LoadItem`: `item.transform.SetParent(base.transform)` | false (via the `Container` above it) |
| display proxy | the **clone body's** slot or limb, plus the `RemoteCloneRender` marker | false |
| world item | none | true |

A standing object cannot take any of the first four parents without becoming something else: under the local body's slot it becomes the local player's own item (it blocks the slot — `Body.HoldingItem(int slot)` is `slots[slot].transform.childCount > 0` — enters `GetAllItemsThorough`, every recipe sweep and the native save), and under a clone body it becomes the display-proxy category this design must not mix with. Under a neutral CUO-owned parent it is `IsWorldItem` = `IsStandaloneWorldItem` = **true**: every path in the tree reads it as a world item, while its data row is a carried row (`ItemMessageFlowService.IsWorldItemRegistered` answers from the world table alone).

So the rule as stated does not hold yet, and the gap is not in the object — it is in the classifier. The scope limit is: **the new category must be a named marker that every classifier consults, anchored on the data ("the authoritative table says this id is a world row"), not on a sixth parent-chain rule.** §3 is the census of the sites that need it, each with what it does today.

Why the data rather than a scene marker (the marker form is what the display proxies use, and it works there): a scene marker can be **absent on an object that is already in play**. That is not hypothetical — batch `20261006-h`'s adopt scan claimed a retired proxy and stamped a domain id onto it one frame before its destroy, precisely because the renderer deactivates a stale proxy before its deferred destroy and "an inactive object answers no default component lookup"; `ItemWorldSync.IsDisplayProxy` carries `includeInactive: true` for that reason, and `AdoptTargetRule` exists to re-ask the question the marker could not answer. The same ordering hazard applies to the new kind (an object whose id is attached in the frame it is created vs one frame later, §3's `OnItemInstantiated` row). A table lookup has no such state: it answers for an id no matter what the scene currently holds, and it is the same fact the reconcile, the stream and the kernel already consult — so the category cannot drift from the data it mirrors. The marker component can still ride along as the cheap per-object short-circuit; the **decision** is the table's.

**The game makes the same test, for the same reason, and reads the same answer.** "Has a parent that is not a `Container`" *is* the game's word for "carried by the local player": an inventory item's parent is an `InventorySlot`, which is not a `Container` (`InventorySlot : MonoBehaviour`), so `Item.ParentContainer()` is null for it exactly as it is for a CUO-anchored object. `Item.HandleDecay`'s `NoDecayWhenNotWorn` bit, `WaterContainerItem.Update`'s guard and `ItemLock.Update` ("a parent means picked up, destroy me") all read that same state. A CUO-anchored standing object therefore lands on the *carried-by-the-local-player* side of the game's own logic — which is why the decay, water and light rows in §2 bite.

### 2. What the game itself does with such an object

| Path | What it does to a standing object | Verdict |
|---|---|---|
| `Item.Update` (`Item.cs:145-180`) | Per frame. With **no** parent it drives `rb.simulated` (also gated on `Time.timeScale <= 5f`) and `affect.enabled` from `WorldGeneration.world.worldExists && GetClosestChunkRenderer(pos).enabled`; **with** a parent it touches neither. Then `if (!flag) return;` gates the destroy and the decay. `GetClosestChunkRenderer` **clamps** the position into the chunk grid (`WorldGeneration.cs:1177-1183`), so parking outside the map does not exempt an object — it inherits the edge chunk's state. | needs care |
| `Item.HandleDecay` (`Item.cs:183-208`, called from `Update`) | `condition -= rotSpeed * decayMultiplier * (isWet ? 6 : 1) * globalDecayRate * 0.01 * deltaTime` for any item whose chunk renderer is enabled. The `decayInfo` bits are the game's own `ItemInfo.DecayType` names (`ItemInfo.cs:195-206`): `NoDecayWithoutContainerItem = 1`, `NoDecayWhenNotWorn = 2`, `NoDecayWhenStill = 4`, `BatteryDecay = 16`, and only the first three zero the rate. A parent defeats the `!transform.parent` clause of bits 2 and 4 — but **not** the rest of them: bit 1 still zeroes the rate for a container-shaped item with an empty `Container` (`this.cont.itemCount == 0`), and bit 4 still zeroes it whenever the **local** player is standing still (`PlayerCamera.main.body.rb.velocity.magnitude < 0.5f`). So a standing object decays, per side, at a side-dependent rate — the drift the data must re-align, not a guarantee. The same shape already exists for world items (the alignment comment in `ItemReconcile` names it); for the new category the aligning data is the carried snapshot. `decayMultiplier` is `public` and the game itself uses it as a per-item decay switch (`GeigerCounterAudio`: `this.item.decayMultiplier = (this.active ? 1f : 0f)`), so freezing decay on these objects has a native precedent. | needs care |
| destroy-at-zero (`Item.cs:157-178`) | `condition <= 0f && Stats.destroyAtZeroCondition` → the break particle + `Object.Destroy` (or `Container.ContainerBroke()` when the object is a container). A standing object that reaches zero disappears locally while the data still says it exists. A **container-shaped** one spills its real children into the world — `Container.UnloadAllItems` does `SetParent(null)`, `item.rb.simulated = true`, `position += Vector3.up * 1.5f`, sprite on — i.e. duplication into the local world. `trashbag` (`Item.cs:1937-1946`), `duffelbag` (`:5908-5914`) and `bigpack` (`:5951-5957`) all set `destroyAtZeroCondition = true`. | blocker for a container-shaped object |
| `Item.Awake` / `Start` (`Item.cs:85-119`) | `rb.mass = Stats.weight`, `AddComponent<LiquidAffect>()`, `Item.allItems.Add(this)`. Registration is in `Start` — one frame after `Instantiate` — so the instance id must be attached in the same frame (see §3, `OnItemInstantiated`). | needs care |
| `LiquidAffect` (`LiquidAffect.cs:8-34`) | `Start` destroys the component when the body is not `Dynamic` (`if (!this.rb || this.rb.bodyType != null)` with the decompiler's `null` = `0` = `Dynamic`), and `Item.Update`'s `this.affect.enabled = …` — which sits inside its `if (!base.transform.parent)` branch, so it is the **unparented** object that NREs per frame on the dead reference — CUO already patches both (`LiquidAffectPatches`, keyed on `bodyType == Kinematic`). A `simulated = false` **Dynamic** body passes the guard and early-outs in `FixedUpdate` (`if (!this.rb.simulated && !this.limb) return;`). | safe under the existing patch |
| `OnWillRenderObject` (`Item.cs:229-235`), `OnCollisionEnter2D` (`:238-247`) | Wetness accrues only while rendered; a collision above `relativeVelocity.magnitude > 3f` plays `drop` + a step sound and spawns `DustMini`. `SawbladeScript.OnCollisionStay2D` (`:20-26`) and `Heater.OnCollisionEnter2D` (`:41-49`, a cooker destroying a `meat`-tagged item) are the same family. All need a live collider/body; CUO's proxy recipe removes both (`CloneInventoryRenderer`: `item.rb.simulated = false; // pure display`, `col.enabled = false; // never pickable/blocking`). | needs care without that recipe |
| `WaterContainerItem.Update` (`WaterContainerItem.cs:289-305`) | For a `WaterContainerItem` whose parent is **not** a `Container` (and with space left and `AutoFill`), every frame: read the local fluid at the object's block, `AddLiquid(...)` and `FluidManager.main.SetLiquid(x, y, 0)` — it drinks the local world fluid and deletes it. Two more conditions ride the read: `this.item.condition < 1f`, so a pristine bottle does not drink (decay or `Item.SetCondition` gets it there), and there is no chunk test and no live-world test at all. `LiquidItemInfo.autoFill` defaults to `true` (`LiquidItemInfo.cs:11`); of the 54 liquid definitions **26** set it `false` explicitly (the medical and utility family: `saline`, `bloodbag`, `morphine`, `syringe`, `materialpouch`, …) and **28** keep the default, the everyday bottles among them (`waterbottle`, `canteen`, `waterjug`, `sodabottle`, `milk`, `applejuice`). The guard the game uses elsewhere for "not in the world" is position-first and does **not** transfer: `LiquidAffect.FixedUpdate` returns when the position is outside `world.halfWidth`/`halfHeight` (`LiquidAffect.cs:31`), but `FluidManager.GetLiquid`/`SetLiquid` **clamp** their coordinates into the map (`FluidManager.cs:338-353`), so an out-of-map parked object would drink the *edge* block's fluid rather than nothing. | blocker — a real local world edit with neither collision nor simulation needed |
| `BatteryItem.Update` (`BatteryItem.cs:75-82`) | `if (!this.hasBattery) { this.myItem.condition = 0f; this.maxCharge = 0f; }` — no parent, chunk or live-world test. A standing object of a battery item without a battery is pinned to condition 0 locally, which then feeds `Item.Update`'s destroy-at-zero. | needs care |
| `CustomItemBehaviour.Update` (`CustomItemBehaviour.cs:20+`) | A per-item string-hash switch whose cases branch on the parent: the "in an `InventorySlot`" tests are the first branch, and the `else` is the ordinary fall-through — which a CUO parent takes. Concretely: `gravbag` (`:69-77`) at `condition <= 0.005f && battery.hasBattery` runs `WorldGeneration.CreateExplosion(...)` and destroys itself; `exposedcore` (`:251-257`) at `condition <= 0f` destroys itself and **spawns two crystal enemies**; `craftingbottle` (`:237-240`) destroys itself when empty; `torch`/`campfire` drain condition while wet. | needs care — per-item behaviours that mutate the world or spawn entities |
| `LightItem.Update` (`LightItem.cs:19-22`) | `this.light.enabled = this.shouldEnable && !this.inContainer;` — `shouldEnable` is set true in `Start`, `inContainer` is written only by `Container.UpdateItemLight`, and nothing here tests the chunk or `simulated`. A standing object that carries a `LightItem` (torch, lantern, flashlight…) is a **live `Light2D` at its parked position** every frame. | needs care — a visible light wherever the object is parked |
| `WatchScript.Update` (`WatchScript.cs:26-70`) | No parent or chunk test: with `batt.hasCharge` and within 30 units of the **local** body it makes the item's own `Talker` speak the *local* player's readouts — the bleed-out estimate from `:44-52` and the infection list from `:53-70`. A second presentation path of the same kind writes sprites per frame (`EPdaScript.Update`). | needs care — another player's watch talks about you |
| `ItemLock` (`ItemLock.cs`) | The game's own precedent for parking an item object inert in the world (the wall-rack AED): `Start` zeroes the velocities, sets `freezeRotation = true` and `constraints = 7`, and `Update` destroys the component the moment the object gets a parent. It freezes the body, it does not take it out of the simulation. | the native shape to copy for the physics side |
| `FreshItemDrop` (`FreshItemDrop.cs`) | `Awake` sets `gravityScale = 0f`, `Start` builds an outline sprite + particle, `FixedUpdate` writes velocity and gravity per frame. The game adds it on its own creation flows (`BuildingEntity.cs:84/107/118`, `CrystalMetamorphic.cs:30`, `TraderScript.cs:735`); CUO attaches it only on the materialize path (`if (w.FreshItemDrop)`). | safe by omission — do not add it |
| Query surface | `PlayerCamera`'s hover probe `Physics2D.OverlapPoint` over the mask `{ "Item", "Ground", "Descriptor", "Limb" }` (`:1997-2017`, `Item` first, with a `{ "Body" }` fallback) and its drag start (`:1423-1437`, `{ "Item" }` before `Body`/`Limb`), `AltHoverScript` labels (`:64-74`, a ±10-unit box), `Recipe.GetItemsForRecipe[Thorough]` (`Recipe.cs:111-120`, `:142-151`: `GetAllItemsThorough()` **plus every layer-`Item` collider within 10 units** passing `DoPickupCheck(item, true)` become recipe ingredients, consumed by `RecipeItem.UseItem` — `item.condition -= …`, `container.UnloadAllItems()`, `Object.Destroy(item.gameObject)`), `WorldGeneration.CreateExplosion` (`:3982-4004`: an **unmasked** `OverlapCircleAll` that runs `item.SetCondition(...)` and writes velocity). | blocker with a live collider: another player's item can be used as an ingredient and destroyed |
| `WorldGeneration.Clear` (`:1077-1104`) | Destroys every item with `transform.parent == null || parent.name == "DOSPAWN"` at each layer regeneration. A parented standing object **survives**, so its lifetime is CUO's to own; an unparented one dies at every layer switch. | needs care |
| `CraftingCourse.EraseScene` (`:444-455`) | `FindObjectsOfType<Item>()` then destroys everything inside a 42×25 rect centred at `Vector2.up * 9.5f` (which contains the origin) — tutorial scene only, but it reaches CUO objects too. | needs care, narrow |
| `SaveSystem.SaveGame` (`:35-187`) | An explicit walk of `SaveSystem.body`'s slots, wearables and their container children; `Item.allItems` is never used, `WorldSaveData` carries chunk dimensions only, CUO blocks the native load (`SaveSystemTryLoadGamePatch`) and its own save authority never writes from a guest (`RunSaveCoordinator`). The game's own `SaveGame` is **not** patched — the console `saveandquit` can still write the native file, a guest included — but it only ever walks the local body, so a standing object is outside it either way. | safe (and one more reason never to parent one into the local body) |

### 3. What CUO's own item domains do with one — the classification census

This table is the **census**, not a first pass: every call site in the adapters of `ItemWorldSync.IsWorldItem`, `ItemWorldSync.IsStandaloneWorldItem`, `ItemWorldSync.IsDisplayProxy`, the `RemoteCloneRender` / `RemoteInventoryItemId` marker tests and `ItemInstanceId.TryFindItem`, plus the Runtime and kernel sites that answer "is this id a world row" (`ItemMessageFlowService.IsWorldItemRegistered`, `ItemProjection`, `CraftSyncService`, `KernelCommandGateway`). Reproduce with a grep for those six symbols under `src/`. Paths: the item domains are `src/CasualtiesUnknownOnline.GameAdapter/Items/`, their patches `.../GameAdapter/Patches/`, the carried/character selectors `.../GameAdapter/Character/`, the world-generation pair `.../GameAdapter/WorldGen/`, the Runtime side `src/CasualtiesUnknownOnline.Runtime/Session/Items/`, the kernel `src/CasualtiesUnknownOnline.Application/Kernel/`.

**The site on the design's own path is the use report.** `src/…/GameAdapter/Items/ItemUseSync.cs` classifies the use by `ItemWorldSync.IsWorldItem(item)`: on a session host (the host can be the affected side just as a guest can) a world item takes `_items.SendWorldItemCorrection(...)` and **returns** — the carried-fact broadcast (`SendItemCarriedSync`) is skipped — while the non-world branch broadcasts the carried fact. The world branch writes only a row the projection already has (`src/…/Runtime/Session/Items/ItemProjection.cs`: `if (!_worldTable.TryGetValue(itemId, out var w)) { return; }`), and a carried id is not a world row. `src/…/GameAdapter/Patches/UseItemPatches.cs` hooks `Body.UseItem` and suppresses only display proxies (`RemoteDragProxyQuery.IsProxy` = the `RemoteCloneRender` marker), so a standing object passes. The design's promise in §"The design" 4 — the eater's use "rides the existing report → host arbitration → broadcast path" back to the feeder's item — therefore does not hold for the host-as-eater branch as the code stands; the food-chain step must answer it (either the classification makes the carried branch fire, or the use report gets its own carried path).

| Site | What it decides | What a standing object gets | Verdict |
|---|---|---|---|
| `Items/ItemUseSync.cs` (`OnItemUsed`) + `Patches/UseItemPatches.cs` | Which report a use takes: on a session host an `IsWorldItem` object sends `SendWorldItemCorrection` and returns; anything else sends the carried-fact broadcast. The hook suppresses only display proxies. | The item-level half of a host-side meal takes the world path (see above). | must be answered by the food-chain step |
| `Items/ItemReconcile.cs`, the kill loop | `if (idComp == null \|\| !ItemWorldSync.IsStandaloneWorldItem(item)) continue;` — a standing object does **not** `continue`, its id is not in the world snapshot (built from the snapshot rows), and the guard does not protect it → `KillRemoteItem(item)`. Once per keyframe (5 s base, governor-stretchable to 10 s) and on every world entry. | **This is the direct answer to "can the full-table reconcile mistake one for a stale row": yes. The `continue` that protects inventory items *is* the parent-chain test.** |
| `ItemReconcile` late id-less sweep, and the state-alignment loop | The sweep skips anything with an `ItemInstanceId`; the alignment loop skips ids the snapshot does not carry. | safe |
| `GeneratedItemReconcile.Apply`, `GeneratedItemAuthority` | Both act on id-less standalone world items only. | safe |
| `ItemPositionAuthority.SendMovingItemMoves` (host, 10 Hz) and `RefreshWorldItemStates` (5 s) | `IsStandaloneWorldItem` includes a standing object, so the host streams its parked position/velocity/rotation as a world fact. The 5 s refresh is harmless: `ItemProjection.ApplyRefresh` returns early for an id that is not a world row, so the authoritative carried data cannot be overwritten. | needs care (pure waste on the wire plus a wrong fact for the guests) |
| `ItemPositionFollow` (guest, per frame) | `FindWorldItem(id)` finds the id-indexed object, `IsStandaloneWorldItem` passes, and `StartLocalPhysics` sets `bodyType = Dynamic`, moves it to the streamed point and writes velocity — the object stops being "not in the world", and the per-frame walk grows with the new objects. | needs care |
| `RemoteItemSceneOps.FindWorldItem` / `ItemInstanceId`'s index | An id-stamped standing object becomes that id's domain object — which is exactly what the apply path wants — but every caller that then assumes "world item" must be re-read (this row, the two above, and the routing rows below). | the seam |
| `Items/ItemApplication.cs` (`OnRemoteItemPickedUp`, `OnRemoteItemDestroyed`, `ApplyTrapDropPresentation`) | All three gate on `IsWorldItem` — the parent chain, not "is this a world row" — so a standing object is a valid target for a remote kill or a trap-drop enrichment. | needs care (the same predicate defect) |
| `Items/ItemDropState.cs` | Whether an owed departure report is still alive: `alive && ItemWorldSync.IsStandaloneWorldItem(place.Item!)`. Unreachable for a standing object (nothing drops one) but part of the census. | safe today, and another site the marker must reach |
| `Patches/BodyPatches.cs` (`WearWearable` prefix), `Items/PickupSync.cs`, `Items/ItemWorldSync.cs` (`OnItemDropped`/`OnItemThrown` landed status) | The "left the world" verdict of a wear, a pickup and a drop/throw — `IsWorldItem(item) ? Indeterminate : Committed` and `IsStandaloneWorldItem(item) ? Committed : Rejected`. Each is reachable only through a local gesture performed **on the object**. | needs care — a false "landed" verdict is one pickup away (§4) |
| `Items/ContainerItemSync.cs` + `Patches/ContainerItemPatches.cs` (+ `Patches/RemoteCloneContainerGuard.cs`) | The container load/unload classifier: `ContainerLoadClassifier.Classify(ItemWorldSync.IsWorldItem(item), departure, wasWorldItem)`, captured in prefixes either side of the load. A standing object in a container context would classify as "left the world" on an internal move. | needs care, blocked by §6 constraint 5 |
| `Patches/NonAuthoritativeItemImpactGuard.cs` (+ `Items/NonAuthoritativeItemImpactPolicy.cs`) | The collision-presentation authority decision for the item-prefab collision hooks: `ShouldSuppress/ShouldReport(…, ItemWorldSync.IsStandaloneWorldItem(item))`. | needs care (the collider is off, so the hooks cannot fire) |
| The drag-window family: `Patches/RemoteDragProxyQuery.cs`, `Patches/RemoteDragMutationPatches.cs`, `Patches/PlayerCameraDragUsePatch.cs`, `Patches/PlayerCameraHandleWhileDraggingPatch.cs` | The proxy-only intent window over the native mutations (`Container.LoadItem`/`UnloadItem`, `Body.PickUpItem`/`DropItem`/`WearWearable`, `TraderScript.GiveItem`, …): each prefix asks `window.IsOpen && IsProxy(item)`, and `IsProxy` is `GetComponent<RemoteCloneRender>() != null`. | an unmarked standing object inherits none of this guard — §4's reachability conclusion is the substitute |
| The carried/body-scoped marker selectors: `Character/CarriedInventoryReporter.cs`, `Character/CarriedItemLocator.cs`, `Character/CharacterDataCapture.cs`, `Patches/BodyItemPatches.cs`, `Items/ItemSlotSync.cs`, `PlayerInteractionApply.cs`, `RemoteMedicalOperationHandler.cs` | Each walks the **local body's** subtree (slots, limbs, carried containers) and skips `RemoteCloneRender`-marked objects, so a proxy is never reported as the local player's own item. | safe by scope for a CUO holder; the marker half is the category the new one must not be |
| `Runtime/…/ItemProjection.cs` (`ApplyRefresh`, `ApplyUpdateState`) + `Runtime/…/ItemActionSync.cs` | Every world-projection write is behind `_worldTable.TryGetValue(itemId, …)`, so a carried id can never leak into the world table from a standing object's local state. | safe — the cross-check that the data side is already correct |
| `ItemApplication.OnItemCorrection` → `ApplyAuthoritativeState` | Applies condition, favourited, liquids, component states and contents recursively, addressed by instance id. **This is the "explicit apply path" the design demands — already written, already id-addressed.** | reuse |
| `ItemWorldSync.OnItemInstantiated` (the `Item.Start` hook) | `IsReplayedFact \|\| IsGenerating() \|\| !IsStandaloneWorldItem(item)` then `if (idComp != null) → Skipped/AlreadySynced`. Created with the id attached in the same frame: silent. Created one frame earlier: a fresh id is allocated and a spawn is reported. | constraint: attach the id before `Start` |
| `ItemWorldSync.OnItemDestroyed` | Reports `SendItemDestroyed(idComp.Id)` for any id-bearing object — no `IsWorldItem` test. Its four gates are `IsReplayedFact`, `HarmonyTraverse.IsGenerating()`, `_suppressDestroys` (the world-teardown edge the next row is about) and the marker-based `IsDisplayProxy`, which a standing object does not have; the report itself needs `idComp.Id != 0`. The host's backstop is the kernel's own rule: `KernelCommandGateway.MayReportDestroyed` answers `Ignore` (audited, unanswered) for an item that `IsCarried` by another member — "item {id} is neither a world item nor carried by the sender". | an ordinary destroy would be reported; safe by arbitration, loud by design |
| `BodyItemPatches` pickup/drop hooks, `PickupSync.OnPickedUp` | Suppression is `item.GetComponentInParent<RemoteCloneRender>() != null`; a standing object has no marker, so a pickup or drop of one **would** be reported (and `IsWorldItem(item) ? Indeterminate : Committed` would make the pickup verdict "landed"). Reachability is the only thing between the design and a false report. | needs care — see §4 |
| `HeaterCookSync`, `CraftSyncService` | The cook capture is one event-scoped `Item.allItems.Contains(steak)` plus an exact id/condition/position fingerprint; the craft family filters on `IsWorldItemRegistered`, false for a carried row. | safe |
| `CloneFactTable` / `CharacterDataSync` / `CloneInventoryRenderer` | The carried data edge is `CloneSnapshotUpdated`, and today its only scene consumer is the display-proxy category (`IsDisplayProxy` = `RemoteInventoryItemId` or a `RemoteCloneRender` ancestor). The new category needs its own consumer of that same edge. | the other seam |
| Session teardown | `GameAdapterSessionBinding.OnSessionEnded` ends with `domains.Renderer.DestroyAllClones()` — the proxy category's lifetime is explicit. The standing category needs its own rule, because an object that outlives the world dereferences `WorldGeneration.world` in `Item.Update` every frame (`world-null`): the batch `20261002-h` storm was 251 objects and 42 MB/min. | needs care |
| Container contents recursion | The proxy path materializes remote container contents as real children of a real `Container` — `Character/CloneInventoryRenderer.cs` (`RestoreRemoteContents` / `RestoreRemoteContent`), which calls `container.LoadItem(child)` for the ordinary case and **attaches by hand** for a nested container ("Native `Container.LoadItem` refuses a container that already holds items (stacking guard), but remote display proxies must be able to represent a nested container with contents"), then recurses. Doing the same for the new category inherits the zero-condition spill (`Container.ContainerBroke` → `UnloadAllItems`) and `Container.UnloadItem` (`SetParent(null)`, `item.rb.simulated = true`, `position += Vector3.up * 1.5f`), which converts a standing object into a live world item. | constraint |

### 4. Counter-evidence: nothing in the game is ownership-gated — reachability is the whole defence

- **Pickup**: `Body.PickUpItem` (`Body.cs:1390`) checks only a pickable slot, "not already held", an empty target slot, `DoPickupCheck` and the hand-only rule; `DoPickupCheck` (`:1356-1385`) is "within 10 units and no `Ground` in the line". No ownership test exists anywhere — and an **unparented** object with no `Collider2D` component throws NRE at `item.GetComponent<Collider2D>().bounds.center` (`:1362`), so a standing object must be parented (a *disabled* collider still satisfies that call).
- **The click**: `PlayerCamera`'s drag start takes any `Item` collider under the cursor before it even considers `Body`/`Limb` (`:1423-1437`), so an object on the local player steals the click.
- **No collider needed**: `RepopulateContainer` (`PlayerCamera.cs:2824-2843`) gives every child of an opened container an `InvButton` with `refItem` set, and `InvButton.GetItem` returns `refItem` — so an object that is a child of a container whose window a player opens is draggable with no collider at all. The existing proxies are guarded against exactly this by `RemoteDragMutationPatches` (an open release window captures the intent and skips the native mutation) — an unmarked object inherits none of it.
- **Inventory capacity**: every accounting method is body/container-scoped (`HoldingItem(int slot)` = `childCount > 0`; `GetAllItemsThorough`; `Container.GetHoldingWeight` / `CanHoldItem`; `GetTotalEncumberance`). A standing object consumes space only for the body or container it is a child of, so under a CUO holder it consumes none; parented under the local body it would block a slot and enter every one of them at once.
- **Reporting**: the guards are the `RemoteCloneRender` marker (§3), and the kernel then refuses a non-owner's destroy. There is no "a client may not write another player's item" rule in the local object model at all — the game's item model has no ownership concept (`PlayerCamera.ItemHoverDescription` reads name, stats, battery and liquid only).
- **Conclusion**: the object must be **born unreachable** — `rb.simulated = false` and its collider disabled (the proxy recipe, minus the marker), no renderer showing it, and never a child of a container whose window can list it unless that window is guarded. Then every path in §2 and §3 that needs a collider, a held slot or a UI row simply cannot see it. Without that, the sharpest path is the recipe sweep: another player's item becomes a legal ingredient and `RecipeItem.UseItem` destroys it.

### 5. Scale

- The host's world table held 259 → 261 → 263 rows in one recorded session (the `w0/w1/w2-host-tables` reads in `docs/evidence/acceptance/second-drop-report-loses-its-world-object-20261007-b.md`), and the keyframe sent 266 and 286 rows in two others (`sandbox-client-null-reference-bursts-20261002-i.md`, `-j.md`; the j-run's own census read `total=289 withId=287 noId=2`). Those are world items — objects that already exist and already pay `Item.Update`.
- The new objects are the **carried** rows: per remote player one per occupied slot, one per worn item and one per container content, recursively — the top-level half is `CharacterDataMsg.Items.Count` (slots and wearables only; `CharacterDataCapture` puts contents nested inside each entry), which the clone renderer logs as `[CloneRender] apply {Count} items to clone slots ({Slots} slots)` at Debug, so that line is a **lower bound** and the recursive set is what the acceptance rows must count. The per-session number is a runtime reading (see §7).
- Per frame each new object adds one `Item.Update` plus `LiquidAffect.FixedUpdate`'s early-out plus whatever the definition's own components do — the same per-object cost the game already pays for 260-290 world items. **No new wire traffic**: the data already travels (the 1 Hz character snapshot and the carried events carry condition and recursive contents), so the batch cost is unchanged; the 10 Hz / 5 s streams would only touch the new objects if the classification stays as it is (§3).

### 6. What this means for the plan (constraints, not a redesign)

1. **Category first**: a named marker for the new kind, anchored on the data, and **every site of the §3 census** consulted in the same change. The data fact is one query away — `_worldTable.ContainsKey` behind `ItemMessageFlowService.IsWorldItemRegistered`, wrapped by the Runtime-internal `IItemActionWorldAccess.IsWorldItem` (consumed by `ItemActionSync` alone) and **not on `IItemControl` today**, so exposing that one query to the adapter is part of this step. Until the category exists the reconcile kills the object on the first keyframe and the stream drags it into the world on the guest.
2. **Birth recipe**: prefab → state → instance id in the same frame (before `Start`) → parented to the CUO holder → `rb.simulated = false` + collider disabled (the proxy recipe; `Container.LoadItem` is the game's own equivalent for the container case) → renderer off, and the parked spot chosen deliberately: an out-of-map position is what the game's own liquid physics calls "not in the world", but the fluid table clamps, so it is not automatically a fluid-free spot (the `WaterContainerItem` row in §2).
3. **Inert is more than "not simulated"**: the object is still mutated every frame. `Item.Update`'s destroy-at-zero and `HandleDecay` are gated only on the chunk under the object; `WaterContainerItem.Update`, `BatteryItem.Update` and `CustomItemBehaviour.Update` have **no** world test at all (§2). The recipe therefore needs a decision on the definition's behaviour components (disable the ones the data does not need, the way the apply path does not need a live `Update`) — otherwise a standing food item rots, a standing bottle drinks the local puddle and a standing `exposedcore` spawns crystal enemies where it is parked.
4. **Never under the local body**, and never as a child of a container whose window a player can open unless that window is guarded.
5. **Containers**: the recursive contents must not become real `Container` children unless the zero-condition spill and `Container.UnloadItem` are handled.
6. **Lifetime**: parented (or gone at every layer regeneration), not under a `DOSPAWN`, and destroyed or carried across session end by an explicit rule.
7. **Live world only**: materialize under the existing `HarmonyTraverse.HasWorld` gate, the same one `SpawnWorldItem` uses — otherwise the menu-scene `world-null` NRE family returns.
8. **Decay**: a standing object's condition drifts locally by design; if the drift matters, freeze it with the game's own `decayMultiplier` switch rather than adding a CUO guard.

### 7. Readings the acceptance batch owes (real machine, not L0)

- the per-session standing-object count and the per-frame cost against the pre-change baseline (§5);
- the presentation judgement: nothing visible, no dust or collision sound, no hover tooltip, no alt-hover label, **no light** (`LightItem`), no watch chatter (`WatchScript`);
- the local-effect judgement: the fluid table at the parked spot and at the map edge is unchanged (`WaterContainerItem`'s clamped read — measured with a **damaged** bottle, since a pristine one does not drink), and the parked objects' condition tracks the data rather than a locally pinned 0 (`BatteryItem`);
- the prefab facts that are asset data rather than C#: which layer an item collider sits on, trigger vs solid (`reversing/` holds no prefab data);
- whether a standing object's local decay actually reaches the zero-condition destroy between two data refreshes;
- Unity's behaviour for a `simulated = false` body whose velocity is written (the explosion path writes `rigidbody2D2.velocity`).

## The earlier candidate designs (kept for the record)

Both keep the invariants the migrated chains established: the host owns admission, the resource and the
arbitration; the affected side judges on its own picture; the numbers come from the game, never from a
CUO table.

**A — the affected side runs the item's own delegate.** *Its scratch-object form and its
transfer-the-item-first form are both rejected above.* What survives is the split the constraint
forces: the body half on the eater's client, the item half where the item is real — and with the
settled "objects follow data" rule the item half runs on the eater's own local object of the feeder's
item, whose change then rides the ordinary report path back to the real one.

**B — the operator measures the delegate's body deltas and the patient applies them.** The operator runs
the item's own `useAction` against the affected player's render clone inside a capture window, and the
body fields it moved become the effect the host carries; the patient applies that delta through the
existing health apply. Costs: the patient's half is CUO arithmetic rather than the game's own code
(the family's whole point), the clone is momentarily fed (a talker bubble, an eat animation, the
calories counter), the clamping/random branches are evaluated on the clone's state rather than on the
patient's own, and the item-level half (the condition cost, the container transforms) cannot come from
the clone at all, because the clone holds no item. Not needed under the settled design; kept as the
record of the alternative.

## Scope

Two things now, and the first is the platform change the settled design needs:

- **Objects follow data** (the section above): the item materialize / update / destroy path that today exists
  only for world items, extended to every item the synchronized data carries, with the side-effect
  investigation done before the code.
- **The food chain itself**: with the object present, the eat is the game's own `Body.UseItem` →
  `useAction`, and the routing predicate asks the game's own data (a `useAction`-bearing item whose
  class is not one of the other families) exactly the way the drink chain asks `usable`.

## Hard acceptance

Delete `RemoteConsumeCatalog` (both the table and the type) and every existing case of the chain stays
green, with each deleted case's disposition listed in the self-check — the standard the injection,
topical and drink chains were held to. The cases are
`RemoteConsumeApplicationTests.ApplyFood_AppliesBreadEffect`, `..._Catalog_ExposesTheCuratedFoodItems`,
`ItemUseTests.Host_UsesBreadOnGuest_AppliesFoodAndSendsResult`, and the `IsActuallyUsable` half of
`CarriedItemUseTree`. The platform half carries its own acceptance: the side-effect investigation's
findings are answered one by one, and a session shows what the change is supposed to show — the same
**world** item count with no duplicate objects, the standing set matching the synchronized carried rows
**recursively** (every nested `Contents` entry, not the top-level `Items.Count`), and no per-frame cost
beyond one `Item.Update` per standing object against the pre-change baseline (the 2026-10-08
investigation's §5 fixes what that baseline is and §7 lists the readings the acceptance batch owes).

## Non-goals

- Not a mod-API surface change: a mod still cannot author an item's `useAction` (Part 3 A of the ceiling
  ticket), so this ticket's content win is the vanilla items the table never carried.
- Not a re-litigation of the drink chain's shape.
