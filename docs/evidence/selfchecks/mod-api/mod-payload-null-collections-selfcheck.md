# Null collections in a mod payload — mechanism inventory and self-check

Owner cycle: `review/mod-payload-null-collection-tolerance.md` (2026-10-07), filed by the independent
review of `review/mod-crafting-quality-labels.md`, which normalised the two sites it touched and recorded
the remaining sweep here.

Decision (entry 244): every collection member of every mod-authored contract in Abstractions means "none"
when it is null, and **both ends of a payload answer for it** — the member's own coalescing setter for a
null write, and the shared decode step behind every `FromPayload` for a member the payload omits (the
serializer runs no constructor and no initializer, so an absent element has no other owner).

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Why a null collection exists at all | a net48 probe against the built Abstractions assembly settled both shapes: `DataContractSerializer` runs no constructor and no field initializer (0 constructor calls during deserialization), an explicit `i:nil` reaches the member's property setter exactly once (carrying null, coalesced by the member), and a member whose element is ABSENT is never set at all and stays null. The nil shape was pinned by a test this cycle replaces; the census now drives all three shapes for every member |
| 2 | What a guarded consumer did before | each provider normalised only the members it happened to read — `definition.Qualities ??= []` (item, liquid), `definition.Ingredients ??= []` (recipe), `definition.Drops ?? []` (tile), `definition.Rows ?? []`/`VanillaBlocks ?? []`/`TileIds ?? []`/`SpawnCounts ?? []` (structure), `definition.DropOnDestroy is not null` (building) — and the tile provider read `Drops` through a null-safe pattern in one place and `?? []` in another in the same file. Those guards were the ONLY cover for the omitted shape: nothing else ran |
| 3 | What the unguarded members cost | the two members with no guard anywhere are both named `SpawnComponents`: `CustomItemTemplateFactory` and `CustomBuildingTemplateFactory` hand it to `CustomComponentAttach.Attach`, which iterates it, and each provider's own success line reads `definition.SpawnComponents.Count`. All four sites sit on the runtime-template path inside the provider's `Update`, so a null threw out of the service: `Plugin.RunLifecycle` logs `ICuoService.Update failed for {ServiceType}` every frame (the throwing template is never recorded in the provider's failure set) and that frame's remaining definitions are skipped with it. A guardless dereference INSIDE `TryBind` would instead have been caught by `ModContentBinder` (`provider for {Kind} threw while binding {ModId}/{Id}`) at the cost of the whole definition — this tree had none there |
| 4 | Where the rule lives now | two places, because no single one can see both shapes: the member (`public List<T> X { get; set => field = value ?? []; } = [];`, the initializer kept for the mod that builds the DTO in code) and `ModPayloadCodec.Decode<T>`, the one decode step behind all twelve `FromPayload` methods, which replaces every null collection member of the decoded graph with an empty one — nested contracts and collection entries included — and therefore REPAIRS the decoded definition rather than hiding a null field |
| 5 | Why the seam is not a per-contract method | the walk is generic over `[DataContract]`/`[DataMember]` shapes, so a new member or a new contract is covered without touching it, and the twelve roots lose twelve copies of the same serializer try/catch to one call. A depth bound guards a self-referential payload; the contracts are flat |
| 6 | What "genuinely required" looks like | an empty collection flows into the provider's own validation, which refuses the definition with the reason it names: item/liquid `ModItemSpriteAnimation.FramePaths` → "has an invalid base\|worn\|liquid sprite animation — refused"; `ModRecipeDefinition.Ingredients` → "has no ingredients — refused"; `ModStructureDefinition.Rows` → "declares {Height} rows but supplied {Actual} — refused"; `ModMoodleAnimation.FramePaths` → "has an invalid icon animation — refused". None of them is a binder exception |
| 7 | The scan surface | `typeof(IModContent).Assembly` — the mod-facing assembly — public classes, `[DataMember]` properties whose type is `IEnumerable` and not `string`; a nested contract is reached through the root contract that carries it, and the reachability is what makes the count a floor |
| 8 | What the scan found outside the payload contracts | five more collection members a mod can make null: `CuoModAttribute.Dependencies`, `ModConsoleCommand.ArgumentKinds`, `ModManifest.Dependencies`, `ModPacket.Handlers`, `ModStatusMoodleRequest.Payload`. Two of them (`ModManifest.Dependencies`, `ModPacket.Handlers`) already coalesced in their constructors and are unmodified; the rest normalise null at their own declaration, and `ModContentDefinition.Data` is carried as an accounted-for exception (a defensive copy of bytes the registration policy already refused when null, so its accessor cannot answer null) |

## 2. Whole-family audit

| Contract (payload) | Collection member | Rule |
|---|---|---|
| `ModItemDefinition` | `SpawnComponents`, `CustomData`, `Qualities` | null = none |
| `ModItemContainer` | `TagRestriction` | null = none (empty already meant "accept every item") |
| `ModItemVisual` | `MultiWornSprites` | null = none |
| `ModItemSpriteAnimation` | `FramePaths` | null = none; empty is refused by the item validator with its own message |
| `ModItemTool` | `SwingSounds` | null = none (the declared default list is the fallback, not a guard) |
| `ModLiquidDefinition` | `Qualities` | null = none |
| `ModLiquidTileDefinition` | `CustomData` | null = none |
| `ModMoodleAnimation` | `FramePaths` | null = none; empty is refused by the moodle validator with its own message |
| `ModMoodleDefinition` | `CustomData` | null = none |
| `ModRecipeDefinition` | `Ingredients` | null = none; empty is refused with "has no ingredients" |
| `ModStatusDefinition` | `CustomData`, `LimbMoodles` | null = none; empty means "no per-limb routing" (the provider's existing rule) |
| `ModStatusUpdate` | `Value` | null = none (an empty value payload is a legal set frame) |
| `ModStructureDefinition` | `Rows`, `VanillaBlocks`, `TileIds`, `SpawnCounts`, `CustomData` | null = none; an empty `Rows` is refused with the row-count message, an unmapped marker with its own |
| `ModTileDefinition` | `CustomData`, `Drops` | null = none (empty already meant "no custom drops") |
| `ModBuildingDefinition` | `SpawnComponents`, `CustomData`, `DropOnDestroy`, `AlwaysDrop`, `ItemCategoriesToAdd` | null = none |
| Code-constructed | `CuoModAttribute.Dependencies`, `ModConsoleCommand.ArgumentKinds`, `ModManifest.Dependencies`, `ModPacket.Handlers`, `ModStatusMoodleRequest.Payload` | null = none, at the declaration's own construction point (the middle two already coalesced in their constructors and are unmodified) |
| Consumers aligned in the same change | the guards above deleted, `is not { Count: > 0 }` turned into the emptiness check it always meant, `ModRegistry`'s `attribute.Dependencies ?? []` deleted, `CustomBuildingTemplateFactory`'s two private helpers tightened to non-nullable parameters, `ModStatusDefinition.ResolveMoodleId` and `ModStructureDefinition.TryGetSpawnCount` losing their null clauses | every payload member's rule now lives at the member and at the decode seam; one coalesce remains on a non-payload source (`ModStatusMoodleProjection` feeding the runtime moodle request from the status store's own nullable out value), and it is named rather than counted as the rule |
| The decode seam | `ModPayloadCodec.Decode<T>` replaces the twelve duplicated `FromPayload` serializer bodies and normalises every null collection member of the decoded graph, nested contracts and collection entries included | the twelve roots call it; the census drives its two decode shapes per member |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Decode path, the nil shape | every member is non-null and empty after decoding a payload that carries an explicit nil for it | `ModPayloadNullCollectionTests.ExplicitlyNullCollection_IsNoneInBothPaths`, 27 rows |
| Decode path, the OMITTED shape | every member is non-null and empty after decoding a payload whose element for it is absent — the shape the review's probe proved no setter can see | the same 27 rows, which remove the element from a real payload |
| The decoded contract is repaired, not hidden | re-encoding a definition decoded from an omitted element writes an empty collection, never a nil | the same 27 rows |
| Assignment path | every member reads empty after a null is assigned in C# | the same 27 rows |
| Our own payload never carries nil | the member's own element is asserted free of `i:nil` before either rewrite | the same 27 rows |
| The census cannot drift | the discovered member set is asserted equal to the assembly's public-class collection members, with a pinned count | `Census_CoversEveryCollectionMemberOfEveryPayloadContract` (33 names), 1 case |
| Code-constructed declarations | each of the five returns an empty collection for a null argument | `CodeConstructedDeclarations_TreatNullAsNone` |
| End-to-end binding | the real binder + all nine GameAdapter providers, every payload collection member null: the definition binds, or the provider refuses it with its own reason, and no binder error is logged | `ModContentNullCollectionBindingTests`, 10 cases |
| The two required members keep their refusal | item animation and moodle animation with no frames are refused by name, a recipe with no ingredients and a structure with no rows likewise | the same suite, 4 of its 10 cases |
| DTO-level rule | null means none rather than an explicit nil | `ModItemDefinitionTests.ExplicitNullQualities_IsNoneNotAFailedDefinition` |

## 4. Verification design

- The census is driven from the assembly, not from a hand-written list: the theory's rows ARE the
  discovered members, so a member added later is covered without editing the test, and a member that
  forgets the rule fails a named row.
- Two payloads are REWRITTEN out of a real one — the member's element turned into an explicit nil, and the
  element removed — so the evidence does not depend on the serializer producing either shape. What both
  shapes do to a decode was measured once, outside the repository, with the review's net48 probe against the
  built assembly: `constructor calls during deserialization: 0`, an explicit nil reaching the setter once
  with null, and an omitted element left null on the real contract as well.
- The binder-level half drives the production path (`ModContentBinder` → `IContentBindingProvider`) with
  recording loggers on both sides, so "refused with the message that says why" is read from the log rather
  than inferred from a boolean.
- Four mutation controls, each expected to turn a named case red and each restored byte-identically
  (SHA-256 compared before and after): a member's setter, the decode seam's normalisation call, the census's
  own scan surface, and a required member's refusal.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| Focused (`ModPayloadNullCollectionTests` + `ModContentNullCollectionBindingTests`) | 39 passed / 0 failed |
| `dotnet test CasualtiesUnknownOnline.slnx` (with build) | behaviour 4794 passed / 0 failed; gates 451 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | exit 0, run once before the commit |
| Mutations | M1 the `ModItemDefinition.Qualities` setter reverted to a plain one → 4 cases red (the census row, `ModContentNullCollectionBindingTests.Item_EveryCollectionNull_Binds`, `ModItemDefinitionTests.ExplicitNullQualities_IsNoneNotAFailedDefinition`, `CraftingQualityLabelTests.TryBind_TreatsAnExplicitNullQualityListAsNoQualities`); M2 the decode seam's `NormalizeCollections` call removed → 27 rows red; M3 the census's `IsCollection` narrowed to skip `byte[]` → the census case red; M4 the item validator's frame-path refusal removed → `Item_AnimationWithoutFrames_IsRefusedByItsOwnRule` red. Every mutated file was restored byte-identically (SHA-256 equal) and the focused suites re-run green afterwards |

## 6. Independent review

Adversarial review in a fresh context against the frozen tree, read-only, FULL tier: **1 blocker, 1 major,
6 minor, 4 nit**. The blocker is what the review exists for and it changed the code: this cycle's first cut
had the member half only and claimed the decode path was covered, and a net48 probe against the built
assembly proved that `DataContractSerializer` runs no constructor and no initializer — so a payload that
OMITS a member's element left it null, which is the shape the deleted consumer guards had been the only
cover for. `ModPayloadCodec` was added in the same commit, the census now drives the omitted shape for every
member, and every claim about the rule was rewritten to match. The other findings (a retained guard named
honestly, unreproducible counts, an over-stated "every consumer", the wrong `[DataContract]` count, the
census's real scan surface, a stale anchor from the renamed test, an index row over budget, and the
mutation evidence recorded nowhere) are fixed in the same commit.

## 7. Limits

- The provider paths that need Unity objects (runtime template construction, `CustomComponentAttach.Attach`,
  sprite/`Resources.Load` animation frames) are not executed by the suite. The rule is proven at the boundary
  every path shares — the member and the decode seam — so no production path can hand a provider a
  collection member that reads null; `CustomComponentAttach` itself was already non-nullable and is
  unmodified, and the two `CustomBuildingTemplateFactory` helpers that used to take a nullable collection now
  take a non-nullable one.
- `ModPayloadNullCollectionTests` covers public CLASSES of Abstractions; an interface member is a read-only
  view the framework answers, not a declaration a mod fills in (16 of them are outside the census), and the
  framework's own file formats (`ModStateFile`, `HostBanFile`) are outside the mod payload contracts.
- The decode seam normalises collections only: a null scalar or a null nested contract keeps its existing
  meaning (absent), and the framework's own protocol messages (`ProtocolCodec`, `NetPacket`) do not run
  through it.
- Nothing here needs the game running: every row is load-time and client-local, and a session would only
  re-confirm that content still appears, which the existing content-binding acceptance rows already cover.
