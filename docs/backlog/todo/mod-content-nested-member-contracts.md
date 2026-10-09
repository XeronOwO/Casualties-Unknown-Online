# A declaration's nested member types are not contracts either

- Status: Todo — split out of `docs/backlog/review/mod-content-attribute-declarations.md` when that ticket's
  stage B landed (2026-10-09). That ticket's settled shape names these contracts ("the same rule applies one
  level down to every member type a provider reads"); stage A delivered the nine KIND interfaces only.
- Priority: Medium
- Category: Mod platform / mod API
- Related: `docs/backlog/review/mod-content-attribute-declarations.md` (the kind-level contract this would
  complete one level down), `docs/backlog/review/mod-content-typed-registration.md` (the typed definition
  this hangs off), `docs/en/reference/modification-policy.md` (every added surface is a reviewed baseline
  change).

## What is missing

The named member types — `IModItemTool`, `IModItemGun`, `IModItemContainer`, `IModItemBattery`,
`IModItemLight`, `IModItemVisual`, `IModItemSpriteAnimation`, `IModItemLimbWornSprite`,
`IModRecipeIngredient`, `IModCraftingQuality`, `IModBuildingDrop`, `IModTileDrop`, `IModMoodleAnimation`,
`IModLimbMoodleBinding` — do not exist: `src/CasualtiesUnknownOnline.Abstractions` carries the concrete
classes only, and the kind contracts name them (`ModItemTool? Tool`, `List<ModRecipeIngredient> Ingredients`).
A mod can therefore COMPUTE a nested value (build a framework DTO in the getter) but cannot hand over an
implementation of the nested TYPE.

## What this ticket owes

- The acceptance row moved here verbatim from the parent ticket: **A mod-authored nested implementation (its
  own `IModItemTool`, say) materializes with that tool's values: the seam is the interface, not the
  framework's class.**
- The collection members are the harder half and decide the shape: `IModRecipeDefinition.Ingredients` is
  `List<ModRecipeIngredient>` and `IModItemDefinition.Qualities` is `List<ModCraftingQuality>`, and generic
  invariance means a `List<IModRecipeIngredient>` cannot accept the concrete list — so either the member
  types become interface collections (a breaking DTO change that reaches every consumer, the
  `ModNullCollectionRuleTests` census and the fluid/tile/building drop lists), or the nested contracts are
  offered for the single-valued members only, with the collection case answered as composition. The ticket
  decides with the consumer census in hand, the way stage A decided the same question for the kind level.
