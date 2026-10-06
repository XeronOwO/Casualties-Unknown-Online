# Item-Fact Report Carriers — Self-Check (2026-10-06)

Delivery fact sheet for backlog item `todo/container-move-snapshot-only-sync.md`: an item
mutation a peer asked for is executed by its OWNER, and the owner's item-fact carriers now report
it exactly as they report the owner's own gesture. Batch `20261005-c` read the
`[CharSync] divergence` warning on the operator's clone fact table for every remote container move
it drove; the warning was right and the event was missing.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The owner's own local container move already carries its own event: `ContainerItemSync.OnLoadedIntoContainer` reports the top-level carried root through `SendItemCarriedSync` (host) or `SendItemContainerContent` (guest report the host relays). | `src/CasualtiesUnknownOnline.GameAdapter/Items/ContainerItemSync.cs`, "a move INSIDE the carried inventory … the parent container's FULL fact is one operation = one message" |
| 2 | A remote-driven container move runs the SAME native pair on the owner's real objects, with the landed check on the parent. | `src/CasualtiesUnknownOnline.GameAdapter/RemoteIntentApplier.cs` `ApplyMoveIntoContainer` (`container.UnloadItem(item, null)` then `container.LoadItem(item)`), `ApplyTakeOutOfContainer` (`container.UnloadItem(item, null)`), `ApplyMoveContainerChildren` |
| 3 | The carriers silenced that same fact: `ContainerItemSync` (three hooks), `PickupSync.OnPickedUp` and `ItemWorldSync` (instantiate/destroy/drop/throw) each returned early on the bare `RemoteApply` attribution. | the four files named above, pre-change first guard of each report hook |
| 4 | The bare attribution is not a defect detector but an overloaded identity: the character restore re-materializes items through the game's own slot path under `RemoteApply`, and reporting those made the host refuse the restored items as `Conflict (item … is already carried)`. | `tests/CasualtiesUnknownOnline.NormativeGates.Tests/SourceShapeGateTests.cs` `CharacterRestore_MaterializesItemsInsideARemoteApplyScope` |
| 5 | The sibling report hooks of the same family carry NO call-context guard — `ItemSlotSync.OnSlotMoved` (called from the `Body.SwapSlots` / `Body.SwitchHands` postfixes) and `ItemUseSync.OnItemUsed` report from a replayed call — which is why the batch's row-6 slot warning is the PICKUP half and not a slot-report gap: the remote slot release replays `DropItem`/`PickUpItem` and never `SwapSlots`, so its slot fact travels through the guarded `PickupSync.OnPickedUp` → `_slotSync.OnItemRehomed`. | cited paths; `RemoteIntentApplier.ApplySwapSlots` (reaches `OnSlotMoved`) versus `ApplyPickUpToSlot` (does not); batch `20261005-c` `r1-host-log.txt` `bandage … slot 2 → 5 — a carried move without an event sync` |
| 6 | The divergence monitor is `CloneFactTable`'s own and compares the event-driven fact table against the incoming snapshot by instance id. | `src/CasualtiesUnknownOnline.GameAdapter/Character/CloneFactTable.cs` `WarnOnDivergence` |

Whole-family audit: the same rule was applied to every report hook of the three carriers, not to the
container wordings the batch happened to see. The sites that actually fire INSIDE the intent scope are the
container LOAD/UNLOAD hooks, the pickup hook and the drop hook; `ItemWorldSync.OnItemInstantiated` is an
`Item.Start` postfix and `OnItemDestroyed` an `OnDestroy` postfix, both running after the scope closes, so
they report either way; and `ContainerItemSync.OnUnloadedAll` is the container-BROKE path
(`Container.UnloadAllItems`), which no intent reaches — the three conversions state the rule consistently
rather than changing behaviour. Guards on OTHER families that legitimately mean "a replayed fact"
(`HeaterCookSync`, the world replays, `ItemApplication`, `CharacterRestoreApplier`) were left on the bare
query on purpose. One carrier that legitimately means it but is the closest sibling of all is named
explicitly so it is not read as an oversight: `CraftingPatches.BodyCombinePatch` suppresses the craft
report for a remote-driven `CombineItems`, because that fact rides the intent's own authoritative
re-report and a second channel for one intent is what the rework's design forbids; the divergence monitor
does not compare the ammo/charge state a combine moves, so no monitor symptom follows.

## 2. The change

- `CallContext.Origin.RemoteIntentApply` — "this client is executing a PEER's intent on its own
  objects". It is opened by `RemoteIntentApplier` INSIDE its existing `RemoteApply` scope, never
  instead of it: the presentation and echo guards (item sounds, world replays, every "this is not my
  action" guard) must keep answering for a remote application.
- `CallContext.IsReplayedRemoteFact` = `IsWithin(RemoteApply) && !IsWithin(RemoteIntentApply)` — the
  only remote-apply question an item-fact carrier may ask.
- The three carriers ask it. A replay stays silent; a peer's intent this client executed reports
  through the same carrier a local gesture uses, so a remote-driven container move, drop, pickup or
  destroy travels as an event instead of on the character snapshot.

No wire member, no protocol bump, no authority change: every carrier lit up here already had its
event kind.

## 3. Verification

| Mechanism × change × evidence | Evidence |
|---|---|
| The distinction exists and composes — a replay answers true, an intent execution false, through a nested classification scope | `tests/CasualtiesUnknownOnline.Tests/Patching/CallContextCompositionTests.TheReplayQuery_ExcludesAPeerIntentExecution` (passes) |
| A replay is still silent, so the restore's reports cannot come back | `CallContextCompositionTests` (the bare `IsWithin(RemoteApply)` answer is asserted true under the intent scope) and the restore gate above (unchanged, passes) |
| No carrier gates on the bare attribution any more, and each carrier states the distinction | `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ItemFactReportScopeGateTests` — FAILED (3 of 17) before the change, passes after |
| An event-carried container move leaves the next snapshot unwarned, and a move the event chain missed still warns with the batch's two wordings | `tests/CasualtiesUnknownOnline.Tests/Patching/CloneFactTableDivergenceMonitorTests` (both cases pass) |

Red before the fix, recorded from the build of the pre-change tree:

```text
失败 CasualtiesUnknownOnline.Tests.Tooling.NormativeGates.ItemFactReportScopeGateTests.TheItemFactCarriers_AskTheReplayQueryAndNeverTheBareAttribution
  src\...\Items\ItemWorldSync.cs: 1 `CallContext.IsWithin(CallContext.Origin.RemoteApply)` guard(s) …
  src\...\Items\PickupSync.cs: 1 …
  src\...\Items\ContainerItemSync.cs: 1 …
失败 …CallContext_DeclaresTheReplayQueryThatExcludesAPeerIntentExecution
失败 …CallContext_DeclaresThePeerIntentExecutionOrigin
失败! - 失败: 3，通过: 14，总计: 17
```

## 4. Limits

- The gate pins SOURCE SHAPE, not runtime behaviour: it cannot see a carrier that gates through a
  helper or a switch arm, and it says so in its own doc comment.
- No session was run in this cycle. The behaviour the change is FOR — a remote-driven move reporting
  its fact, and the operator's monitor reading zero warnings over a full cycle — is the three-client
  acceptance run's row, named in the ticket's `What remains`.
- One consequence is reasoned rather than observed, and the reasoning was corrected by the independent
  review before the commit: a remote-driven `DropItem` now sends the drop report from the owner's own
  hook, and that report was the ONLY carrier in BOTH owner directions. It is what the peers materialize
  from (`ItemApplication.OnRemoteItemDropped`) and what creates the host's world-table row
  (`ItemProjection.ApplyDrop` / `ApplySpawn`); the keyframe's `RefreshWorldItemStates` early-returns for
  an id the table does not already hold, and the 10 Hz follow stream only moves copies that already exist
  (`ItemPositionFollow` on a missing copy). The earlier draft claimed the host-owner direction was healed
  by the host's own world-item stream — that is wrong on both counts, and the batch's row 5 ran the
  host-owner direction and read only the owner's own pickup, which is the half that stays true while a
  peer's copy never appears. The acceptance run must read the dropped item on a third peer, in both
  directions.
- The `PickUpToSlot` gesture replays a slot drop plus a pick-up in one bracket; the pickup hook now
  cancels the pending drop and reports the move as a slot re-home, which is the local shape. Read on
  the operator's monitor by the same run.
- A note for the NEXT sibling conversion: `CallContextScopeCompositionGateTests.MinimumAttributionSites`
  is 12 and the change left exactly 12 bare-attribution sites under `src/`, so it now passes with zero
  slack. A further conversion has to lower or re-justify that floor in the same change.
