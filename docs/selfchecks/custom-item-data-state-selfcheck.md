# CustomItemBehaviour.data — liquidcentrifuge cooldown sync

Owner cycle: backlog item-domain recorded gap "CustomItemBehaviour.data payload
unsupported / liquidcentrifuge cooldown timer stays local-only". Decision for
this cycle: close the persistent gameplay state — the 60-second use-gating
cooldown — with an explicit synthetic component field on the existing
item-state paths. The remaining payload entries (jetpack throttle, dynamite
lit-fuse visual) stay non-synced because they are transient/presentation or
already covered by the dedicated dynamite explosion event.

Decision summary:

- `CustomItemBehaviour.data` is `object[]` (CustomItemBehaviour.cs:582-583), so
  the generic `[Saveable]`-field codec cannot carry it. The liquidcentrifuge
  cooldown is `data[0]` as float and it gates the use action
  (Item.cs:5667-5689): a use is refused while `data[0] > 0`, and a successful
  use drains the container and sets `data[0] = 60f`.
- New `CustomItemDataState` (Game Adapter/Items) maps that value to a synthetic
  `cooldown` component field (kind float). `ItemStateCodec` emits and restores
  it alongside the normal `CustomItemBehaviour` state digest.
- `CustomItemBehaviour.Start` initializes `data[0] = 0f` on every fresh prefab
  (CustomItemBehaviour.cs:9-17), which runs after a restore that happens
  immediately after `Instantiate`. To survive that lifecycle, restore also adds
  a one-frame `LiquidCentrifugeCooldownRestore` marker that reapplies the value
  from `Update` (after Start) and destroys itself.
- No new wire message, no direction row, no `ProtocolVersion` bump.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | `CustomItemBehaviour.data` is a public `object[]`; `data[0]` is the liquidcentrifuge cooldown float | CustomItemBehaviour.cs:574-583; Item.cs:5667-5689 |
| 2 | Native Start initializes `data = new object[1]; data[0] = 0f` for liquidcentrifuge | CustomItemBehaviour.cs:9-17 |
| 3 | Update decrements the cooldown and drives the sprite/countdown presentation | CustomItemBehaviour.cs:300-316 |
| 4 | Use action refuses while `data[0] > 0`; success drains and sets 60f | Item.cs:5670-5688 |
| 5 | Generic codec cannot serialize `object[]` (unsupported kind) | ItemStateCodec.cs:227-231 |
| 6 | Existing item-state paths capture/restore component digests | ItemStateCodec.cs:184-263, 332-386; CharacterDataSync, PickupSync, ItemUseSync, ItemApplication, CloneInventoryRenderer |
| 7 | After a fresh `Instantiate`, `Start` runs before the next frame's `Update` | Unity lifecycle (static scheduling evidence; marker design) |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `CustomItemDataState` | New pure codec face: capture returns `cooldown` float field, restore predicate + array write, default-zero capture before Start |
| `ItemStateCodec` | Captures the synthetic field for liquidcentrifuge in `CaptureSaveableComponents`; restores it in `RestoreComponentStates` and adds/refreshes the one-frame marker |
| `LiquidCentrifugeCooldownRestore` | New one-frame MonoBehaviour reapply after `CustomItemBehaviour.Start`, then self-destroys |
| Existing item-state paths | Unchanged — all paths already call the same capture/restore pair, so the new field rides carried sync, world corrections, character snapshots, spawn/drop and reconnect restore |
| Protocol | No new message, no `ProtocolVersion` bump (an unknown field name is ignored by older peers) |
| Jetpack throttle / dynamite fuse visual | Deliberately unchanged (frame transient / presentation; dynamite detonation already has its own event) |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Capture | Liquidcentrifuge emits one `cooldown` float field | `CustomItemDataStateTests.Capture_LiquidCentrifugeCooldown_ReturnsSyntheticFloatField` |
| Capture default | Null/empty/malformed array emits 0 before Start | `Capture_NullOrMissingData_UsesTheNativeDefaultZero`, `Capture_NonFloatFirstElement_UsesTheNativeDefaultZero` |
| Non-target | Other item ids emit no synthetic field | `Capture_NonLiquidCentrifuge_ReturnsNull` |
| Restore predicate | Only liquidcentrifuge + `cooldown` + float is applied | `IsCooldownField_OnlyMatchesLiquidCentrifugeFloatCooldown` |
| Restore write | Existing array mutated; missing array created | `With_LiquidCentrifugeExistingData_MutatesTheSameArray`, `With_LiquidCentrifugeMissingData_CreatesArrayAndSetsValue` |
| Start lifecycle | A marker exists and carries float cooldown | `Adapter_DeclaresCooldownRestoreMarker` |
| Game update guard | `CustomItemBehaviour.data` remains a public `object[]` | `GameField_CustomItemBehaviourDataRemainsPublicObjectArray` |
| Integration | `ItemStateCodec` calls the helper on capture and restore | Source/static evidence: ItemStateCodec.cs lines 246-257, 348-361 |

## 4. Verification design (development-period, no manual acceptance)

- **L0**: the new pure helper is exercised through reflection (the adapter is
  compile-excluded from the test project), same host as the existing
  GameAdapter contract tests.
- **Static**: build + full test suite + `dotnet format` + architecture gate +
  event-replay gate + entity-event dispatch gate.
- **No manual dual-side acceptance**: per the development-period rule, runtime
  verification is represented by the L0 contract/unit evidence plus the
  existing item-state round-trip paths; no user logs or dual-open are required
  for this cycle.

## 5. Delivery notes

- Touched: `CustomItemDataState.cs` (new), `LiquidCentrifugeCooldownRestore.cs`
  (new), `ItemStateCodec.cs`, `CustomItemDataStateTests.cs` (new),
  `docs/item-features.md`, `docs/backlog.md`, `docs/tech-decisions.md` (#52).
- Dead mechanisms: none — no old cooldown path existed to delete.
- Structure review: new files are small single-purpose types; `ItemStateCodec`
  is still under the 600-line gate.
