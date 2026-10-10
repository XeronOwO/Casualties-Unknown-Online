# The nested member contracts: the same seam, one level down

Cycle 2026-10-09 (second cycle of the day). Ticket
`docs/backlog/review/mod-content-nested-member-contracts.md` — the half stage B handed on. Decision 253;
decision 251 is the kind level above it and decision 252 is the scan that reads these members. Baseline
commit `acc21c2d`; this record describes the working tree that carries the change.

The question this cycle answers is the parent ticket's own sentence taken literally: **"the same rule
applies one level down to every member type a provider reads."** A kind contract was already an interface a
mod could implement; the values those contracts carry — a tool, a gun, a container, a sprite animation, a
crafting-quality label, a drop entry — were still the framework's `sealed` data classes, so a mod could
compute a nested value but could not hand over an implementation of the nested TYPE. It can now, and the
collection members follow, which is the part the ticket said would decide the shape.

## §1 Mechanism inventory

One row per touched mechanism, with the evidence that covers it.

| # | Mechanism | What changed | Evidence |
|---|---|---|---|
| 1 | The fourteen nested contracts | each declares exactly the members its data class already carried — 88 members in total: `IModItemTool` (16), `IModItemGun` (12), `IModItemContainer` (5), `IModItemBattery` (3), `IModItemLight` (15), `IModItemVisual` (9), `IModItemSpriteAnimation` (3), `IModItemLimbWornSprite` (4), `IModRecipeIngredient` (6), `IModCraftingQuality` (2), `IModBuildingDrop` (4), `IModTileDrop` (4), `IModMoodleAnimation` (3), `IModLimbMoodleBinding` (2) | `src/CasualtiesUnknownOnline.Abstractions/IMod*.cs`; `docs/contracts/abstractions-api-baseline.txt` (148 added entries) |
| 2 | The nine kind contracts | 18 member sites retyped: the item's `Container`, `Battery`, `Light`, `Tool`, `Gun`, `Visual` and `Qualities`, the item's `Visual`'s three frame animations and `MultiWornSprites`, the liquid's `Qualities`, the recipe's `Ingredients`, the building's `DropOnDestroy` and `AlwaysDrop`, the tile's `Drops`, the status's `LimbMoodles`, the moodle's `IconAnimation` | `IModItemDefinition.cs`, `IModItemVisual.cs`, `IModLiquidDefinition.cs`, `IModRecipeDefinition.cs`, `IModBuildingDefinition.cs`, `IModTileDefinition.cs`, `IModStatusDefinition.cs`, `IModMoodleDefinition.cs` |
| 3 | The fourteen data classes | each keeps every member, its defaults, its coalescing null write and its `sealed`/settable shape, and now implements the contract above it — so `new ModItemTool { … }` and a mod's own implementation are interchangeable everywhere | `ModItemTool.cs` … `ModLimbMoodleBinding.cs`; `ModNullCollectionRuleTests` still finds its 26 constructed members |
| 4 | The Game Adapter's consumers | 9 files: 8 signatures in `CustomItemBehaviorApplier`, 7 in `CustomItemBehaviorValidator`, 3 in `CraftingQualityDeclarations`, one each in `ModItemInfoFactory`, `GameAdapterBuildingContentProvider`, `GameAdapterTileContentProvider`, `GameAdapterMoodleContentProvider`, `ModStatusMoodleProjection` and `CustomBuildingTemplateFactory` | `src/CasualtiesUnknownOnline.GameAdapter/`; the build is what forced the list — every one was a compile error before it was a decision |
| 5 | The framework's read seam | the six nested collection members (a tool's `SwingSounds`, a container's `TagRestriction`, a visual's `MultiWornSprites`, the two animation contracts' and the two visual sites' `FramePaths`) are read through `ModDeclarationCollections` at all eight sites, and the tile provider clamps the rolled condition inline in place of the deleted `RollCondition` | `CustomItemBehaviorValidator.cs`, `CustomItemBehaviorApplier.cs`, `ModItemInfoFactory.cs`, `GameAdapterMoodleContentProvider.cs`, `ModStatusMoodleProjection.cs`, `GameAdapterTileContentProvider.cs`; `ItemAdvancedBehaviorProviderTests.Update_ANestedCollectionThatIsNull_ReadsAsNoneAndStillMaterializes` and `TryBind_ANestedAnimationWithoutFramePaths_IsRefusedNotThrown` |
| 6 | The example mod | its declared item's `Qualities` and its recipe's `Ingredients` are the interfaces now, so the production consumer of the attribute path is also a compile-time consumer of this change | `src/CasualtiesUnknownOnline.ModExample/ExampleMod.cs` |
| 7 | The test fixtures | 4 files: the two mod-authored fixtures that implement a kind contract now implement a NESTED one as well (`ModAuthoredTool`), the scanner fixture carries a nested implementation through the real scan, and the removed roll-rule test is gone with the method | `tests/CasualtiesUnknownOnline.Tests/Mods/ModContentDeclarationScannerTests.cs`, `Mods/TestDeclaredContentMod.cs`, `Mods/ModBuildingDefinitionTests.cs`, `Patching/ModAuthoredDefinitionBindingTests.cs` |
| 8 | The docs a mod author reads | the reference page gained the nested-contract paragraph and the interface name in two rows, and the how-to page gained a worked nested implementation; both blocks, hashes re-recorded | `docs/en/reference/mod-api.md`, `docs/zh/reference/mod-api.md`, `docs/en/how-to/register-content.md`, `docs/zh/how-to/register-content.md`; `docs/standard/alignment.txt` (2 pairs recomputed) |
| 9 | The reviewed API baseline | 148 added entries and 48 rewrites, no type removed: 14 interfaces, 14 class rows gaining their interface, 32 member rows changing return type, and the two `RollCondition` rows DELETED | `docs/contracts/abstractions-api-baseline.txt` (1115 → 1215 lines); `ApiSurfaceGateTests` re-derived the delta itself |

## §2 Whole-family audit

Four measurements drove the shape, each taken over the tree rather than assumed:

1. **The consumer census is what the ticket asked for, and it came back read-only.** Every use of a nested
   member under `src/` is a READ: no consumer calls a nested data class's own method, no consumer mutates a
   definition's collection, and none pattern-matches or casts a concrete nested type. That is why the
   interface sweep is mechanical — the compile errors it produced were the complete list of sites (23
   signature edits in 9 files), and not one of them needed a behaviour change. The nested types are small in
   number of use sites (12 files for `ModItemTool`, 13 for the most-referenced one, `ModItemVisual`), and
   most of those sites are tests.
2. **The collection members follow, and the deciding factor is that six of the fourteen are reachable
   ONLY through a collection.** `IModItemLimbWornSprite`, `IModRecipeIngredient`, `IModCraftingQuality`,
   `IModBuildingDrop`, `IModTileDrop` and `IModLimbMoodleBinding` have no single-valued member site at all
   (the other eight do: `IModItemTool`, `IModItemGun`, `IModItemContainer`, `IModItemBattery`,
   `IModItemLight`, `IModItemVisual`, `IModItemSpriteAnimation` and `IModMoodleAnimation`), so offering the
   contracts for single-valued members alone would define six interfaces no consumer could ever see — the
   opposite of the "a mechanism needs a named consumer" rule. The cost is a DTO type change, and it was
   measured before it was taken: 18 member sites, 23 consumer signature edits, 4 test files, and the
   null-collection census unchanged at 26 rows.
3. **`List<T>` was kept over `IReadOnlyList<T>`/covariance on the datasheet's own reason.** The data classes
   stay constructible with no arguments, fully settable and deserializable, which decision 251 records as the
   point of the shape; an interface that returned `IReadOnlyList<IModX>` would make a framework class's
   `List<ModX>` property an explicit interface implementation, and the concrete property a mod fills in
   would stop being the contract's member.
4. **The one member that was behaviour, not data, left the surface.** `RollCondition` had exactly one
   production caller (the tile provider) and the building-side method had none, so the interface question
   was decided by what the caller needed: the provider already VALIDATES the authored range at bind — non-NaN,
   inside 0..1, min <= max — and its own call site now clamps inline. The clamp is still load-bearing rather
   than dead: a declaration is stored as handed over and re-read at spawn, so a computed `IModTileDrop` can
   present values the bind validation never saw. Lifting the method onto `IModTileDrop` would have given a
   mod a veto over that rule, which is the shape decision 251 already rejected, so it is deleted, its unit
   test is deleted with it, and the arithmetic is byte-for-byte the same function.

## §3 What landed, what deliberately did not

**Landed.** The fourteen contracts and their 88 members; the 18 kind-contract member sites; the fourteen
data classes implementing them; the nine consumer files, with the nested null rule answered at all eight
read sites; the nested-implementation fixtures and the cases that drive them (the scan case, the extended
binding case, the two nested-null cases); the example mod; both documentation blocks with their alignment
hashes; the reviewed baseline; decision 253; the ticket moved to `review/` with its acceptance rows.

**Found by the independent review and fixed in this commit** (tier FULL; no blocker, 2 major, 4 minor, 4 nits).
The major that mattered was a real code defect, not a wording one: the four new contract docs promised the
null rule and no reader implemented it — the six nested collection members were dereferenced, so a
mod-authored nested object returning null would have thrown inside a validator, a provider's `Update`, the
moodle projection or the item's own `useAction`, exactly the failure `ModDeclarationCollections` exists to
prevent (it was unreachable before this change, because the framework's classes coalesce a null write). All
eight sites now read through the helper and two cases pin it. The second major was a wrong figure doing real
work: the count that justified the collection half said "seven of fourteen" in five places and the correct
number is six (the seventh, `IModItemSpriteAnimation`, has three single-valued sites). The minors were a
per-file edit count, a claim that the deleted clamp was dead code (it is load-bearing — a declaration is
re-read at spawn), a reference count whose scope was not stated, and one member listed as a collection entry
in both how-to pages. The nits were a contradictory sentence in one contract doc, a dangling citation in a
historical record, two log assertions in the new scanner case that pin nothing the count does not, and the
loose phrase "the nine kind contracts" for a total that includes `IModItemVisual`'s own four members.

**Deliberately did not.**

- **No default interface members and no base interfaces.** `IModItemVisual`'s three animation members and
  the item/moodle animation contracts are structurally identical and stay separate: they are two features,
  and one shared `ISpriteAnimation` base would be a fifteenth type serving a three-member duplication that
  today has two consumers with their own validation rules. A base is cheap to add when a third consumer
  appears; it is not free to remove once it is a contract.
- **No renaming.** The data classes keep their names (`ModItemTool` next to `IModItemTool`), as the nine kind
  DTOs already do.
- **No clone or snapshot.** The framework still stores the declaration and its nested objects exactly as
  handed over, which is what lets a computed nested value be re-read.
- **No fingerprint work.** `SaveManifest.ContentFingerprint` and the two-peers comparison were left to
  `docs/backlog/review/mod-content-fingerprint.md` (landed 2026-10-10, decision 254).
- **No `ModContentContract` change.** The one home of "which contract means which kind" is untouched: these
  contracts are read through the kind contract that owns them, and the scan's member sweep already reads
  them without knowing their types.

## §4 Verification

| Layer | What it proves | Result |
|---|---|---|
| Build | the contracts, their implementations and every consumer compile with warnings as errors | `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors |
| Format | the tree is the formatter's own output | `dotnet format CasualtiesUnknownOnline.slnx` — exit 0; every new C# file CRLF afterwards, measured byte-wise (CR count == LF count on all 14) |
| Focused suite | the nested seam, its scanner, its binder and the null rule around it | 56/56 over 6 filter conditions: `ModContentDeclarationScannerTests`, `ModContentDeclarationTests`, `ModAuthoredDefinitionBindingTests`, `ModNullCollectionRuleTests`, `ItemAdvancedBehaviorProviderTests`, `ModContentNullCollectionBindingTests` |
| Behaviour suite | nothing else moved | 4880/4880 (was 4878: the nested-member case, the two nested-null cases and the refusal case are added, the roll-rule case is removed with its method) |
| **The nested seam, through the real scan** | a declaration whose member is the mod's own implementation of a nested contract is read and registered, and the registered object still carries that implementation | `ModContentDeclarationScannerTests.Register_ReadsANestedMemberWhoseImplementationIsTheModsOwn` |
| **The nested seam, through the real provider** | the framework's data class carrying a mod-authored nested tool still binds; the registered definition exposes the mod's tool by identity, with its computed member | `ModAuthoredDefinitionBindingTests.Item_TheAcceptedRegistrationExposesTheComputedMembersNotTheDelegatedDefault` (extended) |
| **The nested null rule** | a nested object that returns null for a collection it does not carry is read as "none": the item binds and materializes with its `useAction` installed, and where "none" is itself invalid to the provider (an animation with no frames) the definition is refused with the provider's own reason instead of throwing out of a validator | `ItemAdvancedBehaviorProviderTests.Update_ANestedCollectionThatIsNull_ReadsAsNoneAndStillMaterializes` and `TryBind_ANestedAnimationWithoutFramePaths_IsRefusedNotThrown` |
| The contract's shape | the public surface is exactly what was reviewed | `ApiSurfaceGateTests` — red with 148 additions and 48 rewrites, green after the baseline was reviewed and updated; no type removed |
| The docs' pair record | both blocks changed together and the hashes are the files' own | `git hash-object` on the four pages; `docs/standard/alignment.txt` re-recorded |
| The whole gate set | nothing else in the repository's rules moved | 573/573 on the frozen tree, the delivery checklist excepted until this cycle's boxes are filled |

## §5 Limits

- **No game process ran.** The runtime evidence is the bind chain over the real providers plus the scan
  chain over the real registry; that a mod-authored nested tool reaches the GAME's `ItemInfo.useAction` and
  produces the authored attack is the same shape the flat behaviour test already covers and stays an
  acceptance row.
- **The nested implementation is read at bind time, not exercised in combat.** `UseTool` receives the
  interface, which the compiler proves; what a swing does at runtime is unchanged from the flat test.
- **`IReadOnlyList` was not tried in code.** The covariance argument in §2.3 is a language-level one
  (interface implementation requires an exact return type) and was not measured by attempting the change.
- **The collection change reaches third-party mods.** A mod that hands back a `List<ModCraftingQuality>`
  where the contract now wants `List<IModCraftingQuality>` fails to compile until the type argument is
  spelled as the interface. There is no compatibility shim by design (the project is pre-release), and both
  documented shapes are updated.
