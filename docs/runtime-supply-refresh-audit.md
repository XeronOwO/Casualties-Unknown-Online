# Runtime supply refresh audit — mechanism inventory and self-check

> **Historical audit (2026-08-16).** Point-in-time evidence for an item-spawn
> question. Current item authority/wire behavior is documented in
> [`architecture-evolution/domains.md`](architecture-evolution/domains.md) and
> [`architecture-evolution/protocol.md`](architecture-evolution/protocol.md).

Owner cycle: backlog item-domain open question (2026-08-16)

> "Runtime random supply refresh (phase-1 memory, unresolved question): it is
> not verified whether any supplies spawn independently of world generation.
> If such a mechanism exists it needs host-authoritative spawn + broadcast
> (the item-spawn channel pattern; see `docs/item-features.md` Runtime item
> spawn surface — this is not tech-decisions #110, which is cross-player
> drinkable medicine); investigate before assuming."

## Conclusion

**No world-level random/timed supply refresh exists in the shipped game.** Every
item prefab creation is either

1. **generation-time** — created inside `WorldGeneration`'s generation pass (or
   a `Start` that runs while generation is finishing) and already covered by
   the host's generation-item snapshot, or
2. **one-shot runtime** — created as the direct result of a player/entity
   action (destruction drops, use results, unload, cook, trade, tutorial) and
   already covered by either the generic runtime item-spawn channel or its
   dedicated operation domain, or
3. **the one repeating runtime item creation** — `Body`'s unconscious
   "droppings" loop (a fixed 1000 s cadence, not random and not a supply),
   which also flows through the generic runtime item-spawn channel.

The conditional work named in the backlog entry ("if such a mechanism exists it
needs host-authoritative spawn + broadcast") needs **no new implementation**:
the host-authoritative spawn path already exists and is the single sink for
every standalone runtime item. This is a docs-only resolution.

## Method (repeatable evidence)

1. Enumerated every `Object.Instantiate` / `Utils.Create` call site in the
   decompiled `Assembly-CSharp` (194 sites) and separated item-prefab
   creations from particles/UI/effects.
2. Classified each item-creation site by trigger: generation, one-shot
   runtime, repeating runtime, tutorial, save-load, or debug console.
3. Checked for repeating/timed item producers specifically:
   - `InvokeRepeating` anywhere in the decompiled sources: **none**.
   - `for (;;)` / `while (true)` loops: 17 loops. Exactly one contains an item
     creation — `Body.cs:1790-1798`
     (`TheCoroutineThatMakesYouShitYourselfWhenUnconscious`, fixed cadence).
     The rest are tutorial waits (`BasicCourse`, `FirstAidCourse`), the workout
     loop (`Body.cs:397`), `LastHappinessUpdater` (`Body.cs:1084`),
     `LifepodPump`'s fluid pump, `PlayerCamera.cs:537`, FastNoiseLite internals
     and `WorldGeneration` drawing loops — none creates items.
   - Every `ItemLootPool` consumer: `BuildingEntity.cs:91`,
     `CorpseScript.cs:35`, `TraderScript.cs:687`, `ConsoleScript.cs`
     (dev commands). None is timer-driven.
4. Traced the CUO sink for runtime item creations from the Harmony hook to the
   host arbitration, and confirmed the backlog's requested item-spawn pattern is
   the already-landed generic `ItemSpawn` channel.

## Runtime item creation inventory

Game file references are `reversing/Assembly-CSharp/Assembly-CSharp/<file>.cs`
(the decompiled sources, gitignored raw material).

| # | Game site | Trigger | Repeats? | Destination | CUO coverage (static evidence) |
|---|---|---|---|---|---|
| 1 | `BuildingEntity.cs:79,102,113` (`Update`, `health < 0.5` branch) | Entity destroyed / opened / mined — one roll per entity | No | World | `BuildingEntityUpdatePatch.cs:22-37` lets only the attacker roll the branch (remote deaths are marked and destroyed without rolling); every spawned item hits `ItemPatches.ItemStartPatch` → `ItemWorldSync.OnItemInstantiated` (`ItemWorldSync.cs:177-247`) → `ItemService.SendItemSpawned` (`ItemService.cs:152-180`) → host register + relay (`HandleHostSpawnReport`, `ItemService.PendingPickups.cs:66-104`) |
| 2 | `WorldGeneration.cs:761,771,778,786,793,809,812,815,825,830` (`DamageBlock`, runtime mining/attack) | Block damaged/destroyed — one roll per block | No | World | `UtilsCreateDropPatch.cs:19-38` stamps `DropOrigin` inside the `DamageBlockOrigin` scope; `ItemWorldSync.OnItemInstantiated` folds the drops into the pending break report (`ItemWorldSync.cs:212-226`) — one break = one `BlockDamaged` message with all drops |
| 3 | `CorpseScript.cs:36` (`Start`) | Corpse created during generation; `Start` runs while the generation coroutine is suspended at the darken wait | No | World | `GeneratedItemAuthority.cs:60-136` enumerates the host's id-less standalone items at the generation-finished edge and publishes one `WorldItemsSnapshot`; `GeneratedItemApplication.cs:56-126` binds/materializes on the guest and destroys host-unknown locals. The original corpse-loot divergence behind the item-spawn question was resolved by this mechanism (commit `61b30a2`) |
| 4 | `CrystalMetamorphic.cs:30` (`Touched`) | Crystal touch latch — one roll per crystal | No | World | The latch travels as `CrystalMetamorphicTriggered` (`TrapCrystalPatch.cs:115-128`); the trigger side's item drops ride the generic `ItemSpawn` channel and the entity death/drop domains (`TrapStateActions.cs:379-384`) |
| 5 | `Heater.cs:46` (`OnCollisionEnter2D`) | Heater cooks raw meat — one conversion per collision | No | World | Dedicated `ItemCook` operation: `HeaterCookPatch.cs:19+` + `HeaterCookSync` claim the source destroy and stamp the steak before the generic hooks decompose the conversion (backlog RESOLVED 2026-08-16) |
| 6 | `GunScript.cs:98` (`UnloadMag`), `:170` (`Update` rack edge) | Player unload/rack — one item per edge | No | Inventory / world | `UnloadMag` immediately auto-picks up → `PickUpItemPatch` (`BodyItemPatches.cs:60-77`) → `PickupSync.OnPickedUp` id-less spawn-then-pickup (`PickupSync.cs:61-198`); the rack-ejected casing/round is standalone → generic `ItemSpawn` |
| 7 | `AmmoScript.cs:15` (`UnloadRound`) | Player unload — one item per call | No | Inventory | `PickUpItemPatch` + `PickupSync.OnPickedUp` (same path as #6) |
| 8 | `BatteryItem.cs:118` (`UnloadBattery`) | Player unload — one item per call | No | Inventory | Same pickup path as #6 |
| 9 | `BatteryRecharger.cs:71` (`OnUse`, `firstTime` latch) | First charger use — one mp3player ever | No | Inventory | Same pickup path as #6 |
| 10 | `SplintLimb.cs:21` (`TakeOff`, called from timed expiry or dismember) | Component expiry — once per component | No | Inventory | Same pickup path as #6; the component's `condition` is part of the save-aligned character data snapshot (`ItemStateCodec` component capture) |
| 11 | `TourniquetScript.cs:27` (`TakeOff`, dismember path) | Dismember — once per component | No | Inventory | Same pickup path as #6 |
| 12 | `Item.cs:1442,2198,2619,2801,2845,5682,6698,6699` (use-action lambdas) | Player uses an item — one result set per use | No | Inventory / world | `UseItemPatches.cs:17+` reports the post-use digest; products that are immediately picked up ride `PickupSync`'s id-less spawn-then-pickup, standalone products ride generic `ItemSpawn` |
| 13 | `RecipeResult.cs:52,62` (`SpawnResult`) | Crafting menu — one result set per craft | No | Inventory / world | Crafting is ONE `CraftReportMsg` operation: the coordinator's inventory diff carries products and material dispositions (`CraftingSync`, `CraftingPatches.cs:16+`); product `Item.Start` hooks are silenced inside the craft scope |
| 14 | `TraderScript.cs:734` (`DropInventory`), `:779` (`TryPurchase` success) | Trade interactions / trader death — one event per action | No | World / inventory | Trade domain: guest stock generation is skipped, the host's snapshot supplies the stock (`TraderPatches.cs:18-26`); `DropInventory` and `TryPurchase` ride `TradeExecutor`/`TradeStateSync` reports (`TradeStateSync.cs:102-104`, `TradeExecutor.cs:37-52`) |
| 15 | `SaveSystem.cs:304` (`TryLoadGame`) | Official save load | No | Inventory | Character restore domain (`CharacterDataSync` / `ItemStateCodec`). `LoadedRun` has no backing world field and is recorded as Phase 3 saves scope (`WorldParamsService.cs:107-109`) |
| 16 | `TutorialHandler.cs:260` (`Update`, course-driven) | Tutorial course steps | No | World / inventory | Tutorial claw props are marked `TutorialClawProp` (`TutorialHandlerUpdatePatch.cs:17+`, `UtilsCreateTutorialPatch.cs`) and stay per-player until a real pickup |
| 17 | `ConsoleScript.cs:648,689,1455-1473,1520,1824` | Dev console commands | Manual | Inventory / world | Debug surface, not a gameplay spawner — accepted local-only boundary (a command typed on one machine is that machine's deliberate cheat, never a world refresh) |
| 18 | `Body.cs:1796` (`TheCoroutineThatMakesYouShitYourselfWhenUnconscious`, `Body.cs:1790-1798`) | 5 s loop; creates `droppings` every 1000 s while `!conscious` | **Yes — the only repeating runtime item creation** | World | Started by `Body.Start` (`Body.cs:1782-1783`) on local bodies. The spawned standalone item hits the generic `ItemSpawn` channel exactly like #1/#6. Render clones never fire it: their `Body.Start` also starts the coroutine, but clone `brainHealth` is never driven to 0 (the clone's `Body.Update` is replaced by the render-only path and the session pump writes pose/lying state, not vitals — `BodyPatches.cs:24-62`, `SessionStatePump.cs:17-148`), so `conscious` stays true and the 1000 s condition never opens |

The remaining enumerated `Instantiate`/`Create` sites create particles, UI,
climbing visuals, sound visuals or non-item entities — not supplies. (The raw
counts are 194 spawn sites before classification; the 18 families above are the
item-producing subset, including the generation-time families from
`WorldGeneration.cs` that are referenced for completeness.)

## Host-authoritative contingency (the item-spawn pattern) already exists

The backlog asked for "host-authoritative spawn + broadcast" only if an
independent refresh existed. That capability is not hypothetical — it is the
generic runtime item channel landed with the world-item domain (`541e3be`,
`768eb88`) and refined by the pending-pickup race work:

1. **Report**: `Item.Start` Postfix (`ItemPatches.cs:14-18`) → `PatchBridge`
   → `ItemWorldSync.OnItemInstantiated` (`ItemWorldSync.cs:177-247`) skips
   generation/remote/inventory items and reports every standalone runtime item
   with a fresh id.
2. **Host arbitration**: `ItemService.SendItemSpawned` (`ItemService.cs:152-180`)
   registers on the host before sending, or sends a guest→host report;
   `HandleHostSpawnReport` (`ItemService.PendingPickups.cs:66-104`) makes the
   registration idempotent, settles queued pickup races and relays.
3. **Broadcast**: `ItemSpawnHandler.cs:18-22` → `FireItemSpawnedReceived`
   (`ItemService.cs:305-315`) → guests materialize through
   `ItemApplication.OnRemoteItemSpawned` (`ItemApplication.cs:64+`).

Consequence: any future game update that adds a periodic supply refresh which
creates a standalone `Item` outside world generation is automatically captured
by step 1 and arbitrated by step 2 — no per-mechanism patch is required unless
the new mechanism spawns items directly into an inventory without passing
`Body.PickUpItem` (none of today's mechanisms does; the inventory-direct paths
all call `PickUpItem`/`AutoPickUpItem`, which is the `PickUpItemPatch` surface).

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Item-creation enumeration is complete | None (docs-only) | 194 `Instantiate`/`Utils.Create` sites scanned; 18 item-producing families listed with game file:line; non-item sites excluded by inspection |
| Periodicity classification is complete | None | `InvokeRepeating` search = 0 hits; all 17 infinite loops inspected; only `Body.cs:1790` loop creates an item |
| Random loot pools have no timer caller | None | All `ItemLootPool` consumers listed (`BuildingEntity`, `CorpseScript`, `TraderScript`, `ConsoleScript`); each trigger is generation/one-shot/manual |
| Host-authoritative spawn contingency exists | None | `ItemPatches.cs:14-18` → `ItemWorldSync.cs:177-247` → `ItemService.cs:152-180` / `ItemService.PendingPickups.cs:66-104` → `ItemSpawnHandler.cs:18-22` |
| Inventory-direct runtime spawns are covered | None | Every inventory-direct site (`GunScript.UnloadMag`, `AmmoScript.UnloadRound`, `BatteryItem.UnloadBattery`, `BatteryRecharger.OnUse`, `SplintLimb.TakeOff`, `TourniquetScript.TakeOff`, use-action results) calls `PickUpItem`/`AutoPickUpItem`, which is the `PickUpItemPatch` surface (`BodyItemPatches.cs:60-77` → `PickupSync.OnPickedUp`) |
| Debug/tutorial/save boundaries are explicit | None | Console commands accepted local-only; tutorial props marked per-player; `LoadedRun` stays Phase 3 saves scope |
| Verification design | None | Static source sweep only — the item is an investigation, and the development-period rule replaces manual acceptance with L0/static evidence (`no manual acceptance`) |

## Accepted boundaries (recorded, not re-discovered later)

- **Console commands** (`spawn`, `spawncategory`, `starterkit`, gift commands)
  are a manual debug surface and remain local-only; they are not a world
  refresh mechanism and are not synchronized.
- **Official save-load item restore** (`SaveSystem.TryLoadGame`) belongs to the
  Phase 3 saves scope already recorded around `WorldStartParams.LoadedRun`.
- **Tutorial course props** are per-player by design (the claw double-give
  resolution, 2026-08-16).
- **The droppings loop** is the only repeating runtime item creation. It is a
  fixed 1000 s cadence tied to `!conscious` (not random), creates an ordinary
  standalone `Item`, and therefore already rides the generic channel. No new
  message or rate limit is needed.
