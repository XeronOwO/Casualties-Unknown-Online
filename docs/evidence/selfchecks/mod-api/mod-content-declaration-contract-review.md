# Independent adversarial review — a content declaration's kind is an interface (stage A)

Risk tier: **FULL** (architecture, cross-module, and a mod-visible `Abstractions` contract). Reviewed
revision: the UNCOMMITTED working tree on `master` at HEAD `dfb0470a`, read frozen; no file was modified
and `dotnet format` was not run. Change reviewed: `docs/backlog/todo/mod-content-attribute-declarations.md`
stage A, decision 251, against the author's own record
`docs/evidence/selfchecks/mod-api/mod-content-declaration-contract-selfcheck.md`.

The prompt template's **A0** section does not apply and I say why rather than skipping it: this change adds
no Harmony patch, no native call site and no player-facing gesture, and no wire or save shape moves — the
diff contains no file under `src/CasualtiesUnknownOnline.GameAdapter/Patches/`, no `ProtocolVersion`
change and nothing under `Persistence/`. The third-party view a FULL tier must still cover is the MOD
AUTHOR's, and that is where the two majors below live.

## Findings

### F1 — major — the recorded isolation guarantee is false for two of the nine providers; a null collection read after binding throws in the provider's `Update`, and that provider's remaining definitions never materialize

The record states the guarantee twice, in the same words:

- self-check §6: *"A mod-authored implementation that returns null for a collection is not repaired. The
  binder's per-entry `catch` isolates it to that one registration with an error line and leaves the mod's
  other declarations binding; the interface states the rule (an implementation returns a collection) and
  the data classes cannot produce null at all."*
- decision 251: *"so an implementation hands back a concrete collection, and a null return is isolated to
  its own registration by the binder's per-entry catch instead of taking that mod's other declarations
  down"*.

The binder's `catch` covers exactly one call — `ModContentBinder` wraps only `provider.TryBind(registration)`
(*"provider for {Kind} threw while binding {ModId}/{Id}; the entry is skipped."*). Two of the nine providers
first read a collection member AFTER binding, inside their own `Update()`:

- **Item.** `GameAdapterItemContentProvider.Update` iterates `_definitions` and calls
  `EnsureTemplate(pair.Key, pair.Value)`; `EnsureTemplate` calls `CustomItemTemplateFactory.Create(id,
  definition, _log)`, which ends in `CustomComponentAttach.Attach(template, definition.SpawnComponents,
  log, "ItemContent")`; `Attach` opens with `foreach (var componentTypeName in componentTypeNames)`. A null
  list throws there.
- **Building.** `GameAdapterBuildingContentProvider.Update` calls `EnsureTemplate` →
  `CustomBuildingTemplateFactory.Create`, which reads `ToItemCategories(definition.ItemCategoriesToAdd)`
  (a `foreach` over the argument), `CustomComponentAttach.Attach(template, definition.SpawnComponents,
  log, "BuildingContent")`, and then the provider's own success log
  `"[BuildingContent] built runtime template for {Id} (base {TemplateId}, components {ComponentCount})."`
  with `definition.SpawnComponents.Count`.

`TryBind` does not read either member: `CustomItemBehaviorValidator.Validate` reads only
`Container`/`Battery`/`Light`/`Tool`/`Gun`/`Visual`, and `GameAdapterBuildingContentProvider`'s
`ValidateWorldGen`/`ValidateDrops` read the two drop lists only. So the definition is ACCEPTED (the binder
logs its `accepted` line and no error), and the throw lands one frame later, outside the binder:

- `Plugin.RunLifecycle` catches it per SERVICE — `_log.LogError(ex, "ICuoService.{Stage} failed for
  {ServiceType}", stage, service.GetType().Name)` — and the provider's loop returns at that entry every
  frame, because the same entry throws again. Every definition ordered after it, including another mod's,
  never gets a template. That is the opposite of *"leaves the mod's other declarations binding"*, and the
  observable is not *"the binder's per-entry catch … with an error line"* but a repeated
  `ICuoService.Update failed for GameAdapterItemContentProvider` (or `…BuildingContentProvider`).

Verified by reading the whole chain in the frozen tree (paths above; the throw is `foreach` on a null
`IEnumerable<string>`, which is an unconditional `NullReferenceException`). Reachability needs a
mod-authored `IModItemDefinition`/`IModBuildingDefinition` whose `SpawnComponents` (or building
`ItemCategoriesToAdd`) is null and whose `TemplateId` resolves to a real prefab — a shape F2 shows the
published contract explicitly invites. What I could not do is run it: the prefab resolution needs the game
(see the limits section).

The record's supporting sentence — *"the interface states the rule (an implementation returns a
collection)"* — is also not true of the tree: none of the nine kind interfaces nor `IModContentDefinition`
contains the words "collection", "never null" or "must not be null".

### F2 — major — three interface members advertise null tolerance the framework's readers do not implement; the sentence is NEW or CHANGED in this change, and the reader-facing page repeats it

`IModItemDefinition.SpawnComponents` and `IModBuildingDefinition.SpawnComponents` now end their summary
with a sentence HEAD's data classes did not carry at all (HEAD stops at *"…and refuses non-Component
types."*):

> *"Component type names (assembly-qualified or simple names) attached to the runtime template before it
> is instantiated. The Game Adapter resolves the types from loaded assemblies and refuses non-Component
> types. Null means none."*

and `IModBuildingDefinition.DropOnDestroy` flipped the operative word:

- HEAD (`ModBuildingDefinition`): *"Chance-based drops spawned when the building is destroyed. **Empty**
  means no authored chance drops; the vanilla building's own drop table still applies when the base prefab
  carries one."*
- now (`IModBuildingDefinition`): *"Chance-based drops spawned when the building is destroyed. **Null**
  means no authored chance drops; …"*

The framework does not mean "none" for any of the three: `SpawnComponents` is F1 (an uncaught throw in
`Update`), and a null `DropOnDestroy` throws inside `GameAdapterBuildingContentProvider.ValidateDrops`'
`foreach (var drop in definition.DropOnDestroy)` — which is at least inside `TryBind`, so it is isolated,
but the outcome is the binder's logged EXCEPTION and a refused definition, not "no authored chance drops".
The sentence is true of the data class (its setter coalesces null into `[]`) and false of the interface it
is now written on, which is exactly the distinction this change created.

The same promise is on the reader-facing page in both blocks, unchanged by this change while the table
above it was re-labelled to the contracts: `docs/en/reference/mod-api.md` — *"**Null means empty.** Every
collection member of every declaration above — a list or a dictionary — means "none" when it is null, and
the member itself answers for that: a mod that assigns null builds a definition whose list reads empty."*
and `docs/zh/reference/mod-api.md` — *"**null 就是「没有」。** 上述每份定义里的集合成员 …… 为 null 时都表示
「没有」，而且由成员自己负责：模组赋 null 得到的定义里，列表读出来就是空的。"*

Fix either the text or the readers; today the contract licenses the shape F1 punishes. Disposition note
for the parent: F1 and F2 are one decision — guarding the two read sites makes both sentences true, and
rewording alone leaves the throw reachable-but-undocumented, which is the state decision 251 already
accepted.

### F3 — minor — the baseline figure is the FILE's line count, not the entry count the repository's own rows use

The self-check §4 and the `MANIFEST.md` row for this cycle both say *"940 → 1109 entries"*, with *"eight
tombstones"* (the tombstone count is right). Measured on the frozen tree:

| revision | file lines | entries (`type|`/`member|`) | tombstones |
|---|---|---|---|
| `HEAD` | 940 | 860 | 67 |
| working tree | 1109 | 1021 | 75 |

So the reviewed baseline grew **860 → 1021 entries (+161) and 67 → 75 tombstones (+8)**; "940 → 1109" is
the raw line count, which also counts 12 comment lines, 1 blank line and every tombstone. The convention
is not mine: the immediately preceding `MANIFEST.md` row records *"baseline 860 entries / 67 tombstones"*.
The artifact itself is correct — I re-derived the delta as 8 tombstones plus 161 entries.

### F4 — minor — two census counts in §1/§2 do not reproduce from the frozen tree

- §2 and the checklist evidence line: *"Runtime (5 files), GameAdapter (22 files), ModExample (1) and tests
  (26) were enumerated"*. Under every reading I tried, Runtime is not 5: files naming one of the nine kinds
  (interface or class) = Runtime 1 / GameAdapter 21 / ModExample 1 / tests 27; adding `ModContentKind` = 3 /
  24 / 1 / 38; adding the content-registration vocabulary = 13 / 24 / 1 / 39; reading
  `Definition.Id|Kind|SchemaVersion` = 7 / 11 / 0 / 3; and `src/CasualtiesUnknownOnline.Runtime/Session/Content`
  holds 7 files. The other three figures DO reproduce on the "files this change touched / files that
  referenced a kind before the new test" reading — GameAdapter 22 is exactly the touched set (21 modified +
  `NormalizedLiquidTileDefinition.cs`), ModExample 1, and tests 26 is today's 27 referencing files minus the
  test this change added. So one of the four numbers has no reading I could find.
- §1 row 5: *"63 identifier replacements across 16 GameAdapter files in this change"*. I measured the
  GameAdapter diff differently and cannot get either number: 85 added lines carry one of the nine interface
  names, across 19 of the 22 touched GameAdapter files. A "replacement" count depends on a method the record
  does not state, so I record this as unverifiable rather than as wrong.

Neither figure is load-bearing for the change; both are the kind a later record copies.

### F5 — minor — the "three amended records" claim is short by one CURRENT record (and by one dated backlog review)

§6: *"The three self-checks whose text names a MOVED member are marked with a bracketed amendment in
`docs/evidence/selfchecks/MANIFEST.md` instead of being rewritten."* Three amendments are indeed present
(status-moodle-row, structure-worldgen-distribution, tile-ore-worldgen-projection), but a fourth record
`MANIFEST.md` still marks **current** names two moved members and was left alone:

- `docs/evidence/selfchecks/mod-api/mod-payload-null-collections-selfcheck.md` — *"`ModStatusDefinition.ResolveMoodleId`
  and `ModStructureDefinition.TryGetSpawnCount` losing their null clauses"*.
- `docs/backlog/review/mod-payload-null-collection-tolerance.md` — *"`ModStatusDefinition.ResolveMoodleId` /
  `ModStructureDefinition.TryGetSpawnCount` losing their null clauses"*.

Both names no longer exist on those classes, so a reader following either pointer finds nothing — the exact
failure the three amendments were made to prevent. (Two further records name a moved member but are marked
`historical` in the MANIFEST — `mod-building-drop-worldgen-selfcheck.md` cites `CanSpawnInLayer`,
`mod-status-moodle-content-binding-selfcheck.md` cites `ResolveMoodleId` — so their status already covers
them.)

### F6 — minor — the liquid-tile normalisation was re-implemented with no test pinning it, before or after

The prompt's claim 2 asks whether the normalisation is behaviour-preserving; my answer is yes (see the
"could not falsify" section), but only by reading. `tests/CasualtiesUnknownOnline.Tests/Patching/LiquidTileContentProviderTests.cs`
— the provider's only test, unmodified by this change — binds `LiquidId = "water"`, `MaxFloodFill = 128` and
asserts acceptance and id order only. No test anywhere drives a blank `LiquidId`, a blank `FillLiquidId`, a
non-positive `MaxFloodFill`, an out-of-range `VisualLiquidByte` or `ConsumeOnDrink = false`, so the five
defaults this change moved from a write-back onto `NormalizedLiquidTileDefinition` are pinned by nothing.
Its delivery-checklist "Verification design" box claims the shape is proven by the compiler and the baseline
gate and the seam by the binder tests; nothing there covers this rewrite.

### Nits

- **The "two checks that had become unreachable behind the write"** (self-check §1 row 7: *"the two checks
  that had become unreachable behind that write are deleted"*; decision 251: *"the two checks that had
  become unreachable behind the write are deleted rather than kept"*) is accurate for one of the two. At
  HEAD `NormalizeDefaults` forced `MaxFloodFill >= 1` before validation, so the
  *"has invalid MaxFloodFill … — refused"* branch was already dead — no reachable refusal was lost, which
  is the substantive answer to that question. But the `ConsumeOnDrink` branch was LIVE at HEAD (*"if
  (!definition.ConsumeOnDrink) { _log.LogWarning(…); definition.ConsumeOnDrink = true; }"* — the only place
  that member was normalised) and it was a warn-and-set, not a refusal; it moved into `Normalize`. "One dead
  check deleted, one live branch relocated" says what happened.
- **Log order on a refused declaration.** Because the `ConsumeOnDrink` warning now runs inside `Normalize`
  before validation, a declaration that is refused for another reason AND sets `ConsumeOnDrink = false` now
  emits one extra warning that HEAD did not emit (HEAD returned from the value checks first). Log-only, but
  it is an observable difference the "behaviour-preserving" claim does not name.
- **Tombstone wording.** All three `CanSpawnInLayer` tombstones end *"and the data class is a plain data
  carrier"*, while those classes keep `LayersToMask`, `AllLayersExcept` and `AllSpawnLayers` — the class
  summary calls them *"the mod-facing helpers a declaration is authored with"*. "Plain data carrier" is one
  word too strong.
- **`ModContentDisplayName`'s arm order.** Benign as it stands: all eight arms read the same-named
  `DisplayName` member, so a type satisfying several arms yields the same value whatever the order — which
  is why the two-interface case decision 251 names is not a defect here. The one shape that could differ is
  an EXPLICIT interface implementation giving `IModItemDefinition.DisplayName` and
  `IModLiquidDefinition.DisplayName` different values; the record's two-interface limit does not mention it.
  Worth one sentence, not a code change.

## What I tried and could NOT falsify

1. **Member parity (claim 1) — holds exactly.** For each of the nine kinds I extracted every public member
   of the HEAD data class and of the new interface and compared name, type spelling (nullability included)
   and shape: Item 23/23, Recipe 9/9, Liquid 12/12, LiquidTile 26 of 29, Tile 22 of 25, Building 23 of 26,
   Structure 6 of 7, Status 7 of 9, Moodle 11 of 13. The only differences are exactly the eight derived
   members that moved by design (`CanSpawnInLayer` ×3, `TryGetSpawnCount`, `ShowsPerLimbMoodles`,
   `ResolveMoodleId`, `FormatLimbDisplayName`, `FormatLimbDescription`); nothing was added, nothing dropped,
   no type or nullability drifted, and `Id`/`Kind`/`SchemaVersion` come from `IModContentDefinition` with
   `Kind` still fixed by each class (`public string Kind => ModContentKind.<Kind>;`). The `List<T>`/
   `Dictionary<K,V>` types are the classes' own, as the record says.
2. **The liquid-tile normalisation (claim 2) — every value is the one that was read before.** `Normalize`
   reproduces the four `NormalizeDefaults` rules and the fifth (`ConsumeOnDrink`) verbatim, including the
   same warning text; `NormalizedLiquidTileDefinition` delegates the other 21 members unchanged and the
   compiler enforces completeness. Every reader reaches the view, not the mod's object: the provider's own
   `TryGetDefinition`/`TryGetDefinitionByWorldByte`/`GetDefinitionsForWorldGen` all return the stored view
   (`_definitions.Add(id, bound)`), and the four outside readers use exactly those accessors —
   `LiquidTileDrink` (`definition.ConsumeOnDrink`), `LiquidTilePlacement` (`definition.MaxFloodFill`),
   `LiquidTileBodyTouch` (`TryGetDefinitionByWorldByte` → `ApplyRates` → the per-second rates),
   `LiquidTileWorldGenDistribution` (`GetDefinitionsForWorldGen` → `SpawnAmount`, `MaxFloodFill`,
   `CanSpawnInLayer`). Nothing else under `src/` holds the mod's object for those five members;
   `ModContentDisplayName` reads only the display name, which normalisation does not touch. One deleted
   check was provably dead at HEAD (F6's first nit) and the other was relocated, so no refusal a mod could
   still trigger was lost.
3. **The derived-rule move (claim 3) — bodies identical, call sites intact, sentinel single.** Each of the
   eight bodies is the HEAD body with the receiver named (`AllowsLayer(definition.SpawnLayers, biomeDepth)`
   holds the HEAD `CanSpawnInLayer` statements character for character; `ModStructureDistribution` and
   `ModStatusPresentation` likewise). All six production call sites resolve — `TileWorldGenDistribution`,
   `BuildingWorldGenDistribution`, `LiquidTileWorldGenDistribution` (`CanSpawnInLayer`),
   `StructureWorldGenDistribution` (`TryGetSpawnCount`), `ModStatusMoodleProjection`
   (`ResolveMoodleId`, `ShowsPerLimbMoodles`, `FormatLimbDisplayName`, `FormatLimbDescription`) — and the
   two files that needed the `using CasualtiesUnknownOnline.Abstractions;` got it. The strongest evidence
   that the C# 14 extension form kept its call sites is that the UNMODIFIED tests
   (`ModStatusDefinitionTests`, `ModTileDefinitionTests`, `ModBuildingDefinitionTests`) still call those
   members and compile. `ModLayerSpawnRule.AllSpawnLayers` is `-1` and the three data classes alias it as
   `const int AllSpawnLayers = ModLayerSpawnRule.AllSpawnLayers;` (a compile-time constant, so `-1` in
   every consumer); `LayersToMask`/`AllLayersExcept` stay on the classes and unchanged.
4. **No concrete-class dispatch remains (claim 4).** `is`/`is not`/`as`/pattern over any of the nine classes
   returns nothing in `src/`, and the only hit in `tests/` is a doc comment in the new test describing the
   shape that *used to* exist (*"every provider opened with `if (registration.Definition is not
   ModItemDefinition definition)`"*). `Select-String 'is not IMod'` over
   `src/CasualtiesUnknownOnline.GameAdapter/Content/` returns exactly nine hits, one per provider, as the
   checklist claims.
5. **Nothing else in `src/` or `tests/` changed meaning.** I paired every removed line with its added line
   in the whole `src/`+`tests/` diff and compared them after normalising the renames away: outside the
   nine base-list changes, the three `AllSpawnLayers` aliases and the liquid-tile rewrite, every difference
   is an identifier swap or a doc comment. The nine providers' refusal text also changed `not a {Expected}`
   → `not an {Expected}`; that is correct, because the expected value is now an interface name ("an
   IMod…"), and both doc blocks quote the new line.
6. **The baseline is honest (claim 6).** The eight tombstone keys are exactly the eight members that left
   the classes, and no other entry lost its key: the only other changed lines are the nine
   `type|…|sealed class|` base lists and the three `AllSpawnLayers` initializers — same key, changed
   signature, which is the "reviewed line updated" path and needs no tombstone. Every ADDED line is the nine
   kind interfaces, the three containers and their members; `ModLayerSpawnRule.AllowsLayer` is private and
   correctly absent, `NormalizedLiquidTileDefinition` is internal and correctly absent, and nothing public
   leaked in by accident. Each tombstone's reason names a member that exists at the place it claims.
7. **Docs and records (claim 5).** Both reader-facing pairs were updated in both languages and their
   recorded hashes verify: I recomputed all 39 `alignment.txt` pairs with `git hash-object` and got **0
   stale**. The provider refusal line was updated in both blocks
   (`{ModId}/{Id} claims kind {Kind} but is a {Type}, not an IModItemDefinition — refused`), no human page
   lists a moved member as a class member, and no page still quotes the old `not a ModItemDefinition` form.
8. **The self-check's numbers, where I could run them.** Build `dotnet build CasualtiesUnknownOnline.slnx`
   = 0 warnings, 0 errors. Gates `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` = 573
   total, 572 passed, **1 failed and it is `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`**
   — exactly the three boxes this cycle still owes (`Self-check table`, `Build + dotnet format + dotnet test
   normative gates pass`, `Structure review done`). The seam and the census:
   `ModAuthoredDefinitionBindingTests` + `ModContentNullCollectionBindingTests` + `ModNullCollectionRuleTests`
   = 41/41 green, which covers the two new cases and the pinned census count
   (`ExpectedConstructedMemberCount = 26`, unchanged). The behaviour project enumerates **exactly 4852**
   test cases (`--list-tests`), consistent with "was 4850; the two new cases are this seam's".
9. **What a null collection does per provider — the parts the record gets right.** For the seven providers
   whose `TryBind` itself reads the collection (item `Qualities`, recipe `Ingredients`, liquid `Qualities`,
   tile `Drops`, structure `Rows`/`VanillaBlocks`/`TileIds`/`SpawnCounts`, status `LimbMoodles`, building
   `DropOnDestroy`/`AlwaysDrop`), a null throws inside the binder's `try`, so it IS isolated to that one
   registration. F1 is the item/building residue that runs later.

## What I could not check at all

- **No game process ran, so F1's throw is proven by reading, not by a run.** The chain needs a real prefab
  for `TemplateId` (`Resources.Load<GameObject>`), which only the game provides; what I verified statically
  is that nothing between `TryBind` and the `foreach` can reject a null list.
- **The behaviour suite's pass figure.** I enumerated all 4852 cases but did not execute them (the review
  was scoped to narrow filters), so "4852/4852" is reproduced as a COUNT and its affected subset only.
- **The focused 205/205.** The record names 25 filter conditions but does not record them, so I could not
  rerun that exact command; a 41-case subset of the same family (the two seam tests, the nine-provider
  refusal, the census) is green.
- **`dotnet format` exit 0** — forbidden here (it rewrites the frozen tree), so that half of the checklist
  line is unverified by me.
- **Stage B's refusals** (the kind-mismatch and two-kinds refusals the ticket still owes) do not exist yet;
  I could only confirm that no two-kind refusal exists on the Runtime/GameAdapter path and that routing by
  `Kind` means exactly one provider sees a declaration that implements two kind interfaces.

## Verdict

**Not falsified on its central claim** — the interface carries exactly the class's members, the derived-rule
move is behaviour- and call-site-identical, the liquid-tile rewrite reads the same five values everywhere,
and the baseline, the alignment pairs, the build and the gate counts all reproduce — **but the change is not
fit to commit as recorded**: one major (`F1`, the null-collection isolation the record promises does not hold
for the item and building providers, and the failure is a repeated per-service error that stalls that
provider's remaining definitions) plus a major contract-text contradiction (`F2`) that invites exactly that
shape, and four minors that are record/count/coverage accuracy rather than behaviour.
