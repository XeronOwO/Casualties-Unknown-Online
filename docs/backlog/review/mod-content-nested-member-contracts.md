# A declaration's nested member types are not contracts either

- Status: Review — **landed 2026-10-09** (see *What landed*): nothing is left to develop here, and the
  acceptance batch is pending. It was split out of `docs/backlog/review/mod-content-attribute-declarations.md`
  when that ticket's stage B landed: that ticket's settled shape names these contracts ("the same rule applies
  one level down to every member type a provider reads"), so stage A delivered the nine KIND interfaces and
  this cycle delivered the fourteen nested ones.
- Priority: Medium
- Category: Mod platform / mod API
- Related: `docs/backlog/review/mod-content-attribute-declarations.md` (the kind-level contract this completes
  one level down), `docs/backlog/review/mod-content-typed-registration.md` (the typed definition this hangs
  off), `docs/en/reference/modification-policy.md` (every added surface is a reviewed baseline change),
  decision 253 (the two calls this ticket left open).

## What was missing

The named member types — `IModItemTool`, `IModItemGun`, `IModItemContainer`, `IModItemBattery`,
`IModItemLight`, `IModItemVisual`, `IModItemSpriteAnimation`, `IModItemLimbWornSprite`,
`IModRecipeIngredient`, `IModCraftingQuality`, `IModBuildingDrop`, `IModTileDrop`, `IModMoodleAnimation`,
`IModLimbMoodleBinding` — did not exist: `src/CasualtiesUnknownOnline.Abstractions` carried the concrete
classes only, and the kind contracts named them (`ModItemTool? Tool`, `List<ModRecipeIngredient> Ingredients`).
A mod could therefore COMPUTE a nested value (build a framework DTO in the getter) but could not hand over an
implementation of the nested TYPE.

## What landed (2026-10-09)

Fourteen nested contracts, each declaring exactly the members its framework data class already carried, and
every nested member of the nine kind contracts typed as them: the item's `Tool`, `Gun`, `Container`,
`Battery`, `Light` and `Visual` (with the three frame animations and the limb worn sprites inside it), the
item's and liquid's `Qualities`, the recipe's `Ingredients`, the building's `DropOnDestroy`/`AlwaysDrop`, the
tile's `Drops`, the moodle's `IconAnimation` and the status's `LimbMoodles`. A mod implements a nested contract
the same way it implements a kind contract — composition, never inheritance from the `sealed` data class —
and every consumer (the nine providers, the item behaviour validator and applier, the item info and building
template factories, the crafting-quality helper, the moodle projection) now takes the interface.

Both calls the ticket left to the consumer census were taken as decision 253 records. The COLLECTION members
followed the single-valued ones instead of staying concrete: the parent ticket's settled shape reaches every
member type a provider reads, and six of the fourteen have no single-valued member site at all, so a
single-valued-only delivery would have defined six contracts no consumer could ever reach. The cost is a DTO
type change, contained because every consumer only READS these members, and `List<T>` was kept over
`IReadOnlyList<T>`/covariance because the data classes stay settable plain data a future loader deserializes
into (decision 251's own reason). The one member that is BEHAVIOUR rather than data, `RollCondition` on the
two drop classes, was deleted rather than lifted onto the new interface: its only remaining caller clamps
inline after the bind-time validation of the authored range, so it had no consumer on the interface, and
lifting it would have handed a mod a veto over a framework rule.

Evidence: `docs/evidence/selfchecks/mod-api/mod-content-nested-member-contracts-selfcheck.md`; the reviewed
baseline delta (148 added entries, 48 rewrites, the two method removals named) is in
`docs/contracts/abstractions-api-baseline.txt`.

## Acceptance (moved here verbatim from the parent ticket, judged at the acceptance batch)

- **A mod-authored nested implementation (its own `IModItemTool`, say) materializes with that tool's values:
  the seam is the interface, not the framework's class.**
- The same declaration registered through code, as a framework data class with a nested implementation in one
  of its behaviour members, still materializes — one provider, two ways to feed it.
- A nested member that returns null where the member is single-valued is read as "not carried" by every
  consumer, exactly as the framework's own data class defaults are.
