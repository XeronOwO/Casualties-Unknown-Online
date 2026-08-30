# Dynamite lit-fuse presentation sync

Owner cycle: the last item-domain accepted presentation residual — the
5-second dynamite lit-fuse visual/audio on remote clones was local-only.
Decision for this cycle: close it with the same explicit synthetic
component-field pattern as the liquidcentrifuge cooldown, so the existing
item-state paths carry the fuse latch and the clone renderer presents the lit
sprite/audio before the detonation message arrives.

Decision summary:

- The native use action enables a child `SpriteRenderer` and plays the item's
  `AudioSource` on the trigger side only (Item.cs:6678-6680), then schedules
  `CustomItemBehaviour.DynamiteExplode` 5 seconds later.
- New `CustomItemDataState` (Game Adapter/Items) maps `data[0] = true` on
  `dynamite` to a synthetic `fuse` component field (kind bool).
  `ItemStateCodec` emits and restores it alongside the normal
  `CustomItemBehaviour` state digest.
- `RemoteItemPresentation.Apply` enables the clone's child sprite when the
  wire says `fuse = true`, and a persistent `DynamiteFuseAudioReplay` marker
  plays the clone AudioSource exactly once for the fuse lifetime. The same
  `ApplyDynamiteFuse` presentation runs from `ItemApplication` on corrected
  world-item copies, so a used-in-place dynamite also shows its fuse to peers.
- The detonation continues to ride the existing `DynamiteExplosionMsg`
  (NetMsg 105); this slice only adds the preceding presentation.
- No new wire message, no direction row, no `ProtocolVersion` bump.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Dynamite use action sets `data[0] = true`, enables child 0's `SpriteRenderer`, plays `AudioSource`, schedules `DynamiteExplode` in 5 s | Item.cs:6671-6682 |
| 2 | `CustomItemBehaviour.data` is a public `object[]`, unsupported by the generic saveable-field codec | CustomItemBehaviour.cs:574-583; ItemStateCodec.cs:227-231 |
| 3 | The explosion event carries the one-shot item id + position and replays the body/visual explosion | DynamiteExplosionMsg.cs; DynamiteExplosionSync.cs; docs/decisions/active.md #40 |
| 4 | Existing item-state paths capture/restore component digests on carried sync, world correction, character snapshots, spawn/drop and reconnect restore | ItemStateCodec.cs:184-263, 332-386; CharacterDataSync, PickupSync, ItemUseSync, ItemApplication, CloneInventoryRenderer |
| 5 | Clone inventory rendering instantiates by prefab and applies `RestoreComponentStates` + `RemoteItemPresentation.Apply` on every snapshot | CloneInventoryRenderer.cs:58-200 |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `CustomItemDataState` | New pure codec face: capture returns `fuse` bool field for dynamite, restore predicate + array write, deterministic false capture before use |
| `ItemStateCodec` | Captures the synthetic field for dynamite in `CaptureSaveableComponents`; restores it in `RestoreComponentStates` |
| `RemoteItemPresentation` | Applies the child-sprite enable and adds the one-shot audio replay marker when `fuse = true` |
| `ItemApplication` | Calls the same fuse presentation on corrected world-item copies in `ApplyAuthoritativeState` |
| `DynamiteFuseAudioReplay` | New persistent MonoBehaviour marker: plays the `AudioSource` once, stays for the fuse lifetime so 1 Hz refreshes never re-trigger |
| Existing item-state paths | Unchanged — all paths already call the same capture/restore pair |
| Protocol | No new message, no `ProtocolVersion` bump |
| Jetpack throttle | Deliberately unchanged (frame-level transient) |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Capture | Dynamite emits one `fuse` bool field | `CustomItemDataStateTests.Capture_DynamiteFuse_ReturnsSyntheticBoolField` |
| Capture default | Null/empty/false array emits false before use | `Capture_DynamiteMissingOrFalse_UsesFalse` |
| Non-target | Other item ids emit no synthetic field | `Capture_NonDynamite_ReturnsNull` |
| Restore predicate | Only dynamite + `fuse` + bool is applied | `IsFuseField_OnlyMatchesDynamiteBoolFuse` |
| Restore write | Existing array mutated; missing array created | `With_DynamiteFuseExistingData_MutatesTheSameArray`, `With_DynamiteFuseMissingData_CreatesArrayAndSetsValue` |
| Clone decision | `fuse = true` selects lit presentation | `RemoteItemPresentationTests.DynamiteFuse_PresentAndTrue_ReturnsTrue` |
| Clone decision | `fuse = false`/missing keeps unlit | `DynamiteFuse_PresentButFalse_ReturnsFalse`, `DynamiteFuse_MissingField_ReturnsFalse` |
| Audio replay | Adapter declares the one-shot marker with a non-static Start | `RemoteItemPresentationTests.Adapter_DeclaresDynamiteFuseAudioReplayMarker` |
| Integration | `ItemStateCodec` calls the helper on capture and restore | Static evidence: ItemStateCodec.cs capture/restore branches |
| Game update guard | `CustomItemBehaviour.data` remains a public `object[]` | Existing `GameField_CustomItemBehaviourDataRemainsPublicObjectArray` (still passes) |

## 4. Verification design (development-period, no manual acceptance)

- **L0**: the new pure helpers are exercised through reflection (the adapter is
  compile-excluded from the test project), same host as the existing
  GameAdapter contract tests.
- **Static**: build + full test suite + `dotnet format` + architecture gate +
  event-replay gate + entity-event dispatch gate.
- **No manual dual-side acceptance**: per the development-period rule, runtime
  verification is represented by the L0 contract/unit evidence plus the
  existing item-state round-trip paths; no user logs or dual-open are required
  for this cycle.

## 5. Delivery notes

- Touched: `CustomItemDataState.cs`, `ItemStateCodec.cs`,
  `RemoteItemPresentation.cs`, `ItemApplication.cs`,
  `DynamiteFuseAudioReplay.cs` (new), `CustomItemDataStateTests.cs`,
  `RemoteItemPresentationTests.cs`, `docs/features/items.md`,
  `docs/backlog/README.md`, `docs/decisions/active.md` (#53).
- Dead mechanisms: none — the previous local-only presentation had no separate
  sync path to delete.
- Structure review: new files are small single-purpose types; `ItemStateCodec`
  and `RemoteItemPresentation` remain under the 600-line gate.
