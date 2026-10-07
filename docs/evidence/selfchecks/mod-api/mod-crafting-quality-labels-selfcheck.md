# Crafting-quality labels — mechanism inventory and self-check

Owner cycle: `review/mod-crafting-quality-labels.md` (2026-10-07), the stage-1 item of the mod content
ceiling's Part 2 (`todo/mod-content-ceiling.md`): a mod item could not declare a crafting quality, and a
quality reference that resolved to nothing was silent.

Decision: one label type for both surfaces (`ModLiquidQuality` renamed `ModCraftingQuality`), the item
provider writes the game's own `ItemInfo.qualities`, and the recipe provider refuses a quality reference
no provider in the ingredient's own direction can reach — the game's matcher is direction-selected and
amount-aware, so a check that was neither would inject dead recipes while claiming to prevent them.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | The matcher's real input | `RecipeItem.GetMatchingItem` matches `Item.GetQualityThatMeetsCriteria(this.quality, item.Stats.qualities)`; `Item.Stats` is `Item.GetItem(this.id)` — the `ItemInfo` the provider injects, so a mod item is matched like a vanilla one |
| 2 | The matcher is DIRECTION-SELECTED | an item ingredient is matched against `item.Stats.qualities`, a liquid one against `liquidType.GetScaledQualities(ml)` (`RecipeItem.GetMatchingItem`, repeated in `UseItem`); `DoesUseItemType` is the only presence-only consumer and its sole caller is the visible-list filter, so refusing a recipe cannot hide one the panel would have shown as craftable |
| 3 | The matcher is AMOUNT-AWARE | `q.amount >= target.amount`; item amounts are fixed, liquid amounts scale per millilitre; the recipe side normalises a non-positive requirement to 1, so declarations normalise the same way |
| 4 | Label grammar | a label is a bare vanilla-style token (`ContentId.IsValidNamespace`) or a canonical namespaced content id (`ContentId.TryParse` round-tripping to itself); the comparison is ordinal and the value is stored verbatim, so anything else is refused at bind rather than normalised |
| 5 | Where the vocabulary comes from | the labels an accepted mod definition declares (each provider keeps its own `CraftingQualityDeclarations`, with the largest amount per label) plus the same direction's vanilla table (`Item.GlobalItems` / `Liquids.Registry`) |
| 6 | Why the sources are consulted first | the pump order is not uniform: the item provider is registered before the recipe provider, the liquid provider after it, so a liquid bound in the same frame is still missing from `Liquids.Registry` — the tables alone would refuse a recipe that uses a label its sibling had just declared |
| 7 | Table readiness | `Item.GlobalItems` and `Recipes.recipes` are built in one native body, items first (`WorldGeneration.Awake`); `Liquids.Registry`'s only assignment is its own static constructor. Verified in the shipped assembly by IL: one `stsfld` site each |
| 8 | Null collections | `DataContractSerializer` runs no property initializer, so a null list round-trips as an explicit nil; the providers normalise it to empty instead of failing the definition (the remaining sites are filed as `todo/mod-payload-null-collection-tolerance.md`) |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModCraftingQuality` | renamed from `ModLiquidQuality`; one label DTO for items and liquids |
| `ModItemDefinition.Qualities` | new; same amount normalisation as the liquid side |
| `ModLiquidDefinition.Qualities` | retyped to the shared DTO |
| `CraftingQualityDeclarations` | new: the label rule plus what one provider declared, with amounts |
| `ICraftingQualitySource` | new: the kind a source answers for plus an amount-aware query |
| `GameAdapterItemContentProvider` | writes `info.qualities`, validates labels at bind, reports declarations, warns when a colliding id leaves a declared label unprovided |
| `GameAdapterLiquidContentProvider` | the same rule and report on the liquid surface |
| `GameAdapterRecipeContentProvider` | direction- and amount-aware refusal; null `Ingredients` normalised |
| `GameAdapterComposition` | two `ICraftingQualitySource` registrations; neither is an `ICuoService`, so the pump order is unchanged |
| Docs | mod API page in both blocks, terminology registry, pair registry, backward index, the umbrella's four now-stale statements |
| Tests | `CraftingQualityLabelTests` (11), `ModItemDefinitionTests` (2 new), `ModLiquidDefinitionTests` (rename) |
| Protocol version | unchanged (no wire) |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Item qualities reach the matcher | `info.qualities` written from the DTO, amount `<= 0` normalised to 1 | `CraftingQualityLabelTests.ItemQualities_SatisfyTheVanillaRecipeMatcher` drives the game's own `Item.GetQualityThatMeetsCriteria` |
| Label grammar enforced on both surfaces | 3 accepted / 8 refused forms on items; the liquid twin | `TryBind_RefusesAQualityLabelThatIsNotCanonical`, `LiquidQualities_AreValidatedAndMaterialized` |
| Direction awareness | an item-only label on a liquid ingredient and a liquid-only label on an item ingredient are both refused | `Recipe_IsRefusedWhenTheLabelComesFromTheOtherDirection` |
| Amount awareness | a label nothing declares at the required amount is refused, from a mod declaration and from the vanilla table | `Recipe_IsRefusedWhenNoProviderReachesItsAmount` |
| Source-first order | isolated twice: the mod item / mod liquid is bound but NEVER materialised, so only the declared sources can answer | `Recipe_IsInjectedFromAModDeclarationBeforeTheItemIsMaterialized`, `...AModLiquidDeclarationBeforeTheLiquidIsMaterialized` |
| Satisfiable recipes still land | vanilla item, vanilla liquid, and the colliding-id-free declared paths | `Recipe_IsInjectedWhenAVanillaItemProvidesItsQuality`, `...AVanillaLiquidProvidesItsQuality` |
| Null collections | a null list means none, not a failed definition | `TryBind_TreatsAnExplicitNullQualityListAsNoQualities`, `ModItemDefinitionTests.RoundTrip_ExplicitNullQualities_ComesBackAsNull` |
| Pump order unchanged | the two new registrations are not `ICuoService` | `GameAdapterComposition` diff + `ApiSurfaceGateTests`/`SourceShapeGateTests` green |
| Public surface recorded | rename removed with three tombstones, four lines added | `ApiSurfaceGateTests` 11/11 |
| Docs pair intact | both blocks edited, hashes re-recorded | `DocumentationTreeGateTests` (both blob hashes equal `git hash-object` of the pages) |

## 4. Verification design

- Behaviour tests are driven reflectively against the game assemblies (the test project never
  compile-references GameAdapter), and the strongest row calls the game's own matcher rather than
  re-reading the DTO.
- Six mutations, each expected to turn a named case red and each restored byte-identically (SHA-256).
- The load-time and client-local nature of every row is why the suite judges them; the rows that would
  need a running game are named in the ticket's *Limits*.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| Focused (`CraftingQualityLabel` + the two DTO suites) | 18 passed / 0 failed |
| `dotnet test CasualtiesUnknownOnline.slnx` (with build) | gates 451 passed / 0 failed, behaviour 4711 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | run once before the commit |
| Mutations | M1b sources answer the opposite → 4 cases red; M2 liquid direction-blind → 1 red; M3 source amount ignored → 1 red; M3b table amount ignored → 1 red; M4 label grammar ignored → 2 red; M5 null normalisation removed → 1 red; every file restored byte-identically |

Mutation notes, recorded because they cost time: the first M1 attempt (`foreach` over an empty array)
turned `_qualitySources` unread and failed the BUILD on IDE0052 — a build red is not a test red, so it was
replaced by the inverted-answer mutation. And restoring a mutated file with `File.Copy` carries the
BACKUP's older timestamp, which makes MSBuild consider the project up to date: the mutated binary stayed in
place and the next full run reported one failure that looked like a real defect. Every restored file is now
touched before the rebuild, and the suite was re-run to green afterwards.

## 6. Independent review

Adversarial review in a fresh context against the frozen tree: **no blocker, 4 major, 4 minor, 8 nits**.
All fixed in the same commit:

- the refusal was direction-blind and amount-blind (the two majors): fixed in the check and pinned by two
  new cases;
- no test isolated the source-first order: two isolating cases added;
- the umbrella inventory still said the capability did not exist: four statements reconciled;
- the colliding-id false acceptance was recorded nowhere a mod author can see: now a ticket limit, a
  user-page sentence and a runtime warning on both surfaces;
- the ticket named a type that does not exist (a mid-implementation rename): the design section now
  describes what landed;
- the liquid half was untested (and `Locale.LoadLanguage()` was why): the harness now installs a game
  `Language` object, and the liquid provider has real cases;
- both doc comments justified the source-first order with the wrong half: the load-bearing case (the liquid
  provider's registration position) is stated instead;
- the presence-only match: amounts are now compared in the item direction.

## 7. Limits

The ticket's *Limits* section is authoritative. In short: a LIQUID label's amount is not checked (it scales
with container volume); a definition whose id collides with an existing table entry is accepted but never
injected, so a label only it declares still yields a dead recipe (warned at injection, not prevented); a
mod label renders as `cq<label>` (no locale surface for quality labels); the vanilla table reads are
unguarded by design and rest on the initialization order verified above; `_failedKeys` is sticky per recipe
table generation; and nothing here was observed in a running game — `RecipeItem.GetMatchingItem` itself
needs live Unity objects, so row 1 drives the amount-aware matcher it calls.
