# Typed content registration — independent review

Review of the uncommitted change set "content registration carries its type" against the FROZEN working
tree at `3c1590ad` (branch `master`). Read-only: the only file this review writes is this report. The
change's own claims were attacked, never the evidence for them; every claim below was re-opened in the
tree, and the suites that can run without the game were run.

Ticket: `docs/backlog/review/mod-content-typed-registration.md` (moved out of `todo/` at cycle close).
Decision: entry 247, with 244 narrowed.

## 1. Verdict

**No blocker, no major.** Three minor findings and six nits, all documentation, evidence-anchor or
stability-declaration level; the mechanism itself is sound and the eight claims hold. Two figures in the
hand-off need correcting (the nine definition suites went **38 → 49** facts, not 37 → 49, and the provider
cast refusal is exercised for **one** of nine providers, not family-wide).

## 2. What was run (evidence)

- `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` → **573 total, 571 passed, 2 failed**,
  and the two are exactly the ones declared expected: `DeliveryChecklist_NoIncompleteRequiredBoxes`
  ("Delivery gate failed (2 issue(s), 6 boxes checked)" naming only *Build + dotnet format + dotnet test
  normative gates pass* and *Structure review done*) and
  `BacklogReferenceGateTests.EveryBacklogReference_ResolvesToATicket`, whose single dangling reference is
  `docs\decisions\active.md: docs/backlog/review/mod-content-typed-registration.md`. No third gate is red,
  so `ApiSurfaceGateTests`, `ContentKindProviderCoverageGateTests`, `SelfcheckManifestGateTests` and
  `DocumentationTreeGateTests` are green on this tree.
- `dotnet test tests/CasualtiesUnknownOnline.Tests` → **4818 passed / 4818** (net48), matching the freeze.
- Independent reflection census over the built `CasualtiesUnknownOnline.Abstractions.dll` (PowerShell +
  `System.Reflection`, no test helper involved): 32 collection members on the assembly's public classes =
  **1 travelling payload member** (`ModStatusUpdate.Value`) + **27 mod-built members** + the **4**
  constructor-only declarations; **3** `[DataContract]` types remain (`ModStatusUpdate`,
  `ModBodyFormulaProjection`, `ModLimbProjection`); each of the nine DTOs reads
  `Kind`/`Id`/`SchemaVersion` = the claimed kind / `""` / `1`; `ModContentKind` declares exactly nine
  string constants whose values equal those nine kinds.
- `git hash-object` on the four edited en/zh pairs reproduces every hash recorded in
  `docs/standard/alignment.txt`.
- Baseline line-set diff plus the gate: **36 recorded lines removed, 42 added, 26 tombstones added**; no
  tombstone duplicated, none for a key that is still declared or re-declared, none removed.
- Docs-wide anchor scan: every `XTests.Y` reference under `docs/` matched against the 4,247 test methods
  declared under `tests/`.
- Repository greps for the deleted surface (`ModContentDefinition`, `MaxDefinitionBytes`, `IsValidData`,
  a content DTO's `ToPayload`/`FromPayload`, a `[DataContract]` on a content DTO) over `src/`, `tests/`,
  `tools/` and `docs/`.

## 3. Claim by claim

| # | Claim | Verdict | Where it was checked |
|---|---|---|---|
| 1 | `IModContentDefinition { Id, Kind, SchemaVersion }`; `TryRegister(IModContentDefinition)` replaces both overloads; no caller can file a definition under a foreign kind; a mod can still register its own kind | **Holds** | `src/CasualtiesUnknownOnline.Abstractions/IModContentDefinition.cs` ("One content definition a mod registers through `IModContent`"), `IModContent.cs` (`bool TryRegister(IModContentDefinition definition);`); grep finds no byte[] overload anywhere. The "own kind" path is real and driven: `StubContentDefinition` (new, `tests/CasualtiesUnknownOnline.Tests/Fakes/StubContentDefinition.cs`) is registered by `ModContentTests`, enumerated by `ModContentBinderTests`, `ModContentCatalogTests`, `ModContentOwnerQueryTests` and `ModContentResourceLocationSourceTests` |
| 2 | Nine DTOs implement it with the claimed `Kind` constants, settable `Id` (empty) and `SchemaVersion` (1); every `ToPayload`/`FromPayload` and `[DataContract]`/`[DataMember]` on them and their member objects is gone; `ModPayloadCodec` serves only the three travelling contracts, which keep their payload pair unchanged | **Holds** | Reflection over the built assembly (above); `git status` shows `ModStatusUpdate.cs`, `ModBodyFormulaProjection.cs`, `ModLimbProjection.cs` unmodified; `[DataContract]` survives only in those three files plus `ModPayloadCodec.cs`'s own scan |
| 3 | `ModContentDefinition` deleted; registry keeps the handed-over instance; the other rails unchanged; `MaxDefinitionBytes`/`IsValidData` gone | **Holds** | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModContentAdapter.cs` ("The registry keeps the definition object the mod handed over — it takes no copy") and `_definitions.Add(definition)`; `ModContentPolicy.cs` keeps `MaxKindLength = 64`, `MaxDefinitionsPerMod = 1024`, `IsValidId`, `IsValidKind`, `IsValidSchemaVersion`, `CanAdd`; no reference to the two deleted members remains under `src/`, `tests/` or `tools/` |
| 4 | All nine providers open with a pattern-match cast and refuse anything else with a named warning; the binder still routes by `Definition.Kind`; `IContentBindingProvider.Kind` still names a `ModContentKind` constant | **Holds as code; coverage narrower than the summary reads** | All nine `src/CasualtiesUnknownOnline.GameAdapter/Content/GameAdapter*ContentProvider.cs` read `registration.Definition is not Mod<Kind>Definition definition` and log `"... claims kind {Kind} but is a {Type}, not a {Expected} — refused."`; all nine declare `public string Kind => ModContentKind.X;`; `ModContentBinder.cs` routes on `registration.Definition.Kind`. See finding **M3** for the test coverage |
| 5 | Decision 244's MEMBER half kept, DECODE half shrunk; the census is two groups with pinned counts 1 and 27 plus four constructor-only declarations, and an assembly-wide equality assertion leaves nothing unaccounted for | **Holds exactly** | `ModPayloadNullCollectionTests.cs` (`ExpectedPayloadMemberCount = 1`, `ExpectedConstructedMemberCount = 27`, `ParameterObjectMembers` = the four named members, `Assert.Equal([...], EveryCollectionMemberInTheAssembly())`); the whole suite passes, and my independent reflection census reproduces 1 / 27 / 4 / 32 without using the test's own helpers |
| 6 | Coverage MIGRATED: the nine definition suites 37 → 49 facts; no test deleted in the eleven `Patching/` suites | **Holds with one figure corrected** | Counted from HEAD and the worktree: the nine suites declare 38 `[Fact]`s at HEAD and 49 now (no `[Theory]` in either state); every removed test name is a round-trip/invalid-payload case whose subject no longer exists. In `Patching/`, ten suites kept every method; `StatusMoodleContentProviderTests` renamed `StatusProvider_RejectsInvalidPayload` to `StatusProvider_RefusesADefinitionOfAnotherTypeFiledUnderItsKind` (same count 5) — the deleted case tested undecodable payload bytes, which no longer exist |
| 7 | The baseline records the additions plus 26 removals, each with a tombstone | **Holds** | 36 removed / 42 added lines; 26 added tombstones, all matched to a removed key, no duplicates, none for a still-declared key (the gate checks the same three conditions). The 10 further removed lines are the 9 DTO `type|…|sealed class|-` base-list lines and `IModContent.Definitions`' element type — the gate keys a member by owner+name+parameter list and a type by its name, so these are `CHANGED`, not removals, and the policy requires a tombstone only for the latter |
| 8 | Docs: four en/zh pairs edited with re-recorded hashes; decision 247 added and 244 narrowed; the affected MANIFEST rows marked historical; the example mod and the acceptance recipe register a typed definition | **Holds except two rows and two anchors** | All four pairs' `git hash-object` values equal the registry; decision 247 sits in `docs/decisions/active.md` with `**[Narrowed 2026-10-08]**` on 244; 13 MANIFEST rows flipped to `historical`; `ExampleMod.cs` registers `new ModItemDefinition { Id = "example.trophy", … }`; `tools/acceptance/recipes/building-template-inject.cs` builds a typed `ModBuildingDefinition`, sets `Id`, and binds `new ModContentRegistration(owner, definition, null)`. See findings **M1**, **N3** |

## 4. The mechanism, not the wording

- **Does the registry really keep the instance?** `RegisteredDefinition_IsStoredAsTheInstanceTheModHandedOver`
  asserts `Assert.Same(definition, content.Definitions.Single(...))` twice (by id, and by kind+id), which is
  the strongest available form; `ModContext.cs` re-registers those same instances into the framework-wide
  entries (`[.. _content.Definitions.Select(d => new ModContentRegistration(...))]`). No copy remains
  anywhere on the read path.
- **Is the provider's cast branch reachable at all?** Yes, and this matters: `ModContentKind.Item` is a
  public constant and `IModContentDefinition` is implementable by a mod, so a hand-written definition with
  `Kind => ModContentKind.Item` reaches the item provider through the binder and is refused with a warning
  naming both types. The branch is not dead code — but only one provider's branch is driven by a test
  (**M3**).
- **Is the binder still the only router?** `provider.TryBind(registration)` has exactly one caller in the
  tree (`ModContentBinder.cs`); no provider is invoked from anywhere else, which is what makes dropping the
  providers' own `Kind` comparison safe rather than a lost guard. `ModContentBinderTests` still pins the
  routing (a `Recipe` entry goes to the recipe provider, an unknown kind is reported and nothing is bound).
- **Does the null-write theory cover every collection member, or only remembered ones?** It covers the
  assembly: the census builds its three groups by reflection over `typeof(IModContent).Assembly`, and the
  equality assertion compares their union, as a sequence, with the assembly-wide scan — a member added to a
  contract, a `[DataContract]` type that loses its payload root, or a class that moves between the groups
  fails the assertion. My own reflection run reproduced the same 1 / 27 / 4 from the same assembly without
  the test's helpers, so the counts are a property of the tree, not of the test.
- **Did any mechanism lose its consumer?** No touched member is now read by nothing:
  `IModContentDefinition.Id` is read by the registry, catalog, owner query, console source and the
  duplicate rail; `Kind` by the binder's router, the catalog's grouping and the console entry;
  `SchemaVersion` by `ModContentAdapter`'s positive-version rail, the catalog's version-mismatch group
  (`Select(e => e.Definition.SchemaVersion)` feeding `ModContentConflictKind.VersionMismatch`) and every
  provider's acceptance line. Conversely, everything deleted (`ModContentDefinition`, `MaxDefinitionBytes`,
  `IsValidData`, the 18 payload methods) has zero remaining references outside historical evidence pages.
- **Half-migrated residue.** The docs pages, the two edited tickets, decision 244 and 13 MANIFEST rows moved
  with the change; what did not is listed in **M1**, **N1**, **N3** and **N5**.

## 5. Contract shape (the three questions, asked of the NEW surface)

- **A payload:** the added and changed lines carry typed definitions, not blobs
  (`TryRegister(IModContentDefinition definition)`, `IReadOnlyCollection<IModContentDefinition> Definitions`).
  The change is a net reduction of the erased-shape debt: it removes 18 payload methods, one `byte[]`
  member and one wrapper from the baseline. `ModStatusUpdate.Value` stays `byte[]` as the mod-owned value
  inside CUO's own envelope, which the sweep ticket already carries.
- **A handle:** `IModContentDefinition` is a CUO-defined type, never `object`; the three members are
  `string`/`string`/`int`. No engine type enters the contract.
- **A binary value:** the new surface contains none. Nothing added introduces a `byte[]`, `object`,
  `Delegate`, `dynamic` or `IntPtr`.

So the shape passes on its own merits, and `IModContentDefinition` (three get-only members a mod may
implement) is the right form for a contract that separates identity (`Id`), routing vocabulary (`Kind`) and
the mod's own versioning (`SchemaVersion`) without leaking any content schema into the store. The one
judgement this review would put back to the author is the declared stability level — finding **M2**.

## 6. Findings

### M1 (minor) — Two self-check pages still labelled `current` cite tests this change deleted

- `docs/evidence/selfchecks/mod-api/mod-crafting-quality-labels-selfcheck.md`:
  "`TryBind_TreatsAnExplicitNullQualityListAsNoQualities`, `ModItemDefinitionTests.ExplicitNullQualities_IsNoneNotAFailedDefinition`".
  The second method no longer exists (replaced by `NullCollectionMembers_MeanNone`); the page's MANIFEST row
  still reads "`mod-api/mod-crafting-quality-labels-selfcheck.md` | Mod platform | current".
- `docs/evidence/selfchecks/mod-api/mod-tile-ore-worldgen-projection-selfcheck.md`:
  "New worldgen/drop fields survive the opaque payload | `ModTileDefinitionTests.RoundTrip_PreservesWorldGenerationAndDrops`".
  That test is deleted with the whole round-trip family; the MANIFEST row still reads `current`.

Verified by scanning every `XTests.Y` reference under `docs/` against the 4,247 test methods declared under
`tests/`, then checking each hit's MANIFEST status. The change flipped 13 sibling rows to `historical` and
updated the same stale anchors in `docs/backlog/review/mod-crafting-quality-labels.md` and
`docs/backlog/review/mod-payload-null-collection-tolerance.md`, so this is an inconsistency inside the
change's own migration rather than a pre-existing drift (the deleted tests are deleted BY this change).
Both rows are one line each; either update the anchor or flip the row.

### M2 (minor) — The new `IModContentDefinition` is declared `Stable` although the policy says a new surface starts `Experimental`

`docs/en/reference/modification-policy.md` states: "**A surface without the attribute is `Stable`**, so a
level is a declaration someone made, never a default that merely happened", and the level table says
"`Experimental` | Documented and usable, and allowed to move while it settles. **A new surface starts
here**; the promotion funnel below is how it becomes `Stable`", with step 3 of the funnel reading "marked
`[ApiStability(ApiStabilityLevel.Experimental)]`". The baseline records the new type and its three members
as `Stable` (`type|Stable|CasualtiesUnknownOnline.Abstractions.IModContentDefinition|interface|-` plus the
three `member|Stable|…` lines), and no `[ApiStability]` attribute was added
(`src/CasualtiesUnknownOnline.Abstractions/IModContentDefinition.cs`), while the repository does use the
marker for new surfaces — `IModPackets`, `IModPacketContext`, `ModPacket`, `ModPacketSender`,
`ModPacketDelivery`, `ModPacketHandler`, `ModPacketStage`, `IModResourceCompletion`,
`IResourceLocationMatchStage`, `ResourceLocationEntry` and two `IModContext` members all declare
`Experimental`.

The defensible counter-argument is that this interface replaces a `Stable` contract and is the type of nine
already-`Stable` DTOs, so `Stable` may be the deliberate level — but neither decision 247 nor the ticket
names a level, so the declaration currently happens by omission. Decide it explicitly: add the marker, or
record in decision 247 why the replacement inherits `Stable`.

### M3 (minor) — The "claims a claimed kind, is the wrong type" refusal is exercised for one of nine providers

`tests/CasualtiesUnknownOnline.Tests/Patching/StatusMoodleContentProviderTests.cs` is the only provider
suite that drives it, and it does prove both halves:

> "The typed registry decodes no payload any more, so the reachable refusal is a definition that claims the
> status kind without being a `ModStatusDefinition`: it must be refused and never kept."
> `Assert.False(TryBind(provider, new StubContentDefinition("status.bad", ModContentKind.Status)));`
> `Assert.False(IsKept(provider, "status.bad"));`

A repository-wide scan for `new StubContentDefinition` finds no other provider-suite use: the other
occurrences are the registry (`ModContentTests`), the binder, the catalog, the owner query and the console
source, none of which reaches a GameAdapter provider. The remaining eight providers' cast branches —
`GameAdapterItemContentProvider`, `GameAdapterRecipeContentProvider`, `GameAdapterLiquidContentProvider`,
`GameAdapterLiquidTileContentProvider`, `GameAdapterTileContentProvider`, `GameAdapterBuildingContentProvider`,
`GameAdapterStructureContentProvider`, `GameAdapterMoodleContentProvider` — are untested. The ticket's
self-check row reads family-wide ("the provider's cast refusal is driven by `StubContentDefinition` under a
claimed kind"), and the cast loop is the identical shape nine times, so this is a coverage claim that
outruns the coverage rather than nine risky branches: one parameterised case per provider (or a single case
driving the real binder over all nine with a stub per claimed kind) closes it.

### N1 (nit) — `docs/standard/terminology.txt` contradicts the two glossaries this change edited

The registry both blocks are written from still says:

- "payload | 载荷 | 载荷 | 数据包，内容体 | the opaque bytes a message **or a content definition** carries;
  the framework never reads inside them" — while the edited glossary now reads "the opaque bytes a message
  or a runtime value carries" (`docs/en/reference/glossary.md`) and "一条消息或一份运行时值携带的不透明字节"
  (`docs/zh/reference/glossary.md`).
- "schema version | 结构版本 | … | the mod-owned version **stored with an opaque persisted payload**; the
  framework never migrates it" — while the edited glossary now reads "the version a mod declares on its own
  state, runtime value or content definition".

No gate reads `terminology.txt` (the gate project has no terminology test; `DocumentationTreeGateTests`
only verifies the alignment hashes, which reproduce), so the drift is silent.

### N2 (nit) — The narrowed MANIFEST row is the only row in the file with the wrong shape

`docs/evidence/selfchecks/MANIFEST.md`'s table is "| File | Domain | Status | Note |". The row for
`mod-api/mod-payload-null-collections-selfcheck.md` now reads
"| … | Mod platform | current **[Narrowed 2026-10-08]** The decode half now reaches only … plus a null write
for each mod-built member. ||": the note sentence landed inside the Status cell, the Note cell is empty and
a trailing `|` makes it five columns. It is the only such row in the file (checked: exactly one line ends
with `||`), and `SelfcheckManifestGateTests` parses only the file token, so nothing catches it.

### N3 (nit) — Two per-cycle records still state the old census counts

- `docs/evidence/selfchecks/mod-api/mod-payload-null-collections-selfcheck.md` was not edited: it still
  describes "the shared decode step behind all twelve `FromPayload` methods", "the scan surface … `[DataMember]`
  properties", "all three shapes for every member" and "the same 27 rows" as the current census, where the
  tree now has three `FromPayload` roots, one payload member (three shapes) and 27 mod-built members (null
  write). Its MANIFEST row was annotated "[Narrowed 2026-10-08]", which is the mitigation, but the page body
  a reader lands on still contradicts the annotation.
- `docs/backlog/review/mod-payload-null-collection-tolerance.md`: "27 members across the payload-contract
  graph (26 `[DataContract]` types, of which 12 own a `ToPayload`/`FromPayload` pair)". The change updated
  this ticket's two test-name anchors but not its counts.

Both are per-cycle records rather than live pages, which is why this is a nit and not a defect — but the
ticket is in `review/`, i.e. still citable as the record behind decision 244.

### N4 (nit) — "37 → 49" should be "38 → 49"

The nine definition suites declare 38 `[Fact]`s at HEAD (`ModItemDefinitionTests` 5, `ModRecipeDefinitionTests`
2, `ModLiquidDefinitionTests` 2, `ModLiquidTileDefinitionTests` 3, `ModTileDefinitionTests` 6,
`ModBuildingDefinitionTests` 5, `ModStructureDefinitionTests` 6, `ModStatusDefinitionTests` 5,
`ModMoodleDefinitionTests` 4) and 49 today; neither state uses `[Theory]`, so the migration is +11 from 38.
The current figure (49) is exactly as claimed and is reproducible. Nothing in the repository states 37, so no
file needs fixing — only the hand-off number.

### N5 (nit) — Payload-era wording left on two touched surfaces

- `src/CasualtiesUnknownOnline.Abstractions/IModContent.cs`: "so the kind travels with the object and the
  registry never has to interpret a payload" — the registry now interprets nothing at all; the interface's
  own page says "never interprets its typed members".
- `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModContentBinder.cs`: "stays opaque and is never
  materialized" — consistent with the pages' "stays opaque" phrasing, so this one is only noted as the same
  vocabulary shift, not as an error.

### N6 (nit) — The docs describe the no-provider warning but not the wrong-type refusal a hand-written definition can now trigger

`docs/en/how-to/register-content.md` tells the mod author to "implement `IModContentDefinition` yourself,
declare your own kind tag and its own data" and that "the binder says so once at load time, at warning
level" for a kind no provider claims. A hand-written definition that declares a *claimed* kind (`item`) is
refused by that provider instead ("claims kind {Kind} but is a {Type}, not a {ModItemDefinition} — refused"),
which is a second, different line to look for. One sentence on the same page would close it.

### Observation (not a finding) — 53 `tools/acceptance/recipes/*.cs` files show as modified with no content change

`git status` lists 145 modified paths where the content diff covers 92; the 53 extras are acceptance recipe
files whose filtered hash equals both the index and the HEAD blob (checked `git hash-object` with and
without filters against `git rev-parse :path` and `HEAD:path` for a sample). They will not appear in a
`git add -A` commit. Recorded so that the status count is not mistaken for part of this change.

## 7. What could NOT be falsified, and what could not be checked

Falsified nothing (no claim fell): every restated claim was confirmed in the tree except the two figures
corrected above, and no test I could run was red for a reason other than the two declared expected failures.

- **Not falsified, but residual by design:** the registry now keeps a live reference to a mod-authored
  object, so a mod that mutates `Id`/`Kind` after registering can desync the duplicate rail from what the
  catalog later reads. The payload version could not express that mistake; the new contract answers it in
  prose only ("the registry keeps the instance the mod handed over … so a mod registers a definition it does
  not mutate afterwards", in `IModContent`/`IModContentDefinition` and both blocks of the mod API and
  register-content pages). No test pins the hazard in either direction. This is a documented, accepted trade
  (decision 247's "the wrapper … [has] no consumer"), not an unstated one.
- **Needs the game, so not checkable here:** the acceptance recipe
  `tools/acceptance/recipes/building-template-inject.cs` is evaluated by the driver at run time, not
  compiled by the suites; I verified its text and its typed construction, not its execution. The same
  applies to whether `cuo.example`'s `example.trophy` materializes on a client.
- **No wire or save claim is at stake:** no file under the protocol or persistence projects appears in the
  change set, so the "no wire/save shape moves" statement is consistent with the diff; I could not run a
  two-client session to observe it.
- **Not run:** `dotnet format` (forbidden — it rewrites files) and a separate `dotnet build` (both test runs
  built the tree).
- **Cycle-close work that this report cannot do:** the row for this file in
  `docs/evidence/selfchecks/MANIFEST.md`, the two unticked boxes in `docs/evidence/delivery-checklist.md`,
  and the ticket's move from `todo/` to `review/` (which also fixes the one expected dangling backlog
  reference). `SelfcheckManifestGateTests.EverySelfcheckFile_HasExactlyOneManifestRow` is red from the
  moment this file exists until its MANIFEST row is added. Measured state after this report was written:
  the gate suite reads **573 total, 570 passed, 3 failed** — the two declared expected failures plus
  `"1 self-check file(s) have no MANIFEST row: mod-api/mod-content-typed-registration-review.md"`. Adding
  that one row and ticking the two checklist boxes returns the suite to the declared state; the backlog
  reference clears when the ticket reaches `review/`.
