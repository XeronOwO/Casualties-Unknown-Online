# The kind contract is an interface: content declarations a mod may implement

Cycle 2026-10-09. Ticket `docs/backlog/todo/mod-content-attribute-declarations.md`, **stage A** (the
declaration contract). Decision 251. Baseline commit `dfb0470a`; this record describes the working tree
that carries the change.

Stage A answers one question: **what is a content declaration?** Before this change it was one of nine
`sealed` data classes and nothing else — every provider began with `is not ModItemDefinition definition`,
so a mod that wanted to COMPUTE a member instead of filling one in had nowhere to go. Now each kind is an
interface (`IModItemDefinition` and its eight siblings), the data classes are the framework's ready-made
implementations of it, and every consumer reads the interface. The `[ModContent]` attribute, the scanner
and the optional per-kind base classes are the ticket's next stage and are **not** in this change.

## §1 Mechanism inventory

One row per touched mechanism, with the evidence that covers it.

| # | Mechanism | What changed | Evidence |
|---|---|---|---|
| 1 | `IModContentDefinition` and the nine kind interfaces | each kind is a public interface extending `IModContentDefinition`, declaring exactly the members its data class already carried | `src/CasualtiesUnknownOnline.Abstractions/IMod{Item,Recipe,Liquid,LiquidTile,Tile,Building,Structure,Status,Moodle}Definition.cs` |
| 2 | The nine data classes | implement their kind interface, keep `sealed` + parameterless construction + settable members, and their member docs became `<inheritdoc/>` so the contract has ONE home | the nine `Mod*Definition.cs` files |
| 3 | The nine Game Adapter providers | `registration.Definition is not IModXDefinition definition` replaces the concrete-type test; the refusal names the interface it wanted | `grep "is not IMod"` over `src/CasualtiesUnknownOnline.GameAdapter/Content/` returns exactly nine hits, one per provider |
| 4 | `ModContentDisplayName.Resolve` | its eight arms switched from the classes to the interfaces, so a mod-authored declaration is named like any other; recipes still fall through to the id on purpose | `src/CasualtiesUnknownOnline.Runtime/Session/Content/ModContentDisplayName.cs` |
| 5 | Item/building/tile template factories and the item behaviour pair | `CustomItemTemplateFactory`, `ModItemInfoFactory`, `CustomItemBehaviorValidator`, `CustomItemBehaviorApplier`, `CustomBuildingTemplateFactory`, `CustomTileFactory` take and return the interface | the six files; measured on the frozen tree, 20 `GameAdapter` files name a kind contract and every one of them reads the interface |
| 6 | The five world-generation distributions | `Item`, `Building`, `Tile`, `Structure` and `LiquidTile` distributions read the interface; the rule behind the layer mask and the depth table now comes from the framework (row 8) | `src/CasualtiesUnknownOnline.GameAdapter/WorldGen/` |
| 7 | `GameAdapterLiquidTileContentProvider` | it used to WRITE five members of the mod's object; the defaults now live on a `NormalizedLiquidTileDefinition` view and the author's object is never touched, and the two checks that had become unreachable behind that write are deleted | `Content/GameAdapterLiquidTileContentProvider.cs` (`Normalize`), `Content/NormalizedLiquidTileDefinition.cs` |
| 8 | The derived rules a declaration feeds | eight members left five data classes for three framework-owned extension containers, so a rule the framework owns is not something every implementation must restate | `ModLayerSpawnRule` (`CanSpawnInLayer` for tile/building/liquidtile), `ModStructureDistribution` (`TryGetSpawnCount`), `ModStatusPresentation` (`ShowsPerLimbMoodles`, `ResolveMoodleId`, `FormatLimbDisplayName`, `FormatLimbDescription`) |
| 9 | `AllSpawnLayers` | one home (`ModLayerSpawnRule.AllSpawnLayers`); the three layer-distributing data classes alias it, so the sentinel is no longer three independent `-1` literals | the three classes' `const` declarations |
| 10 | The reviewed API baseline | 940 → 1109 entries: nine kind interfaces, three extension containers and their members, plus eight tombstones for the members that moved | `docs/contracts/abstractions-api-baseline.txt`; `ApiSurfaceGateTests` |

## §2 Whole-family audit

The census that drove this change was taken by an independent reader over all of `src/` and `tests/`
(not a spot check), and it is what turned three assumptions into findings:

1. **Every consumer of the nine kinds is converted.** The census enumerated every reference in `src/` and
   `tests/`. Measured on the frozen tree, 42 files under `src/` name a kind contract at all — 21 in
   `Abstractions` (the nine interfaces, the nine data classes and three helpers), 20 in `GameAdapter`, 1 in
   `Runtime` — while `Plugin`, `PinyinSearch`, `PinyinSearch.Core`, `Application`, `GameState` and
   `Protocol` name none. The conversion touched every reader, not the first one found.
2. **The write-back had exactly one site, and the contract already forbade it.** The census found that
   `GameAdapterLiquidTileContentProvider` was the only place under `src/` that assigned to a
   definition's member — while `IModContentDefinition`'s own doc promises the framework "stores the
   definition as it was handed over and never interprets its typed members". The change makes that
   promise true instead of moving the violation.
3. **A derived-member family existed and was triplicated.** `CanSpawnInLayer` appeared verbatim on three
   data classes, `AllSpawnLayers`/`LayersToMask`/`AllLayersExcept` on three, and the sole consumer of
   each was a world-generation distribution. The move gives the rule one home and leaves the mod-facing
   authoring helpers where mods already call them.
4. **The null-collection census is unaffected by construction.** `ModNullCollectionRuleTests` scans public
   CLASSES with a parameterless constructor and excludes interfaces by design; the interfaces this change
   adds are therefore invisible to it, and the 26 rows and the count assertion are unchanged because the
   data classes keep every `List<T>`/`Dictionary<K,V>` member. This is also why the interface declares the
   same collection types rather than `IReadOnlyList<T>`: C# interface implementation requires an exact
   return type, and `List<T>` does not satisfy `IReadOnlyList<T>` for that purpose.
5. **Only one place dispatches by type.** The census confirmed `ModContentDisplayName.Resolve` is the
   single type-pattern dispatch over the nine kinds; every other use of `Kind` is routing, logging or
   display text. That is why row 4 of §1 is the whole of the display-name work.

## §3 What landed, and what deliberately did not

**Landed.** The kind contract; the nine implementations; every consumer reading it; the derived rules as
framework-owned extensions; the liquid-tile write-back removed; the baseline re-reviewed with eight
tombstones; the reference and how-to pages (both languages) restated.

**Not landed, on purpose.** The `[ModContent]` attribute, the assembly scanner, the kind-mismatch refusal
and the optional per-kind base classes are the ticket's stage B. The content fingerprint the ticket's
acceptance section owes is likewise not here: it is a save-side consequence of opening the ceiling, and
the ticket leaves it "open at implementation" — stage B is where it lands or is moved to the save ticket
with its reason recorded.

## §4 Verification

| Layer | What it proves | Result |
|---|---|---|
| Build | the contract and every consumer compile against it, with warnings as errors | `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors |
| Focused suite | the content family, its providers and the console vocabulary | 205/205, driven by 25 filter conditions each written in full (the repository's measured trap is that a `~A\|~B` shorthand selects zero tests and still reports success) |
| Behaviour suite | nothing else moved | 4852/4852 (was 4850; the two new cases are this seam's) |
| **The seam** | a mod-authored implementation of EACH kind is accepted by the REAL provider for that kind, and the value the framework reads is the computed one rather than the delegated default | `ModAuthoredDefinitionBindingTests` — 2/2: one real binder run over all nine real GameAdapter providers (built reflectively, since the test project never compile-references GameAdapter) with nine fixtures that implement only their kind interface, and a second case asserting `Assert.Same`, the computed `DisplayName`/`Weight`, and that an untouched member still equals the default |
| The refusal | a definition that claims a kind without implementing its contract is refused by all nine, naming the contract it wanted | `ModContentNullCollectionBindingTests.EveryProvider_RefusesADefinitionOfAnotherTypeFiledUnderItsKind` — nine assertions, whose expected strings moved from the class names to the interface names in this change |
| The contract's shape | the public surface is what was reviewed, and a removal is named | `ApiSurfaceGateTests` — red until the baseline was re-reviewed, green after: the baseline FILE grew 940 → 1112 lines, its recorded entries 860 → 1024 and its tombstones 67 → 75, eight of which are this change's |
| The whole gate set | nothing else in the repository's rules moved | 572/573, the only red being this cycle's own delivery checklist (its remaining boxes are checked before the commit). `FullyQualifiedNameGateTests` found one real defect in this change — a fully qualified `System.Math` spelling in a new file — fixed before the freeze |
| The null-collection census | the rule the ticket names still holds | `ModNullCollectionRuleTests` — 26 rows and the count assertion unchanged, because it scans public CLASSES and the interfaces are invisible to it by construction |

No game process was needed: this change retypes a contract and re-points its consumers, and the seam it opens
is proven at the bind boundary, which is where the ticket's own acceptance rows for it live.

## §5 Independent review

Tier FULL, fresh context, against the frozen tree. Report:
`docs/evidence/selfchecks/mod-api/mod-content-declaration-contract-review.md`. **Verdict: the central claim
survives** — member parity holds member by member across all nine kinds, the liquid-tile normalisation is
value-for-value identical and every reader reaches the view, the eight moved bodies and all six production
call sites are unchanged, nothing dispatches on a concrete class, all 39 alignment hashes reproduce, and
every figure this record stated reproduced. Two majors and four minors were found; every one is
dispositioned here and lands in this commit.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| F1 | major | The recorded isolation guarantee is false for two of the nine providers: `ModContentBinder`'s per-entry `catch` covers only `TryBind`, while the item and building providers first read `SpawnComponents` — and the building one `ItemCategoriesToAdd` — inside their own `Update`, where a null would throw once per frame and stop every declaration ordered after it, including another mod's | **Fixed in the readers, not in the wording.** Every read of a declaration's collection member now goes through `ModDeclarationCollections.OrEmpty` (`src/CasualtiesUnknownOnline.Abstractions/ModDeclarationCollections.cs`); decision 251 and this record were rewritten to state the real mechanism |
| F2 | major | Contract sentences advertised null tolerance the readers did not implement — the building's `DropOnDestroy` flipped HEAD's "Empty means" to "Null means" — and the reader-facing page repeated the promise under a table it did not reach | **Fixed by making the promise true** (F1) and then stating it where the contract lives: `IModContentDefinition` now carries the rule for the whole family, and both language blocks of the reference page describe the two shapes |
| F3 | minor | The baseline figure quoted the FILE's line count where the repository's own rows use entry counts | Corrected in §4: lines 940 → 1112, entries 860 → 1024, tombstones 67 → 75 |
| F4 | minor | Two census counts in §1/§2 did not reproduce from the frozen tree | Both replaced with measurements taken on the frozen tree (§1 row 5, §2 point 1) |
| F5 | minor | The amended-records claim was short by one still-`current` record naming two members that had moved | `mod-api/mod-payload-null-collections-selfcheck.md`'s MANIFEST row carries the bracketed amendment as well |
| F6 | minor | The liquid-tile normalisation was re-implemented with no test pinning it, before or after | Added: `LiquidTileContentProviderTests` drives a declaration whose five normalised members are blank or out of range and asserts what the provider exposes for each |
| N1 | nit | "The two checks that had become unreachable" was half wrong — the `ConsumeOnDrink` branch was live at HEAD | Corrected in decision 251: only the `MaxFloodFill` refusal was dead, and the `ConsumeOnDrink` warning now runs one step earlier |
| N2 | nit | The three `CanSpawnInLayer` tombstones say "the data class is a plain data carrier" while those classes keep the static mask builders | Reworded: the derived rule left, the mod-facing authoring helpers stayed |
| N3 | nit | `ModContentDisplayName`'s arm order is a limit the record did not name | Recorded in §6 |
| N4 | nit | The review could not run the throw, because the chain needs a real prefab | Accepted and stated: F1 is proven by reading the whole chain, and the guard removes the throw rather than hiding it |

One correction to the review's own text, recorded because this record is the one a reader will trust: its
quotation of `IModItemDefinition.SpawnComponents` as ending in *"Null means none."* does not match the tree —
no kind interface carried that sentence. F2's substance (the framework did not honour the rule the page
promised) stands, and the fix covers both members it named.

## §6 Limits

- **No game process ran.** This change retypes a contract and how its consumers read it; nothing in it is
  player-visible, and the runtime evidence is the behavioural suite plus the baseline gate. The rows that
  need a real session stay with the ticket's acceptance section.
- **A null collection is read as "none", not repaired.** Every read of a declaration's collection member
  goes through `ModDeclarationCollections.OrEmpty`, so a mod-authored implementation that returns null for
  a member it does not carry is treated exactly like the data class's coalesced empty list. The change's
  first attempt recorded the binder's per-entry `catch` as that guarantee and guarded nothing; the review
  proved that false (F1), which is why the guard is in the readers and the rule is on
  `IModContentDefinition`.
- **A getter that THROWS is a different failure and is not bounded here.** The ticket's stage B refuses a
  declaration whose getter throws, at the scan, with a log naming it; until then such a throw is bounded by
  the provider's per-service catch, which is loud but not per-definition.
- **`ModContentDisplayName`'s arm order is harmless for any implementation that returns its members
  normally.** A class implementing two kind interfaces resolves to the first matching arm, but all eight
  arms read a member of the same name, so the answer differs only for an explicit interface implementation
  that gives one member two different values — a shape the scanner's two-kinds refusal forbids in stage B,
  and one the code path routes by `Kind` today.
- **Process records were not rewritten.** `docs/backlog/`, `docs/evidence/` and `docs/acceptance/` hold
  dated records that name the data classes as they were when written; those statements stay true of the
  classes. The four self-checks whose text names a MOVED member carry a bracketed amendment in
  `docs/evidence/selfchecks/MANIFEST.md` instead of being rewritten, and the dated review ticket that
  repeats one of those sentences (`docs/backlog/review/mod-payload-null-collection-tolerance.md`) is left
  as the record of the cycle it judged.
- **A class that implements two kind interfaces is not refused on the code path.** `TryRegister` routes by
  `Kind`, so exactly one provider sees such an object; the named refusal the ticket asks for belongs to the
  scanner, where the kind comes from the implemented interface rather than from a `Kind` string.
