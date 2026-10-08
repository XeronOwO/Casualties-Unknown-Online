# Crafting-quality labels: the item surface and the load-time reference check

- Status: Review — the development half landed 2026-10-07 (see *What landed*); every acceptance row below is
  judged by this repository's own suite, because all of them are load-time and client-local. The rows that
  would need the game running are named in *Limits*.
- Priority: High
- Category: Mod platform / content surface
- Source: cut from `todo/mod-content-ceiling.md` Part 2 **stage 1** ("content surface parity, small,
  self-contained"), which is the plan this ticket executes; the umbrella keeps the inventory and the later
  stages. Its two acceptance rows come from that stage.
- Related: `todo/mod-content-ceiling.md` (the plan and the inventory: Part 1 is the label surface, this is
  Part 2 stage 1, Part 3 B is the second row's sibling), `docs/en/reference/mod-api.md` +
  `docs/zh/reference/mod-api.md` (the user-facing rule),
  `docs/contracts/abstractions-api-baseline.txt` (the rename is recorded there with tombstones)

## What was asked

Two halves of one surface, both measured from the umbrella's own inventory:

1. **A mod item could not declare a crafting quality.** `ModLiquidDefinition.Qualities` mapped into
   `LiquidType.qualities`, but `ModItemDefinition` had no qualities at all and
   `ModItemInfoFactory.Build` (then `GameAdapterItemContentProvider.BuildItemInfo`) wrote no `info.qualities`, so a mod item could never
   satisfy a quality-based recipe — neither a mod-authored one nor one of the 169 vanilla ingredient
   entries that carry a quality.
2. **A quality reference that resolved to nothing was silent.** `GameAdapterRecipeContentProvider` built
   `RecipeItem.quality` straight from `ModRecipeIngredient.Quality` and checked only the *specific*
   item/liquid references (`IsKnownReference`), so a mistyped or unprovided label became a recipe that
   could never be crafted and said nothing about it.

## Mechanism (read before implementing)

| Fact | Evidence |
|---|---|
| The matcher reads the ITEM TABLE entry, not a per-instance copy | `RecipeItem.GetMatchingItem` matches `Item.GetQualityThatMeetsCriteria(this.quality, item.Stats.qualities)`, and `Item.Stats` is `Item.GetItem(this.id)` — the `ItemInfo` the provider injects |
| The match is DIRECTION-SELECTED | an item ingredient is matched against `item.Stats.qualities`, a liquid one against `liquidType.GetScaledQualities(ml)`; `RecipeItem.UseItem` repeats the split, and the vanilla vocabularies do not overlap (12 item-only labels, 6 liquid-only, and `Recipes.SetUpRecipes` has liquid-direction quality ingredients of its own) |
| The match is AMOUNT-AWARE | the same matcher requires `q.amount >= target.amount`; item amounts are fixed, liquid amounts are scaled per millilitre, and the recipe side normalises a non-positive requirement to `1` |
| The comparison is ordinal and case-sensitive | `Item.GetQualityThatMeetsCriteria` / `HasCommonQuality` filter on `q.id == target.id`, so a label that differs in case or padding can never match, and it is stored verbatim |
| Every craftability decision goes through that one predicate | `Recipe.GetItemsForRecipe` → `TryMake` → `PlayerCamera.TryCraft`, and the panel's craftable flag and per-ingredient marks read the same call; the only presence-only consumer is `RecipeItem.DoesUseItemType`, whose sole caller is the visible-list filter, and it is looser than the craft path in the same direction — so refusing a recipe cannot hide one the panel would have shown as craftable |
| A mod label has no locale entry, and that is cosmetic | `CraftingQuality.LocaleName` is `Locale.GetOther("cq" + id)`; `Locale.GetString` returns the key when the dictionary has no entry and never indexes it, so a mod label renders as `cqmymod:material` rather than throwing |
| Both label sides already existed in the CUO API | `ModLiquidDefinition.Qualities` → `CraftingQuality` in the liquid provider; `ModRecipeIngredient.Quality` / `QualityAmount` → `RecipeItem.quality` with `ignoredId` and the repair rule mirrored |
| The item vocabulary is readable whenever a recipe is built | `WorldGeneration.Awake` calls `Item.SetupItems()` and then `Recipes.SetUpRecipes()` in one body, and `Recipes.recipes`' only assignment is inside that setup (confirmed against the shipped assembly's IL: one `stsfld` site each) |
| `Liquids.Registry` is never null once it is read | its only assignment is the type's own static constructor, so the read that would test it has already run that constructor |
| The two vocabularies have no shared registry | `ItemInfo.tags` and `CraftingQuality` are separate namespaces (umbrella Part 1); the vanilla quality vocabulary is only ever the union of the qualities on `Item.GlobalItems` and `Liquids.Registry` |

## Design (as landed)

1. **One label type for both surfaces.** `ModLiquidQuality` was renamed `ModCraftingQuality` — the
   umbrella's Part 1 decision is that unification belongs in the CUO mod API, not in the game data, and two
   identical DTOs for one label surface is the patch stack that decision forbids. `ModItemDefinition.Qualities`
   takes the same type and the same amount normalisation as the liquid side.
2. **The item mapping stays the only writer of `ItemInfo`.** `ModItemInfoFactory.Build` fills `info.qualities`, so the
   vanilla matcher finds a mod item exactly as it finds a vanilla one; `Item.Stats` needs no new path.
3. **A declared label is either a vanilla label or a namespaced one.** Part 1 decision 2 (namespace
   mod-authored ids, reusing the content-id grammar, "a convention, not architecture") became a checked
   rule: a declared quality id is accepted when it is a bare vanilla-style token (`[a-z][a-z0-9_]*`, which is
   how a mod says "this content provides the vanilla `rippable`") or a canonical namespaced content id
   (`mymod:material`), and the definition is refused with a warning otherwise. Canonical matters because the
   value is stored verbatim and compared ordinally. Membership in the *vanilla* vocabulary is not decided
   here: the item table need not exist when mods bind.
4. **The reference check is the recipe provider's, and it is direction- and amount-aware.** A
   GameAdapter `ICraftingQualitySource` is what a provider uses to report the labels its accepted
   definitions declare (kept in a `CraftingQualityDeclarations` per provider, with each label's largest
   amount); the recipe provider asks the sources in the ingredient's OWN direction first and then the same
   direction's vanilla table. A label no provider in that direction can reach, or one nothing declares at
   the required amount, is refused with a warning naming the recipe, the label, the amount and the
   direction — the same warn-and-skip shape `IsKnownReference` already uses — so a dead recipe is never
   injected. The sources are consulted rather than the tables alone because the pump order is not uniform:
   the item provider runs before the recipe provider (its items are already in the table that frame) while
   the liquid provider runs after it, so a liquid bound in the same frame is missing from
   `Liquids.Registry` and the tables alone would refuse a recipe that uses the label its sibling had just
   declared. For a LIQUID ingredient only presence is asked for, because its amounts are scaled by the
   volume in a container.
5. **A null list means "none".** `ToPayload()` writes every member, but a mod that assigns null round-trips
   an explicit nil (the serializer runs neither a constructor nor a property initializer), so the two
   providers and the recipe provider normalise a null collection before use instead of failing the
   definition with a logged exception. [The rule now belongs to the contracts themselves — the member's
   setter plus the shared decode step — and the remaining sites were swept by
   `review/mod-payload-null-collection-tolerance.md` (decision 244); this paragraph records what this cycle
   landed.]
6. **The probe is the game's own matcher.** Acceptance row 1 is judged by driving
   `Item.GetQualityThatMeetsCriteria` — the call `RecipeItem.GetMatchingItem` makes — with the built
   `ItemInfo.qualities`, not by re-reading the DTO.

## What landed

- `src/CasualtiesUnknownOnline.Abstractions/`: `ModLiquidQuality.cs` renamed to `ModCraftingQuality.cs`
  (one label type, `Kind`-agnostic), `ModItemDefinition.Qualities` added, `ModLiquidDefinition.Qualities`
  retyped.
- `src/CasualtiesUnknownOnline.GameAdapter/Content/`: `CraftingQualityDeclarations` (the label rule plus
  what one provider declared, with amounts), `ICraftingQualitySource` (kind + amount-aware query),
  `GameAdapterItemContentProvider` (writes `info.qualities`, validates labels at bind, reports its
  declarations, and warns when a colliding id leaves a declared label unprovided),
  `GameAdapterLiquidContentProvider` (the same rule and report on the liquid surface),
  `GameAdapterRecipeContentProvider` (direction- and amount-aware refusal).
- `src/CasualtiesUnknownOnline.GameAdapter/GameAdapterComposition.cs`: both providers registered as
  `ICraftingQualitySource`; neither registration is an `ICuoService`, so the pump order is unchanged.
- `docs/`: the mod API page in both blocks, the terminology registry, the pair registry, the backward index,
  and the umbrella's now-stale statements about this surface.
- Tests: `CraftingQualityLabelTests` (new, item + liquid + recipe, driven reflectively),
  `ModItemDefinitionTests` (qualities round-trip, and the explicit-nil probe), `ModLiquidDefinitionTests`
  (the rename).

## Independent review

An adversarial review in a fresh context ran against the frozen tree before the commit: no blocker, 4 major,
4 minor, 8 nits. Every finding was fixed in the same commit — the direction- and amount-blind refusal (both
fixed in the check and pinned by new tests), a test that described the source-first order without isolating
it, the umbrella statements this ticket made false, the colliding-id limit being recorded nowhere a mod
author can see, the ticket naming a type that does not exist, the untested liquid half, the wrong stated
reason for the source-first order, and the presence-only match.

## Acceptance

| # | Row | Judged by | Verdict |
|---|---|---|---|
| 1 | A mod item satisfies a quality recipe | `CraftingQualityLabelTests.ItemQualities_SatisfyTheVanillaRecipeMatcher` drives the game's own `Item.GetQualityThatMeetsCriteria` over the injected `ItemInfo.qualities` | Verified 2026-10-07 |
| 2 | A recipe that references a quality nobody provides is refused at load time | `Recipe_IsRefusedWhenNoProviderInItsOwnDirectionCarriesTheLabel` (absent label), `Recipe_IsRefusedWhenTheLabelComesFromTheOtherDirection` (both directions), `Recipe_IsRefusedWhenNoProviderReachesItsAmount` (mod and vanilla amounts) | Verified 2026-10-07 |
| 3 | A recipe that can be satisfied is still injected | `Recipe_IsInjectedWhenAVanillaItemProvidesItsQuality`, `Recipe_IsInjectedWhenAVanillaLiquidProvidesItsQuality`, `Recipe_IsInjectedFromAModDeclarationBeforeTheItemIsMaterialized`, `Recipe_IsInjectedFromAModLiquidDeclarationBeforeTheLiquidIsMaterialized` — the last two isolate the declared-source path by never materialising the definition | Verified 2026-10-07 |
| 4 | A declared label that is neither a vanilla-style token nor a canonical namespaced id is refused on BOTH surfaces | `TryBind_RefusesAQualityLabelThatIsNotCanonical` (3 accepted, 8 refused forms) and `LiquidQualities_AreValidatedAndMaterialized` | Verified 2026-10-07 |
| 5 | A definition whose lists are explicit nulls binds as "no qualities" / is refused as "no ingredients" | `TryBind_TreatsAnExplicitNullQualityListAsNoQualities` + `ModItemDefinitionTests.ExplicitNullQualities_IsNoneNotAFailedDefinition` | Verified 2026-10-07 |
| 6 | The liquid surface keeps its materialization behaviour after the rename | `LiquidQualities_AreValidatedAndMaterialized` (the liquid is injected and its labels reach `LiquidType.qualities`) + `ModLiquidDefinitionTests` | Verified 2026-10-07 |

## Non-goals

- Not the liquid effect delegates, the limb-use behaviour or the wearable set (umbrella Part 3 A) — each
  needs a real consumer, as the umbrella's *Open questions* say.
- Not stage 2 (predicates plus target-local execution) and not stage 3 (mod-registered semantics): this
  stage decides nothing about who executes an effect.
- Not the parked entries of Part 3 B beyond the quality reference: a content kind with no provider keeps its
  own ticket.

## Limits

- **A LIQUID label's amount is not checked.** Only presence is asked for, because a liquid's quality
  amounts are scaled by the volume in a container (`GetScaledQualities`), so reachability depends on
  container capacity and on the liquid being placeable at all — both game-balance facts this check cannot
  see. An item label's amount IS checked, because item amounts are fixed.
- **A definition whose id collides with an entry already in the game table** is accepted and declared but
  never injected (`Item.GlobalItems` / `Liquids.Registry`), so a label only it declares is counted as
  provided and a recipe requiring it is injected anyway — a dead recipe in the one case where the mod is
  already broken and already warned about (both providers now log that the declared label is not provided).
  The complete fix is declaring at materialization instead of at bind, which needs a pending state and a
  give-up rule to avoid deferring forever; it is deliberately not attempted here.
- **A mod-authored label renders as `cq<label>`** in the recipe panel and the item tooltip: there is no
  locale surface for a quality label (locale is a parked capability, umbrella Part 3 C). It is cosmetic —
  the lookup returns the key and never throws.
- **The declared-label check is a shape check, not a vocabulary check:** a bare id that no vanilla item
  carries is accepted at bind, because the item table need not exist yet. The recipe side is where a label
  that resolves to nothing is caught.
- **Two mods declaring the same namespaced label are not reported**; the label is then provided by both,
  which is the same behaviour as two mods providing the same vanilla label.
- **The vanilla table reads are unguarded by design.** `Item.GlobalItems` is read without a null check
  because `Recipes.recipes` and it are built in one native body, items first (verified in the shipped
  assembly's IL), and `Liquids.Registry` because its only assignment is its own static constructor. The
  sibling `IsKnownReference` keeps its own "not ready yet" guards; that asymmetry is deliberate and this
  ticket does not change its meaning. A game update that reorders that initialization would throw instead
  of silently accepting, which is the failure direction chosen here.
- **`_failedKeys` is sticky per recipe-table generation**, so a refusal lasts until the table is rebuilt.
  The readiness argument above is what keeps a false refusal out; it is not defended a second time at
  runtime.
- **Nothing here is judged in a real session.** Every row is a load-time, client-local behaviour and is
  judged by the suite; live injection into `Item.GlobalItems`, the panel drawing `cq<label>` and the
  craft-deny sound are reasoned from decompiled source and IL, not observed.
- **`RecipeItem.GetMatchingItem` itself is not driven end to end** — it needs live Unity `Item` /
  `WaterContainerItem` instances. Row 1 drives the amount-aware matcher it calls.
