# Mod content ceiling: the native label surface, cross-player semantics and the parked gaps

- Status: Future
- Priority: Medium
- Category: Mod platform / architecture
- Related: `docs/en/reference/mod-api.md`, `docs/en/reference/modification-policy.md`, `docs/backlog/review/cucorelib-migration-support.md`, `docs/backlog/future/phase5-tooling-ecosystem.md`
- Source: The 2026-09-25 tag/quality inventory and mod-ceiling analysis — the whole session's finding; user decision 2026-09-25 — ONE future ticket holding every item, nothing filed under `todo/`, and the implementation split into stages when it is promoted.

## Why this ticket exists

The session started from a narrow question ("is there a Forge-style tag system, where a recipe can ask
for a tag instead of an item?") and ended with the mod platform's ceiling. Everything that came out of
it lives here so it is not re-derived: the native label surface and what CUO exposes of it (Part 1), the
cross-player chains that cap what mod content can do online (Part 2), and the smaller gaps with no
consumer (Part 3). This is a memo, not a work item: nothing below is implemented until its entry is
promoted.

## The ceiling, in three layers

| Layer | Question | State |
|---|---|---|
| 1. Definition | can content be added at all? | 12 content kinds, 9 with a provider |
| 2. Local semantics | does the content behave like vanilla content? | tags yes; crafting qualities, limb use and liquid effects no |
| 3. Online semantics | does it work between players? | carried/dropped/container/durability follow the generic mechanisms; "use it on another player" is a curated id table |

## What the game itself leaves open (seam inventory)

The game has no mod API by design: no data files, no event bus, no published extension points. What a
mod can attach to are five implementation-level seams:

| Seam | Form | Note |
|---|---|---|
| Static registries | `Item.GlobalItems`, `Liquids.Registry`, `Recipes.recipes`, `ItemLootPool.pool` | entries can be added, but every table is filled by hardcoded C# |
| Delegate fields | `ItemInfo.useAction` / `useLimbAction`, `LiquidType.onDrink` / `onHealthUse` | the closest thing to a hook: assigning the delegate replaces the behaviour |
| Attribute plus reflection | `[Saveable]` on a field or property, captured by `SaveSystem` | a mod component persists without inventing a format |
| String resource loading | `Resources.Load` by id or path (item prefabs, `Sprites/...`, sound ids) | a mod references the game's own assets; shipping its own is a separate capability |
| Unity plus BepInEx/Harmony | everything is patchable | the universal seam, not a designed one |

Two traps worth recording:

- `CustomItemBehaviour` looks like the mod hook for item behaviour but is a `ComputeStringHash(item.id)`
  switch over vanilla ids, so attaching it to a mod item does nothing. Its `state` and `data` payload is
  what the item state codec whitelists.
- `ItemInfo.tags` and the crafting `qualities` are two separate namespaces with no shared registry, and
  the two vocabularies even overlap (`water` and `dressing` appear in both).

## Part 1 — the native label surface (tags and crafting qualities)

What exists, measured 2026-09-25:

| | `ItemInfo.tags` | `CraftingQuality` |
|---|---|---|
| Shape | comma-separated string, split into `actualTags[]`, queried with `HasTag` (ordinal) | `List<CraftingQuality{id, amount}>`, on items and on liquids |
| Vocabulary | 14 ids (`medicine` 55, `cangetwet` 50, `tool` 27, …) | 18 ids (`rippable` 27, `hammering` 19, `water` 18, …) |
| Consumers | behaviour gates and `Container.tagRestriction` (an intersection test) | recipe matching (`RecipeItem.GetMatchingItem`, amount-aware) and the recipe panel |
| In recipes | never read | 169 of the 613 ingredient entries; 99 of those carry `destroyItem = false` (a tool is required, not consumed) |

So "a tag recipe" in this game is a *quality* recipe. The extra semantics are the amount (it scales the
condition the ingredient spends) and `minimumCondition`; a liquid scales its qualities by millilitres.

Two decisions stand from 2026-09-25:

1. **Do not merge the two vanilla fields.** Their consumers are hardcoded and separate, so a merged field
   would either be invisible to recipes (`tags`) or silently grant crafting qualities and durability
   semantics (`qualities`). Unification belongs in the CUO mod API, not in the game data.
2. **Namespace mod-authored label ids** (`mymod:material`), reusing the content-id grammar. Matching
   across mods already works because it is a string comparison; the namespace buys collision safety and
   is the precondition for a declared vocabulary later. It costs a convention, not architecture.

What CUO exposes today: `ModItemDefinition.Tags` (written into `ItemInfo.tags`, with `ApplyTags` filling
the private `actualTags`), `ModItemContainer.TagRestriction`, `ModLiquidDefinition.Qualities`, and
`ModRecipeIngredient.Quality` / `QualityAmount` (mapped to `RecipeItem.quality` with `ignoredId` and the
repair rule mirrored). What it does not: item qualities, limb use, the liquid effect delegates, and any
validation of a label reference — those are Part 2 stage 1 and Part 3.

## Part 2 — cross-player semantics: curated tables become capability predicates

### Problem (evidence)

Every "use this on another player" chain is a hand-written allowlist of vanilla ids, with the effect
formulas copied out of the game:

- `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteConsumeCatalog.cs` — `Food`
  keyed by 25 vanilla item ids and `Liquids` by 14 vanilla liquid ids, each entry carrying the effect
  coefficients; the type's own comment says "Unknown liquids/items are deliberately refused by the
  host so an unsupported effect is never silently approximated."
- `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteMedicineCatalog.cs` —
  `InjectionAmounts` (15 containers) plus `Liquids` (immediate, opiate and timed branches), with the
  comments naming the decompiled sources they were transcribed from (`Liquids.cs` `Drink`/`Inject`
  formulas and the `onHealthUse` branches).
- The same shape in `RemoteTopicalCatalog`, `RemoteLimbToolCatalog` and `RemoteWearCatalog`; the
  decisions that introduced the slices describe them as curated (`docs/decisions/archive.md` entries
  98, 100, 103).

Consequences:

- Mod content — and any vanilla content the tables do not carry — can be carried, dropped, traded and
  saved, but it has no cross-player semantics: a mod food cannot be fed to another player, a mod
  medicine cannot be injected, a mod dressing cannot be applied, a mod wearable cannot be put on.
- The ceiling of the mod platform is therefore our maintenance speed, not the game's own capability.
- The transcribed constants are a patch-stack debt against the root-cause rule in `AGENTS.md`: a game
  update that changes a formula moves the game and leaves our copy behind, silently.
- The content DTOs cannot even declare what these chains would need: `ModItemDefinition` has no
  qualities and no limb-use behaviour, `ModLiquidDefinition` has no drink/health delegates, and
  `GameAdapterItemContentProvider.BuildItemInfo` writes no `info.qualities`.

### Direction (staged — the stage split is mandatory)

Each stage is its own deliverable with its own verification, and stage 1 must not be smuggled in as
preparation for stage 2.

**Stage 1 — content surface parity (small, self-contained).** `ModItemDefinition.Qualities` decoded
into `ItemInfo.qualities`, so a mod item can satisfy a quality-based recipe (vanilla and mod-authored).
Add the liquid effect delegates and the limb-use behaviour only behind a real consumer (see *Open
questions*). Acceptance: a mod item satisfies a quality recipe, and a recipe that references a quality
nobody provides is reported at load time instead of becoming a silently dead recipe.

**Stage 2 — semantic predicate plus target-local execution (its own architecture ticket).** Replace the
id tables with predicates over the game's own data: the native eat/drink branch for solid food,
`LiquidType.onDrink`, `injectable` with a `WaterContainerItem`, `Stats.HasTag("dressing")` or a non-null
`useLimbAction`, `wearable` with `desiredWearLimb` / `wearSlotId`. The effect runs on the target's own
client through the native path; the host keeps only "is this operation allowed", resource consumption
and first-writer-wins arbitration. A precedent for the shape already exists: the timed medicine
branches travel as `TimedBodyEffectMsg` and run on the target's local body through the native
`CoUtils.DoTimedOp` path. Migrate one chain first (injection or topical — medical is CUO's main line and
already has the operation session), then the remaining chains. Hard acceptance for the migration:
delete the constant table and every existing acceptance case of that chain stays green.

**Stage 3 — mod-registered semantics (only if stage 2 leaves a real need).** A registration surface for
semantics the game's own data cannot express (a mod-authored medical interaction, for example). It needs
a wire face, a permission and consistency rules; trigger is a second real consumer.

### Red lines that do not change

- The allowed operation set, resource consumption and conflict arbitration stay host-side
  (first-writer-wins).
- The affected side judges on its own picture and timeline; latency never becomes a judgment input.
- Compatibility is not a design input: the curated tables are replaced, never kept alive beside the
  predicates.

## Part 3 — the parked surface gaps

### A. Content-surface field gaps

The vanilla type carries the field; the mod DTO does not, so the content cannot declare it.

Item side (`ItemInfo` versus `ModItemDefinition` and its behaviour DTOs):

- **item qualities** — the stage 1 item of Part 2, not repeated here.
- **`useLimbAction`** — apply to a limb (bandage, splint, tourniquet, amputation). The only mention in
  `src/` is a comment in `RemoteLimbToolProfile.cs`, so CUO covers its own curated list and offers no mod
  surface.
- **the wearable set** — `wearableArmor`, `wearableIsolation`, `wearableHitDurabilityLossMultiplier`,
  `desiredWearLimb`, `wearSlotId`, `wearableCanBeHeld`, `wearableVisualOffset`; the last two appear in
  `src/` only as reads.
- **miscellaneous** — `rec` (recognition), `onlyHoldInHands`, `combineable`, `ignoreDepression`,
  `scaleWeightWithCondition` (read-only in `CarriedEncumbranceCalculator`), `jumpHeightMultChange`,
  `slotRotation` (read-only in the clone renderer), `usableOnLimb`.

Liquid side (`LiquidType` versus `ModLiquidDefinition`):

- **`onDrink` / `onHealthUse`** — the effect delegates. `GameAdapterLiquidContentProvider` maps only the
  static fields, so a mod liquid can declare `HealthUsable` while no effect function exists.

### B. Declaration versus materialization

The framework says a content kind exists, but nothing consumes it — the same silent shape as a recipe
that references a quality no item provides:

- `ModContentKind.Entity`, `ModContentKind.Setting` and `ModContentKind.Locale` have **no content
  provider** (the nine providers cover the other nine kinds). `Entity` appears once in `src/`, as the
  console resource-location label of the built-in player entity; `Setting` and `Locale` do not appear at
  all. A registration for one of them is accepted and materializes nothing.
- Nothing checks at load time that a `ModRecipeIngredient.Quality` is provided by any item or liquid
  anywhere, so a mistyped or unprovided quality becomes a recipe that can never be crafted, silently.

### C. Capability the game itself does not have

These need new capability rather than a predicate, and none has a consumer:

- **recipe override** — change or delete the 132 hardcoded vanilla recipes; CUO can only append.
- **new layer modifiers and biomes** — the vanilla `LayerModifier` family.
- **custom enemies** — an entity definition, generation, host-authoritative replication and behaviour.
- **settings** — game settings and run settings, with session consistency for the run-affecting ones.
- **locale, audio and art resources** — a mod authors resource *paths* into the game's own assets today;
  shipping its own assets is a separate capability.
- **skills and progression rules**, **tutorial courses**.
- **UI** — the control set beyond `IModUi`'s four controls, and injecting into native panels.
- **mod-authored component state across peers** — the item and limb component codecs are whitelists, so a
  mod component's own fields have no cross-peer channel.
- **custom replication domains** — a mod owns its own consistency if it goes through `IModNetwork`.

## Promotion rule

Nothing here enters `todo/` until its entry is promoted: the 2026-09-25 ruling was that the whole
finding waits in `future/`. Each entry is then promoted on its own, as its own ticket, with a real
consumer named (the promotion funnel in `docs/en/reference/modification-policy.md`), and every promoted
entry is split into stages that each produce a verifiable result.

Two entries change class when promoted, because they are defects of a surface the API already promises
rather than new capability: **a content kind with no provider**, and **a quality reference with no
provider**. Both should then be filed at a higher priority and carry a reachable failure path.

## Open questions

- Vanilla null-delegate behaviour: a mod liquid with `HealthUsable = true` and no `onHealthUse` — does the
  native path null-check or throw? The answer decides whether that half of stage 1 is a defect fix or a
  new feature.
- Stage 3 with mixed-mod sessions: does the existing handshake consistency (mod id, version, permissions,
  `NativeBinding` parity) cover "same content, different semantics"?
- Part 3's kind-versus-provider mapping: is a warning at registration the right shape, or should the
  unimplemented kinds leave `ModContentKind` until a provider exists?

## Non-goals

- Not an argument for widening the API now; it is the candidate list, not a plan.
- Not a Forge/Fabric-style ecosystem — that is `phase5-tooling-ecosystem.md`.
- Not the KrokMP migration surface — that is `cucorelib-migration-support.md`.
