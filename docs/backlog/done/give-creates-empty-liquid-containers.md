# /give creates liquid containers with no condition and no contents

- Status: Done — the grant now creates the item the game's own spawn does (batch `20261107-q`): a given cream reads condition 1 and the wound view accepts it (the release measured 10.00 ml, the patient's `disinfectionTime` went 0 → 299.445), and the guest's materialized copy agrees at condition 1.00.
- Priority: Medium
- Category: Console / item domain
- Related: `docs/backlog/done/mod-cross-player-native-semantics.md` (found while judging its topical row),
  batch `20261107-o`
- Source: batch `20261107-o` (2026-11-07): `/give @p paincream` on the host produced an item whose own
  `condition` reads 0 and whose liquid contents are empty, while `item-provide mode=create` (the game's own
  `Utils.Create` path) produces `condition=1` with the default contents.
- Evidence: `docs/evidence/acceptance/give-creates-empty-liquid-containers-20261107-q.md` (batch `20261107-q`, the
  deployed `0.1.0+8a440599`).

## The reading

```
use-eligibility mode=one type=paincream → found=true, condition=0, isUseItem=false
item-provide mode=create type=paincream → condition=1
[ItemDropped] paincream (id …) … container contents 0
```

The guest that materialized the given item read it as `condition 1.00` — the host's own copy is the broken one,
so the two clients describe the same item differently.

## Why it matters

A player who uses the console `/give` receives a container the game's own wound view refuses (`condition=0`,
empty), so the item looks broken; and the guest's copy disagrees with the host's about the same item, which is
exactly the class of divergence the item domain's fact table exists to prevent.

## Design

The item family table (`docs/design/items/README.md`) and the topical family page (`docs/design/items/topical.md`):
a container is the same item on every client, and the game's own creation path is the only template a console
grant may copy — the defect is that the grant path does not reproduce the template's condition and contents.

## Chain

1. Entry: the console `/give <definition> [state]` verb → `PlayerItemGiveService` (`src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/PlayerItemGiveService.cs:251`) → `_creation.TryCreate(definitionId, state, out item, out refusal)`.
2. Grant: `src/CasualtiesUnknownOnline.GameAdapter/Items/ItemGrantSource.cs:35-46` → `ItemDefinitionTable.TryResolve` (`ItemDefinitionTable.cs:45` → `ItemPrefabResolver.Load(id)` = `Resources.Load<GameObject>(id)` → `prefab.GetComponent<Item>()`) → `ItemStateCodec.CaptureItem(template, slotIndex: -1)`.
3. The capture (`ItemStateCodec.cs:84-110`) reads `item.condition` = 0 — the prefab ASSET's serialized field; the C# initializer `reversing/Assembly-CSharp/Assembly-CSharp/Item.cs:7174 public float condition = 1f;` only runs for editor-created components, never for the saved asset — and `CaptureLiquids(item)` reads `WaterContainerItem.stack`, which is null on the asset because `Awake` never ran (`WaterContainerItem.Awake` fills it from `LiquidItemInfo.defaultContents` only on an instance).
4. Bind: `ItemStateDocument.TryBind(template, capture, document, …)` → `bound.InstanceId = _ids.AllocateId()` — the state document therefore carries condition 0 and no stacks; the receiving item is empty.
5. Player-visible effect: the wound view's native gate `PlayerCamera.ApplyWoundItem` (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:739-763`) requires `body.conscious && item.Stats.ActuallyUsableOnLimb(item)`, and `ItemInfo.ActuallyUsableOnLimb` (`ItemInfo.cs:9-25`) asks any liquid stack `healthUsable` — with no stacks the cream is refused; the drop reads `container contents 0`.

The game's own creation, for contrast: `/spawn` (`reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs:635-670`) → `Utils.Create(id, pos, rot)` = `Object.Instantiate(Resources.Load(id), …)` (so `Awake` fills the default contents) → `item.SetCondition(num7)` with the default `1f` → the item is full and condition 1.

## Entrances

| Entrance | Input | Native call site it mirrors | Carries what the behaviour needs |
|---|---|---|---|
| `/give <definition> [state]` | console, host | the game's `/spawn` + `SetCondition(1f)` | yes — a blank asset state was never what a spawn produces |
| `/summon` (item kind) | console, host | the game's `/spawn` at the anchor | yes — same seam (`WorldSummoner.cs:165`) |
| `item-provide mode=create` | acceptance driver | `Utils.Create` directly — the control | n/a (control, not CUO's seam) |
| trader stock | `TraderRecruitCoordinator.cs:319` captures a LIVE item | — | not this seam |

## The fix (designed 2026-11-07)

`ItemGrantSource.TryCreate` must create the item the way the game does, not copy the asset: instantiate the
template (`ItemPrefabResolver.InstantiateResource(template.gameObject, Vector3.zero, Quaternion.identity)`), get
its `Item`, `SetCondition(1f)` as the game's own spawn verb does, capture THAT instance, then deactivate
(`SetActive(false)`) and destroy it before its `Start` runs — an item that never starts never reports itself to
the item domain — and keep the existing describe/overlay/bind/allocate order. The class doc's "no temporary scene
instance is created" sentence becomes "the instance never starts, so no spawn report fires".

## Family (2026-11-07)

The class is "a state copied from a prefab ASSET instead of a game-created instance". Member census:

- `ItemGrantSource.cs:46` — the one member that becomes a player's item; fixed here.
- `ItemStateDocument.DefinitionSchema` (`ItemStateDocument.cs:74-78`) — the only other template capture. It derives
  the completion SCHEMA, and the schema builder (`:406-432`) reads component type names and fields only, never values,
  so the blank asset values never reach a player. It stays as it is, and the gate pins it as the one remaining
  template capture.
- The acceptance recipe's `item-provide mode=create` is the game's own `Utils.Create` — the control, not a member.

The structural point: the grant seam is the only place the item domain turns a definition into a fact, so fixing it
there fixes both console verbs at once; no caller needs its own copy of the rule.

## The reading on the fixed build (batch 20261107-q)

```
18:53:16.598 [ItemCreate] prepared paincream (id 1340350777283) from a fresh instance of its prefab.
host use-eligibility type=paincream predicate=medical → condition=1, isEligible=true
18:53:16.612 guest [StandingItem] materialized paincream (id 1340350777283) … condition 1.00
18:55:37.512 host [MedicalView] measured 10.00 ml from paincream's own native limb action … (id 1340350777283)
18:55:37 patient limb 5 disinfectionTime 0 → 299.445
```

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| The grant's state capture (`ItemGrantSource.TryCreate`) | captures a game-created instance instead of the prefab asset | `[ItemCreate] prepared paincream … from a fresh instance of its prefab` (18:53:16.598) |
| The game's own creation (`ConsoleScript` `/spawn`) | mirrored: instantiate, then `SetCondition(1f)` | `reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs:635-670`; `ItemStateCodec` restore path unchanged |
| The wound view's admission (`IsMedicalLimbUseItem`) | the given cream is accepted | `use-eligibility predicate=medical → isEligible=true`; the release measured 10.00 ml |
| The patient's limb (`reliefcream.onHealthUse`) | the dose applied | `disinfectionTime` 0 → 299.445 (the game's own `SetDisinfect(300·clamp01(ml·0.1))`) |
| The guest's copy (standing materialization) | agrees with the host | `[StandingItem] materialized … condition 1.00` (18:53:16.612) |
| The temporary instance's lifecycle | never starts, never reports | `ItemGrantGateTests` pins the shape; the class doc states the rule |

## Acceptance

| Row | The check |
|---|---|
| 1 | `/give @p paincream` on the host, then `use-eligibility mode=one` reads `condition=1` and the wound view accepts the item. **PASS 20261107-q**: condition=1, `isEligible=true` (medical), and the release measured 10.00 ml. |
| 2 | The host's own copy and the guest's materialized copy agree on condition and contents (`[StandingItem] materialized … condition` line + `body-items`). **PASS 20261107-q**: host condition 1; guest `condition 1.00`; the dose drained the cream's own contents. |
