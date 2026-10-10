# Independent adversarial review: the nested member contracts

Review cycle 2026-10-09 (second cycle of the day). Tier **FULL**, fresh context (a `subagent`, not a fork of
the working conversation), against the FROZEN working tree at baseline commit `acc21c2d` — the reviewer was
told not to modify any file and did not (the tree's 70 entries were identical before and after). Subject:
`docs/evidence/selfchecks/mod-api/mod-content-nested-member-contracts-selfcheck.md`, decision 253, ticket
`docs/backlog/review/mod-content-nested-member-contracts.md`.

**Verdict: no blocker — 2 major, 4 minor, 4 nit, every one dispositioned in the same commit.**

The reviewer's interim channel to the parent was not routable, so its single delivery is quoted in full in the
session record; what follows is the finding-by-finding account and disposition.

## Findings and dispositions

| # | Severity | Finding | Disposition |
|---|---|---|---|
| F1 | major | **A real code defect, not a wording one.** Four of the new contract docs stated the null rule ("read through `ModDeclarationCollections`: null means …") while NO reader implemented it: the six nested collection members (`IModItemTool.SwingSounds`, `IModItemContainer.TagRestriction`, `IModItemVisual.MultiWornSprites`, both animation contracts' `FramePaths`, and the visual's three animation sites) were dereferenced at eight sites across `CustomItemBehaviorApplier`, `CustomItemBehaviorValidator`, `ModItemInfoFactory`, `GameAdapterMoodleContentProvider` and `ModStatusMoodleProjection`. The shape was unreachable before this change because the framework's classes coalesce a null write, and the throws would land outside the binder's per-entry catch — in a validator, a provider's `Update`, the moodle projection, or the item's own `useAction`. The claimed coverage did not cover it: `ModContentNullCollectionBindingTests`'s null writes hit settable framework classes, whose setters rewrite null to empty. | **Fixed in the code**: all eight read sites now go through `ModDeclarationCollections.OrEmpty`, and two cases pin it through the real provider and the game's own item table — `ItemAdvancedBehaviorProviderTests.Update_ANestedCollectionThatIsNull_ReadsAsNoneAndStillMaterializes` (null tool sounds, null tag restriction, null multi-worn sprites: the item binds and materializes with its action installed) and `TryBind_ANestedAnimationWithoutFramePaths_IsRefusedNotThrown` (null frame paths are "no frames", so the provider refuses the definition with ITS own rule instead of throwing out of a validator). The delivery checklist's verification row was reworded to name the real coverage. |
| F2 | major | **The number the collection decision rests on was wrong in five places.** The self-check, the MANIFEST, two checklist boxes, decision 253 and the ticket said "seven of the fourteen are only reachable through a collection" (decision 253 and the ticket said "three" in the same sentence as "half the family"). The correct figure is SIX: `IModItemSpriteAnimation` has three single-valued sites inside `IModItemVisual`. | Corrected to six everywhere, with the enumeration stated where the figure does work (the eight interfaces that do have single-valued sites are named). The 18 member sites / 10 single-valued / 8 collection split was already right and is unchanged. |
| F3 | minor | "24 consumer edits, 8 in `CustomItemBehaviorApplier`" is one too high: the diff carries seven signature edits in that file (the eighth edit I had counted is the inline clamp in the tile provider, a different file), so the total is 23. | Corrected to 23 in the self-check, the MANIFEST and the checklist; the per-file counts are now stated so the figure can be re-derived from the diff. |
| F4 | minor | "Its clamping was dead defensive code" is not established. The route question is answered correctly (validation precedes every route into `TrySpawnDrops`), but the VALUES are not the validated ones: a declaration is stored as handed over and re-read at spawn, which the self-check's own §3 states, so a computed or mutated drop could present values the bind validation never saw. | Reworded in decision 253, the self-check §2.4 and the ticket: the clamp is load-bearing for a computed nested value, not dead. The code is unchanged — the inline replacement is arithmetically identical, `max < min` normalisation included. |
| F5 | minor | "40 references across 12 files, most of them tests" reproduces in no single scope: 40 is the whole-repo figure (17 files, 24 of the hits in `docs/`), while the consumer scope the sentence is about is 16 occurrences over 12 files, and `ModItemTool` is not even the largest type there. | Rewritten as a scoped fact: 12 files for `ModItemTool`, 13 for the most-referenced type, `ModItemVisual`. |
| F6 | minor | Both how-to pages listed a SINGLE-valued member among "the entries of a collection" (`MoodleAnimation` frames) and omitted the real collection member `MultiWornSprites`. | Both pages corrected in the same change, so the pair record stays valid. |
| F7 | nit | `IModItemContainer` contradicted itself about null versus empty ("Empty means every item is accepted" on the member, "null means …" on the type). | Member doc now says "Null or empty means every item is accepted", and the type doc states that the framework's own implementation cannot produce null because a null write coalesces. |
| F8 | nit | A `historical` record (`mod-building-drop-worldgen-selfcheck.md`) still cited the deleted `RollCondition` and its deleted test. | The row now records the deletion, its date, the decision and the surviving call site. The record's own status was already honest; only the citation dangled. |
| F9 | nit | The new scanner case's second log assertion can essentially never fire — nothing in the scan logs the nested implementation's type name. | The assertion is dropped; the one that names the declaration stays, and the count/lookup already fail on any refusal. |
| F10 | nit | Two unchecked checklist boxes still carried the PREVIOUS cycle's numbers (`was 4854`, focused 153/153, three ADDED entries), and "the eighteen sites in the nine kind contracts" was loose, because four of them are `IModItemVisual`'s own. | Both boxes were rewritten when this cycle checked them, with this cycle's numbers; the mechanism rows now say "the eighteen member sites in the kind contracts plus `IModItemVisual`'s own four". |

## What the reviewer could not falsify

- **Member parity.** Member-by-member comparison of each new interface against its data class at HEAD: 84 of
  88 members are identical in name and type. The four that differ are `IModItemVisual`'s own nested-typed
  members, which is the change's subject and is compiler-enforced by `: IModItemVisual`. No initializer, `set`
  accessor or `sealed` modifier moved; all 16 coalescing setters survive.
- **Read-only consumers.** No `is`/`as`/`OfType`/`Cast`/`typeof()` on any of the fourteen concrete classes
  anywhere under `src/`; no assignment to a definition member anywhere under `src/`; the only collection
  mutations under `src/` are runtime pending-break state, never a declaration.
- **The route into `TrySpawnDrops`.** `_definitions` has exactly one writer and it is guarded by
  `TryValidateDefinition`; the spawn path reads only that dictionary. (The values, not the route, are what
  F4 corrects.)
- **The baseline decomposition.** 14 new interface rows + 88 new member rows + 14 rewritten type rows + 32
  rewritten member rows = 148 added; 14 + 32 + 2 = 48 removed; every removed `type|` row has its replacement.
- **The two new test bodies** pin what they claim, and the docs' C# excerpts are valid against the new shapes.
- **Reference integrity in both directions**: 12 inbound references to the moved ticket, all `review/…`; the
  Review index lists it and the Todo index does not; every self-check file has a MANIFEST row.

## What could not be checked at all

- `dotnet format` — forbidden against a frozen tree (the reviewer declined even `--verify-no-changes`), so the
  formatter claim is unverified by the review; the cycle ran it on the tree itself (exit 0) and measured CRLF
  on all fourteen new files byte-wise afterwards.
- Anything needing the game: no runtime or acceptance run — no materialized item, no swing, no authored tile
  drop condition in a live world, no moodle icon animation. Every mechanism statement in the review is a
  call-site or compile-time reading.
- Whether a third-party mod that hands back `List<ModCraftingQuality>` still compiles: `List<T>` invariance is
  a language fact the reviewer did not compile-test; the self-check's §5 names it as a limit for mod authors.
- The `[Trait("Category", "Integration")]` tiering used by CI; both suites ran in their default configuration.
