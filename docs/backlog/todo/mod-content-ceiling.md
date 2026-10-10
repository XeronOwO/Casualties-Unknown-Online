# Mod content ceiling: the native label surface, cross-player semantics and the parked gaps

- Status: Todo — **promoted 2026-10-07** by the user's backlog directive (open-ended content customisation and
  a mod-extensible platform); the stages below are the work plan, cut into their own tickets as each one
  starts, and the inventory stays the source of truth.
- Priority: High
- Category: Mod platform / architecture
- Related: `docs/backlog/todo/mod-authored-effects.md` (Stage 3, cut out on 2026-10-08 with the user's
  ruling that supersedes its consumer gate),
  `docs/backlog/review/mod-declared-behaviour-with-no-function.md` (Part 3.A's declared-behaviour gap,
  cut out on 2026-10-08 with its item twin), `docs/backlog/review/mod-defined-wire-packets.md` (Part 3's
  custom replication domains, promoted on its own),
  `docs/backlog/review/mod-cross-player-native-semantics.md` (Part 2 stage 2, cut out on 2026-10-08),
  `docs/en/reference/mod-api.md`, `docs/en/reference/modification-policy.md`,
  `docs/backlog/review/cucorelib-migration-support.md`, `docs/backlog/future/phase5-tooling-ecosystem.md`
- Source: The 2026-09-25 tag/quality inventory and mod-ceiling analysis — the whole session's finding; user
  decision 2026-09-25 — ONE ticket holding every item, nothing filed under `todo/`, and the implementation
  split into stages when it is promoted. The 2026-10-07 directive is that promotion.

## Why this ticket exists

The session started from a narrow question ("is there a Forge-style tag system, where a recipe can ask
for a tag instead of an item?") and ended with the mod platform's ceiling. Everything that came out of
it lives here so it is not re-derived: the native label surface and what CUO exposes of it (Part 1), the
cross-player chains that cap what mod content can do online (Part 2), and the smaller gaps with no
consumer (Part 3). It was written as a memo; it is now the promoted work item, and every stage below is
implemented as its own deliverable with its own verification.

## The ceiling, in three layers

| Layer | Question | State |
|---|---|---|
| 1. Definition | can content be added at all? | 9 content kinds, each with a provider |
| 2. Local semantics | does the content behave like vanilla content? | tags yes; item and liquid qualities yes, and a quality reference that resolves to nothing is refused (stage 1, landed 2026-10-07); limb use and liquid effects no |
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
the private `actualTags`), `ModItemDefinition.Qualities` and `ModLiquidDefinition.Qualities` (each written
into the game's own quality list for its kind, which is the field the vanilla matcher reads),
`ModItemContainer.TagRestriction`, and `ModRecipeIngredient.Quality` / `QualityAmount` (mapped to
`RecipeItem.quality` with `ignoredId` and the repair rule mirrored, and checked against the labels a
provider in the ingredient's own direction can reach). What it does not: limb use and the liquid effect
delegates — those are Part 3.

## Part 2 — cross-player semantics: curated tables become capability predicates

### Problem (evidence)

Every "use this on another player" chain is a hand-written allowlist of vanilla ids, with the effect
formulas copied out of the game:

- `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteConsumeCatalog.cs` — `Food`
  keyed by 25 vanilla item ids and `Liquids` by 14 vanilla liquid ids, each entry carrying the effect
  coefficients; the type's own comment says "Unknown liquids/items are deliberately refused by the
  host so an unsupported effect is never silently approximated." Its `Liquids` half and the whole
  `RemoteDrinkMedicineCatalog` beside it were **deleted 2026-10-08** by Part B's consume chain, and its
  `Food` half — the file itself, with `RemoteFoodEffect` and `RemoteConsumeApplication` — is **deleted
  2026-10-08** by the solid-food ticket's step 3, which read the family out of the item's own use action
  instead (`todo/mod-cross-player-solid-food-semantics.md`). This row is the record of what the
  hand-transcribed tables were, not a live inventory.
- `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteMedicineCatalog.cs` —
  `InjectionAmounts` (15 containers) plus `Liquids` (immediate, opiate and timed branches), with the
  comments naming the decompiled sources they were transcribed from (`Liquids.cs` `Drink`/`Inject`
  formulas and the `onHealthUse` branches). **Deleted 2026-10-08** by
  `docs/backlog/review/mod-cross-player-native-semantics.md` Part A, which is the reason this inventory
  can now name one chain as done.
- The same shape in `RemoteTopicalCatalog`, `RemoteLimbToolCatalog` and `RemoteWearCatalog`; the
  decisions that introduced the slices describe them as curated (`docs/decisions/archive.md` entries
  98, 100, 103). All three are **deleted 2026-10-08** by
  `docs/backlog/review/mod-cross-player-native-semantics.md`: `RemoteTopicalCatalog` by Part B, the
  same way the medicine table went with Part A; `RemoteWearCatalog` (the 40-row wearable registry) by
  its wear chain, which reads `ItemInfo.wearable` / `desiredWearLimb` / `wearSlotId` instead; and
  `RemoteLimbToolCatalog` (the ten-row tool registry with its transcribed condition costs, deltas and
  timed ramp) by its limb-tool chain, which reads `ItemInfo.usableOnLimb` + `useLimbAction` and runs the
  delegate on the treated player's own client. No cross-player chain carries an id table any more.

Consequences:

- Every cross-player chain answers from the game's own data now, so the ceiling they imposed is gone:
  a vanilla item the deleted tables never carried (or a game update moved a number inside) reaches
  another player with the game's own code. What mod content can still not declare is the SURFACE: the
  DTOs below cannot express a limb action or a liquid's effect delegate (the WEARABLE set landed
  2026-10-11 — see the Part 3.A entry below), so a mod item cannot ask for those chains to carry it.
- The ceiling of the mod platform is therefore our maintenance speed, not the game's own capability.
- The transcribed constants are a patch-stack debt against the root-cause rule in `AGENTS.md`: a game
  update that changes a formula moves the game and leaves our copy behind, silently. The five
  cross-player chains have paid that debt off: the injection, topical, drink, wear and limb-tool
  catalogs are deleted.
- The content DTOs still cannot declare what those chains would need: `ModItemDefinition` has no
  limb-use behaviour (the wearable half landed 2026-10-11,
  `docs/backlog/review/mod-item-wearable-declaration.md`), and `ModLiquidDefinition` has no drink/health
  delegates.
  (Qualities are no longer in this list — item and liquid qualities landed 2026-10-07 with stage 1,
  `review/mod-crafting-quality-labels.md`.)

### Direction (staged — the stage split is mandatory)

Each stage is its own deliverable with its own verification, and stage 1 must not be smuggled in as
preparation for stage 2.

**Stage 1 — content surface parity (small, self-contained; landed 2026-10-07 as
`review/mod-crafting-quality-labels.md`).** `ModItemDefinition.Qualities` decoded
into `ItemInfo.qualities`, so a mod item can satisfy a quality-based recipe (vanilla and mod-authored).
Add the liquid effect delegates and the limb-use behaviour only behind a real consumer (see *Open
questions*) — **the consumer gate is superseded 2026-10-08** (see Stage 3 below); what stays is the
requirement that no stage be smuggled in as another's preparation. Acceptance: a mod item satisfies a
quality recipe, and a recipe that references a quality nobody provides is reported at load time instead of
becoming a silently dead recipe.

**Stage 2 — semantic predicate plus target-local execution (its own architecture ticket — cut 2026-10-08
as `mod-cross-player-native-semantics.md`, whose Part A landed the injection chain and deleted the
catalog below; this paragraph is the pre-cut statement of the direction).** Replace the
id tables with predicates over the game's own data: the native eat/drink branch for solid food,
`LiquidType.onDrink`, `injectable` with a `WaterContainerItem`, `Stats.HasTag("dressing")` or a non-null
`useLimbAction`, `wearable` with `desiredWearLimb` / `wearSlotId`. The effect runs on the target's own
client through the native path; the host keeps only "is this operation allowed", resource consumption
and first-writer-wins arbitration. The precedent this paragraph named when it was written — the timed
medicine branches travelling as `TimedBodyEffectMsg` and running on the target's local body — is itself
gone: those branches are the drink chain's now and the field was deleted with it, because the liquid's
own `onDrink` delegate IS that timed body and needs no CUO message to start it. Migrate one chain first (injection or topical — medical is CUO's main line and
already has the operation session), then the remaining chains. Hard acceptance for the migration:
delete the constant table and every existing acceptance case of that chain stays green.

**Stage 3 — mod-registered semantics.** A registration surface for semantics the game's own data cannot
express (a mod-authored effect or medical interaction, for example). It needs a permission and consistency
rules, and a wire face only where the existing chains do not already carry what the effect needs. **Cut
2026-10-08 as `todo/mod-authored-effects.md`.** The paragraph's old trigger — "only if stage 2 leaves a real
need ... trigger is a second real consumer" — is **SUPERSEDED** by the user's 2026-10-08 ruling that a
highly customisable effect surface is the point of the platform, so the consumer count is no longer the
gate; the stage's own content is unchanged, and the successor ticket carries the supersession with the
ruling in the user's words.

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
- **`useLimbAction`** — apply to a limb (bandage, splint, tourniquet, amputation). The cross-player
  chain reads the game's own data for every vanilla limb action now, but the DTO still cannot declare
  one: a mod item has no limb-use behaviour, so it is carried, dropped and saved and never applied.
- **the wearable set** — `wearableArmor`, `wearableIsolation`, `wearableHitDurabilityLossMultiplier`,
  `desiredWearLimb`, `wearSlotId`, `wearableCanBeHeld`, `wearableVisualOffset`; the last two appeared in
  `src/` only as reads when this entry was written. **Cut and landed 2026-10-11 as
  `docs/backlog/review/mod-item-wearable-declaration.md`**, and the cut found the entry is not a gap but a
  defect: the declared `Wearable` flag alone reached `Body.WearWearable`, which resolves the limb name
  `desiredWearLimb` carries and dereferences the result
  (<c>Body.cs:1493-1494</c>) — so a mod item marked wearable threw inside the game's own wear flow, and the
  successor ticket carries the fix, the surface, the guard family over the four dereference sites and the
  correction to the sibling ticket's non-goal below.
- **miscellaneous** — `rec` (recognition), `onlyHoldInHands`, `combineable`, `ignoreDepression`,
  `scaleWeightWithCondition` (read-only in `CarriedEncumbranceCalculator`), `jumpHeightMultChange`,
  `slotRotation` (read-only in the clone renderer), `usableOnLimb`.

Liquid side (`LiquidType` versus `ModLiquidDefinition`):

- **`onDrink` / `onHealthUse`** — the effect delegates. `GameAdapterLiquidContentProvider` maps only the
  static fields, so a mod liquid can declare `HealthUsable` while no effect function exists. **Cut
  2026-10-08 as `review/mod-declared-behaviour-with-no-function.md`**: the native path does not null-check, so
  the gap is a crash rather than a missing feature (the open question below is answered there), and the
  family audit that cut it found the same shape on the item side — `ModItemDefinition.Usable` /
  `UsableWithLmb` with no `useAction`. Both halves are that ticket's cycle.

### B. Declaration versus materialization

The framework says a content kind exists, but nothing consumes it — the same silent shape the
quality-reference check closed for recipes in stage 1:

- `ModContentKind.Entity`, `ModContentKind.Setting` and `ModContentKind.Locale` had **no content
  provider** (the nine providers cover the other nine kinds). `Entity` appeared once in `src/`, as the
  console resource-location label of the built-in player entity; `Setting` and `Locale` did not appear at
  all. A registration for one of them was accepted and materialized nothing. **Cut and landed
  2026-10-07 as `review/mod-content-kind-with-no-provider.md`**: the vocabulary now names only the kinds a
  provider binds, the built-in resource entry carries its own kind word, the binder reports a
  provider-less registration at warning level, and a gate ties the vocabulary, the providers and the
  registration statements that name them into one list.
- A `ModRecipeIngredient.Quality` that no provider can satisfy is now refused and reported at load time
  instead of becoming a recipe that can never be crafted — **landed 2026-10-07** with stage 1
  (`review/mod-crafting-quality-labels.md`), which also records the two reach limits that stay open there
  (a LIQUID label's amount, and a definition whose id collides with a vanilla entry).

### C. Capability the game itself does not have

These need new capability rather than a predicate, and none has a consumer. None of them has a
`ModContentKind` constant either: the vocabulary names only the kinds a provider binds, so a
capability's kind joins it in the same change as the provider that binds it
(`review/mod-content-kind-with-no-provider.md`).

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

The 2026-09-25 ruling was that the whole finding waits in `future/`; the 2026-10-07 directive promoted it.
Each entry still becomes its own ticket with a real consumer named (the promotion funnel in
`docs/en/reference/modification-policy.md`) and is split into stages that each produce a verifiable result —
this ticket is the inventory and the plan, not one change that lands everything at once. An entry that needs
a wire face is promoted with its own protocol decision (`docs/backlog/review/mod-defined-wire-packets.md` is the
first one).

Two entries are first in line, because they are defects of a surface the API already promises rather than new
capability, so they carry a reachable failure path instead of a wish: **a content kind with no provider**, and
**a quality reference with no provider**. Both are filed at a higher priority than the capability entries when
their own tickets are cut. Both are now cut: `review/mod-crafting-quality-labels.md` (2026-10-07) and
`review/mod-content-kind-with-no-provider.md` (2026-10-07).

## Open questions

- Vanilla null-delegate behaviour: a mod liquid with `HealthUsable = true` and no `onHealthUse` — does the
  native path null-check or throw? **Answered 2026-10-08: it throws.** `WaterContainerItem.ApplyToLimb`,
  `WaterContainerItem.Inject` and `WaterContainerItem.Drink` all call their delegate with no null check, and
  CUO's own custom-byte drink handler did the same, so that half is a defect fix — cut with its item twin as
  `review/mod-declared-behaviour-with-no-function.md`.
- Stage 3 with mixed-mod sessions: does the existing handshake consistency (mod id, version, permissions,
  `NativeBinding` parity) cover "same content, different semantics"?
- Part 3's kind-versus-provider mapping — **answered 2026-10-07** by
  `review/mod-content-kind-with-no-provider.md`, and the answer is both halves taken together: the
  unimplemented kinds leave `ModContentKind` (so a mod cannot write them through the API at all), and the
  binder warns for any registration no provider binds (so a literal, hand-written kind is still answered
  at load time rather than in silence). Neither half alone closes the failure path.

## Non-goals

- Not an argument for widening the API now; it is the candidate list, not a plan.
- Not a Forge/Fabric-style ecosystem — that is `phase5-tooling-ecosystem.md`.
- Not the KrokMP migration surface — that is `cucorelib-migration-support.md`.
