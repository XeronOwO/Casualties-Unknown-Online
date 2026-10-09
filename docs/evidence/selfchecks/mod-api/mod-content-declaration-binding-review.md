# Independent adversarial review — the `[ModContent]` scan and its ownership rule (stage B)

Risk tier: **FULL** (a mod-visible `Abstractions` contract, cross-module wiring, and a discovery-time code path
that executes mod-authored code). Reviewed revision: the UNCOMMITTED working tree on `master` at HEAD
`08873a6c`, read FROZEN — no file was modified, and `dotnet format` was not run in any form (not even
`--verify-no-changes`, because a mistake there would rewrite the tree this review's premise depends on).
Change reviewed: `docs/backlog/review/mod-content-attribute-declarations.md` stage B, decision 252, against the
author's own record `docs/evidence/selfchecks/mod-api/mod-content-declaration-binding-selfcheck.md`.

## A0 — the premise, before anything else

The change adds no Harmony patch, no native call site, no player-facing gesture and no wire or save shape: the
whole `src/` diff is `ModLifecycle` (two calls and their doc), `ModRegistry` (one helper swapped for a shared
one) and `ModExample` (declared content); there is no file under `src/CasualtiesUnknownOnline.GameAdapter/`, no
`ProtocolVersion` change and nothing under `Persistence/`. The entry mapping therefore has nothing to name, and
the checklist's box says so — but it says it with a claim that is not true ("the one thing a player can see is
the example mod shipping two attribute-declared items and a recipe"): the example assembly is **not deployed at
all** (F2), which is the *stronger* reason the premise holds. The third-party view a FULL tier must still cover
is the MOD AUTHOR's, and that is where F1, F3, F5 and F6 live.

Two of the three A0 questions do not apply and I say why rather than skipping them: no entry here carries an
action, so no gesture can be missing information (question 2) and no item can be claimed by two entries
(question 3). Question 1 — which native action is this the counterpart of — has no entry to answer for; the one
thing the cycle makes reachable in a game is content (an item and a recipe), not an input.

## Findings

### F1 — major — a declaration that cannot be INSTANTIATED takes the whole mod down, not just itself, and the record states the opposite

`ModContentDeclarationScanner`'s own summary promises: *"Isolation: the per-declaration work is inside its own
guard, so one broken declaration never blocks a sibling, and the census never stops the mod scan."* The guard
covers the member sweep (`TryReadEveryMember`'s `catch`) and `content.TryRegister` (its own `catch`). It does
not cover the instantiation that sits between them:

- `RegisterOne` opens `if (Activator.CreateInstance(declaration) is not IModContentDefinition definition)`, with
  no `try` around it, and `Register`'s loop is bare — `foreach (var declaration in owned) { if
  (RegisterOne(declaration, content)) { registered++; } }`.
- `ModLifecycle.RegisterDeclarations` is called inside `DiscoverAndLoad`'s per-mod `try`, whose handler is
  `_log.LogError(e, "[Mods] {Id} failed to load — skipped, the other mods continue.")`.

So a parameterless constructor that throws (surfaced as `TargetInvocationException`), or a static initializer
that throws (`TypeInitializationException`), aborts the loop at that declaration: every declaration ordered
after it never registers **and the mod itself never Binds** — its network, commands and UI are gone too. The
shape is reachable by the scan's own rules (`GetConstructor(Type.EmptyTypes) is null` only asks that the
constructor EXIST), and the declared style deliberately runs mod code at discovery.

The self-check states the guarantee in the same shape: §2.4 lists *"a class that is not
public/concrete/constructible"* among the shapes where *"Every one of them is a log line naming the class, and
none of them takes a sibling down."* A class whose constructor throws is exactly the "constructible" case the
code does not isolate. The blast radius is confined to the offending mod (a failed mod never enters
`ModCatalog`, so nothing of it is bound — see the "could not falsify" section), which is why this is not a
blocker; but a mod author who would have lost ONE item loses the whole mod, and the refusal they were promised
is an unhandled exception instead.

Fix: one `try`/`catch` around the instantiation, logging the same "refused; the other declarations still bind"
line the member sweep uses. If it is left as is, both statements above have to be narrowed.

### F2 — minor — the example consumer is counted twice and described as deployed; it is neither

Four sites state one miscount and one deployment fact that the tree contradicts:

- Self-check §1 row 6: *"the production consumer: two items and one recipe declared by attribute and NESTED in
  `ExampleMod`"*. Only ONE item carries `[ModContent]` — `ExampleDressing`; the sibling `ExampleDressingRecipe`
  is the recipe. The second item, `example.trophy`, is registered by code
  (`context.Content.TryRegister(new ModItemDefinition { Id = "example.trophy", … })` in `Bind`), which the
  class's own summary says plainly: *"It declares its CONTENT both ways on purpose. `example.trophy` is
  registered by code in `Bind`; `example.dressing` and `example.dressing.recipe` are classes next to this one
  carrying `[ModContent]`"*.
- The same count again in `docs/evidence/delivery-checklist.md`: *"the one thing a player can see is the
  example mod shipping two attribute-declared items and a recipe"* and *"the example mod is 277 (was 129: two
  declared items, a recipe and the console command)"*.
- Self-check §6: *"**The example's declared content is not exercised in a real game yet.** It is deployed by the
  same `tools/deploy.ps1` as before"*. It is not: `CasualtiesUnknownOnline.Plugin.csproj` references only
  `Runtime` and `GameAdapter`, its output folder carries no `CasualtiesUnknownOnline.ModExample.dll`, and
  `tools/deploy.ps1` copies exactly `Get-ChildItem -LiteralPath $PluginOut -Filter *.dll`. The repository's own
  records say so independently: *"the deploy scripts do not ship `CasualtiesUnknownOnline.ModExample.dll`, so
  the batch copies it by hand"* (`docs/evidence/selfchecks/mod-api/mod-defined-wire-packets-selfcheck.md`; the
  same sentence is in `docs/backlog/review/mod-defined-wire-packets.md`).

The deployment half matters for the acceptance row this cycle's §6 hands over: *"the acceptance batch is where
its item and recipe are read in the game's own tables"* — that batch has to copy the DLL by hand, exactly as
the earlier mod-API batches did, or there will be nothing to read. The count half matters because it is the
figure a later record copies (it already did: three sites).

### F3 — minor — a declaration owned by a `[CuoMod]` type the framework never loads is silently inert, and the census's own "no mod" line is suppressed by that same type

`ModContentDeclarationScanner.IsACuoMod` is `typeof(ICuoMod).IsAssignableFrom(type) &&
type.IsDefined(typeof(CuoModAttribute), inherit: false)` — no class/concrete/visibility test — while
`ModRegistry.Discover` loads only types matching `t.IsClass && !t.IsAbstract && (t.IsPublic || t.IsNestedPublic)
&& typeof(ICuoMod).IsAssignableFrom(t)` and logs a reason for every candidate it then rejects.

Consequences for an assembly whose `[CuoMod]` type is `internal` (or abstract, or non-class): the census counts
it as a mod, so the line *"declares {Count} [ModContent] class(es) but no [CuoMod] mod — nothing registers
them"* never fires; the registry's own filter drops the type before any log line; and `Register` is never called
because the registry never yields the type — so the declarations register nothing and no line anywhere says so.
An assembly that had left the attribute off would have got the census warning; adding `[CuoMod]` to a non-public
class replaces that warning with silence. The same holds for a mod the registry rejects for its manifest
(missing dependency, duplicate id, bad SemVer, invalid namespace): the mod's rejection IS logged, but nothing
connects it to the declarations that will therefore never register.

§6's limits name the count half of this — *"Ownership treats a `[CuoMod]` class that fails discovery as a
mod. … an assembly whose second mod is rejected still makes its top-level declarations ambiguous"* — but not the
silence, which is the half that costs an author their content. The repository's own test assembly is the shape:
`ModContentDeclarationScannerTests.ScanFixtureMod` is `internal` and `BothAttributesMod` is `private`.

### F4 — nit — three record counts that do not reproduce

- §1 row 3: *"`ModContentDeclarationScannerTests` — 13 cases over ownership and 10 over registration"*. The class
  holds **15** cases (enumerated with `--list-tests`): six `Plan_*`/`Census_*` and nine registration cases (four
  facts plus a five-row theory) — i.e. 6 + 9, not 13 + 10. The only reading I could find that lands on 13 and 10
  is a fixture count: the test file declares 13 types carrying `[CuoMod]`/`[ModContent]`, ten of which sit
  inside the fixture mod. That is not what "cases" means beside a test-class name.
- §4's Ownership row: *"`Plan_*` and `Census_ReportsEachAssemblyLevelRefusalOnce` (5 cases, each asserting the
  log line)"*. `Plan_*` is five cases and the named `Census` case is a sixth, and two of the six assert the
  opposite (`Plan_OwnsADeclarationNestedInsideItsMod` and
  `Plan_OwnsATopLevelDeclaration_WhenTheAssemblyDeclaresOneMod` both end in `Assert.Empty(log.Entries)`).
- `docs/evidence/delivery-checklist.md`: *"§4 (nine verification layers, each with its command, its filter
  conditions and its result)"* — §4 has ten rows (Build, Format, Focused suite, Behaviour suite, Ownership,
  Refusals and isolation, The seam end to end, One list four representations, The contract's shape, The whole
  gate set).

### F5 — nit — the mod-visible `TryRegister` contract still says "Register during `ICuoMod.Bind`"

`src/CasualtiesUnknownOnline.Abstractions/IModContent.cs` closes `TryRegister`'s summary with *"Register during
`ICuoMod.Bind`."* The framework itself now registers a mod's declared content BEFORE `Bind`, and both new pages
tell the author so (*"The scan runs BEFORE `ICuoMod.Bind`, so a mod's own bind already sees what it
declared."*). The claim is still true of a mod-authored call; it is no longer the whole truth about the surface,
and it is the sentence IntelliSense shows the author whose declaration the scan just registered for them.

### F6 — nit — "the cross-mod conflict rule is exercised" has no assertion behind it

§1 row 7 says the end-to-end fixture means *"the registry, the catalog, the owner query and the cross-mod
conflict rule are exercised against the real stack"*. The only thing the new family asserts about conflicts is
the absence of one — `Assert.False(catalog.HasConflicts)` in
`ModContentDeclarationTests.DeclaredContent_IsAddressableThroughTheCatalogAndTheOwnerQuery`. Nothing in the new
tests drives two mods declaring the same id; that rule is pinned only by the pre-existing catalog cases. Either
drop the clause or keep it for the reason that is true (the conflict pass runs over the fixture and finds
none).

### F7 — nit — two `current` records still point at the deleted `todo/` ticket path

This cycle re-pointed `docs/backlog/README.md`, the sibling ticket, decision 251's row, the code comment in
`tests/CasualtiesUnknownOnline.Tests/Patching/ModAuthoredDefinitionBindingTests.cs` and the MANIFEST row of
`mod-content-declaration-contract-selfcheck.md` — and left two records whose MANIFEST rows are still `current`
naming `docs/backlog/todo/mod-content-attribute-declarations.md`, which no longer exists:

- `docs/evidence/selfchecks/mod-api/mod-content-declaration-contract-review.md` — *"Change reviewed:
  `docs/backlog/todo/mod-content-attribute-declarations.md` stage A, decision 251"*.
- `docs/evidence/selfchecks/mod-api/mod-api-typed-seams-review.md` — *"`docs/backlog/todo/mod-content-attribute-declarations.md` re-points"*.

`BacklogReferenceGateTests` exempts `evidence/selfchecks/` by design
(`RecordPrefixes = ["backlog/done/", "backlog/resolved/", "evidence/selfchecks/"]`), which is why the gates stay
green; the previous cycle's review raised exactly this class of dangling pointer and the remedy then was a
bracketed MANIFEST amendment. Low priority — but it is the same failure mode, twice.

## What I tried and could NOT falsify

1. **Every figure the change states, where the frozen tree can answer.** Build `dotnet build
   CasualtiesUnknownOnline.slnx` = **0 warnings, 0 errors**. Behaviour `dotnet test
   tests/CasualtiesUnknownOnline.Tests` = **4876 passed / 0 failed** (54 s), and 4876 − 4854 = **22** decomposes
   exactly as the three new classes: `ModContentContractTests` 2 + `ModContentDeclarationScannerTests` 15 +
   `ModContentDeclarationTests` 5 (the only other test-tree change is a doc-comment path in
   `ModAuthoredDefinitionBindingTests`). Baseline: **1112 → 1115 lines, 1024 → 1027 entries, 75 tombstones**
   (72 `*REMOVED* member` + 3 `*REMOVED* type`, unchanged), and the diff is **exactly 3 ADDED lines and 0
   removed** (`ModContentAttribute`, `ModContentContract`, `ModContentContract.TryGetKind`). Line counts:
   `ModContentDeclarationScanner` **303**, `ModContentContract` **56**, `ModContentAttribute` **44**,
   `AssemblyTypes` **30**, `ModLifecycle` **373** (was **349**), `ModRegistry` **350** (was **365**),
   `ExampleMod` **277** (was **129**). Every one reproduces.
2. **The gates — but at 573/573 GREEN, not the 571/573 the record quotes.** See "could not check" for why the
   mid-cycle pair cannot be re-derived. The checklist's own box ("Build + dotnet format + dotnet test normative
   gates pass") is therefore checkable at the frozen tree, which is what matters.
3. **The CS0535 measurement (148 errors).** I could not run the deleted first attempt, but the number
   reproduces from the tree: the nine kind contracts declare 24/9/12/27/23/24/9/8/12 members of their own
   (148 in total), and a base class that implements only `Id`/`Kind`/`SchemaVersion` leaves exactly those
   unimplemented — one CS0535 each. The compiler rule is C#'s, not the record's.
4. **`AssemblyTypes.Loadable` versus the deleted `ModRegistry.SafeGetTypes`, line by line.** Identical: the same
   single `catch (ReflectionTypeLoadException e)` around the same `assembly.GetTypes()`, the same
   `e.Types.OfType<Type>()` fallback (the old comment's promise — a partially loadable mod DLL cannot take the
   scan down — moves with it), no other exception is caught in either. The only differences are the declared
   return type (`IEnumerable<Type>` → `IReadOnlyList<Type>`, which `.SelectMany(AssemblyTypes.Loadable)` binds
   through covariant method-group conversion) and the file. Nothing else changed meaning: `ModRegistry` is not
   on the mod-visible surface, so the baseline is untouched (its three added lines are the new Abstractions
   types only).
5. **`ModLifecycle` changes neither the lifecycle nor the load order** — with one residue, which is F1. The
   census call is added BEFORE the load loop and after `_registry.Discover`; `RegisterDeclarations` is added
   between the `ModContext` construction and `instance.Bind(context)`, inside the same per-mod `try`. No mod
   loads earlier or later, and the dependency skip, the discovery frame and the pump are untouched.
6. **The member sweep covers every member of every contract — today.** All nine contracts extend
   `IModContentDefinition` and nothing else (read one by one), so `Members`' "contract's own properties plus the
   three address members, deduped by name" is complete; all contract members are properties (no method
   survives on any of the nine), so a properties-only sweep loses nothing; the widest contract
   (`IModLiquidTileDefinition`, 27) is fully swept. The completeness is also PINNED rather than assumed:
   `ModContentContractTests` asserts the constant count is 9 and that every interface in the assembly extending
   `IModContentDefinition` resolves through `ModContentContract.TryGetKind`, so a tenth contract cannot appear
   without failing there.
7. **The ownership rule, branch by branch, by reading the code the tests drive.** Nesting walks the whole
   `DeclaringType` chain (`for (var enclosing = declaration.DeclaringType; …)`), so a declaration nested in a mod
   nested in another type resolves — the fixture `ScanFixtureMod.DeclaredItem` is exactly that, with the test
   class as the outer type. A single-mod assembly owns a top-level declaration (`mods.Count == 1`); a multi-mod
   assembly refuses it by name and returns an EMPTY plan (so nothing is guessed and no mod is denied);
   a declaration carrying both attributes is refused before any ownership question; a declaration is added to
   exactly one owner's list (no double registration is possible); the per-assembly plan is cached
   (`_plans.TryGetValue`), so a five-mod assembly logs its assembly-level refusals once — which
   `Census_ReportsEachAssemblyLevelRefusalOnce` pins by calling `Census` twice and asserting `Assert.Single`.
   The count in the refusal line is the number of `[CuoMod]` DECLARATIONS in the type list, not the number of
   validated manifests, so it can exceed the mods that load — fail-closed, and the wording says "declares".
8. **The isolation that IS claimed, shape by shape.** Two kind contracts, none, a `Kind` that disagrees, a
   non-public class, an abstract class, an open generic, no public parameterless constructor and a throwing
   member getter: each is one log line naming the class (or the member) and each leaves the siblings alone —
   read in `RegisterOne`/`TryReadEveryMember` and driven by `ModContentDeclarationScannerTests` (15 cases) and,
   for the throwing member, through the REAL registry by `ModContentDeclarationTests`. The unit tests can fail
   for the reason they claim: `Register_RefusesAThrowingMember_ByName_AndKeepsItsSiblings` needs the sweep to
   exist (`ModContentAdapter.TryRegister` reads only `Id`/`Kind`/`SchemaVersion`/`GetType().Name`, so without
   the sweep the throwing item WOULD register and the case would fail), and
   `DeclaredContent_IsRegisteredBeforeTheModsOwnBind` reads `DeclarationsVisibleAtBind`, which is computed inside
   `Bind` and would be `false` if the scan ran after it.
9. **A `TargetInvocationException` is surfaced well enough.** `TryReadEveryMember` logs
   `"[ModContent] {Type}.{Member} threw while the declaration was read — refused; the other declarations still
   bind."` with the exception object, and Microsoft.Extensions.Logging renders the full `ToString()`, so the
   inner exception (the mod's own) is in the line; the message names the declaring type and the member. The one
   thing that is not spelled out is the wrapper's own name, which is cosmetic.
10. **A mod that fails to load leaks no observable content.** `DiscoverAndLoad` adds a mod to `_catalog` only
    after `Bind` + `Initialize` + `Start`, and both observable surfaces read the catalog:
    `ModLifecycle.Entries` is `_catalog.Mods.SelectMany(m => m.Context.ContentRegistrations)` and
    `ModContentCatalog`/`ModContentOwnerQueryAdapter` read `IModContentControl` = that same list. So the
    definitions a failed mod already registered sit in an orphaned `ModContentAdapter` nothing can reach, and
    `ModContentBinder` never sees them.
11. **The census's own guard.** `Census` wraps each assembly in `try`/`catch` and logs
    `"[ModContent] {Assembly} could not be read for content declarations — skipped."`, and the plan it computed
    is cached, so `Register` re-reads a plan that already succeeded rather than re-running the census. A throw
    during `Register` is the F1 residue.
12. **The example's declared content can materialize, as far as static reading can prove.**
    `ExampleDressing` passes `GameAdapterItemContentProvider.TryBind`: `WorldSpawnPerChunk` is null,
    `CustomItemBehaviorValidator.Validate` sees six null behaviour members, and its one quality `"dressing"`
    satisfies `CraftingQualityDeclarations.IsValid` (`ContentId.IsValidNamespace("dressing")` is true), so the
    item is accepted and injected into `Item.GlobalItems` by the provider's `Update`; `TemplateId` is empty, so
    `EnsureTemplate` returns before `CustomItemTemplateFactory` ever needs a prefab.
    `ExampleDressingRecipe` passes `GameAdapterRecipeContentProvider.TryBind` (non-empty result id, one
    ingredient) and `BuildRecipe` (category `"medicine"` parses; `ResultItemId` resolves once the item provider
    has run — it is registered before this provider in `GameAdapterComposition` and the recipe provider's own
    summary states the order). The one input I cannot check is that `"rippeddressing"` really is a vanilla item
    id in `Item.GlobalItems` at build time — the tree's own runtime catalogs
    (`RemoteHealProfiles`, `RemoteBandageMinigameCatalog`, `RemoteMedicalTreatmentSoundCatalog`) treat it as one.
    The only order hazard I found is bounded: a `BuildRecipe` that returns null adds the id to `_failedKeys`,
    which is cleared only when `Recipes.recipes` is REPLACED (a new world), so a hypothetical first-frame miss
    would delay the recipe to the next world rather than lose it.
13. **No dangling pointers or index rot in the areas this change touched.** The backlog README lists both new
    tickets and the moved one exactly once each; every `todo/`/`review/` file is listed and every listed path
    exists (233/233); the two hand-on tickets carry the acceptance rows they claim verbatim; the ticket's status
    fields match its container (`review/`, README under Review).
14. **The docs, both blocks.** 39 alignment pairs recomputed with `git hash-object` — **0 stale**, and exactly
    the two pairs the record names changed (en/zh `reference/mod-api.md` and en/zh `how-to/register-content.md`).
    The two blocks carry the same four new reference bullets and the same new how-to section, and the sample's
    12 members match `IModRecipeDefinition`'s 3 + 9. The claim "the scan adds no id rule of its own" holds: ids,
    kinds-as-strings, schema versions, the cap and the duplicate rule are still decided in `ModContentAdapter`
    over `ModContentPolicy`.
15. **The tree is the formatter's own output, as far as byte-level checking goes.** All eleven added/modified
    C# files are pure CRLF (0 bare LF): 44/56/30/303/501/88/69/216/373/350/277 CRLF endings.

## What I could not check at all

- **`dotnet format` exit 0** — forbidden here (it rewrites the frozen tree), so that half of the checklist line
  is unverified by me; the CRLF half above is what I could substitute.
- **The focused 151/151.** The record says "15 filter conditions each written in full" but records none of them
  anywhere in the tree, and the `%TEMP%` transcripts other cycles leave behind are gone, so the exact command
  cannot be rebuilt. What I can say: the behaviour project enumerates 4876 cases, the three new classes hold 22
  of them, and the whole suite is green — the family's pass is not in doubt, the figure's decomposition is.
- **The 571/573 mid-cycle pair.** The frozen tree is 573/573 GREEN (reproduced twice: once alone, once inside
  the full run), so the two mid-cycle reds cannot be re-derived. The record labels the figure "mid-cycle" and
  names both causes (the checklist's own required boxes and the stale ticket path), and the arithmetic is
  consistent (573 − 2 = 571); I could not make either red happen again, and I did not edit anything to try.
- **Anything that needs the game.** No process ran: whether the declared item and recipe land in the real tables,
  whether `"rippeddressing"` is in `Item.GlobalItems`, and whether the example's item row and recipe row are what
  the acceptance batch expects. The chain up to `TryBind` is read; the tables are not.
- **An EXPLICIT interface implementation of a kind contract.** None exists anywhere in the tree (no
  `string IModItemDefinition.Id`-style member), so the sweep's behaviour on one is reasoned rather than
  observed: `PropertyInfo.GetValue` on an interface property dispatches through the object's interface map, so
  an explicit implementation should be read exactly like an implicit one — but nothing pins it, and the
  hand-on ticket for nested member contracts does not name it either.
- **Driving new code paths.** The tree is frozen, so I wrote no test: the ownership and instantiation edge cases
  above (a throwing constructor, an undetectable `[CuoMod]` type, a declaration nested two levels deep under a
  non-mod) are read from the production code and from the existing fixtures, not executed by a case I added.

## Verdict

**No blocker.** The central claim holds and every figure the change states reproduces from the frozen tree:
the kind comes from the one contract (`ModContentContract`), the scan instantiates the class, reads every member
of that contract and hands it to the same `IModContent.TryRegister` the code path uses, before `ICuoMod.Bind`;
ownership is nesting with the single-mod assembly as the fallback and the ambiguous case refused by name while
the mods still load; `AssemblyTypes.Loadable` is the deleted `SafeGetTypes` verbatim; the gates are 573/573
green, the behaviour suite 4876/4876, the baseline +3 with no removal, the alignment 39/39, and the line counts
all match.

One **major** — F1: the record's per-declaration isolation does not cover instantiation, so a declaration whose
constructor (or static initializer) throws loses its mod's whole declared set AND the mod itself, where the
record promises one named refusal; the fix is one `try`/`catch`, and if it is not made the two statements in
`ModContentDeclarationScanner` and §2.4 must be narrowed. Then three minors — F2 (the example consumer counted
twice, described as deployed where `tools/deploy.ps1` does not ship the assembly, which the acceptance batch
must know), F3 (declarations whose `[CuoMod]` owner is never loaded are silently inert, and the census's own "no
mod" line is suppressed by that owner) — and four nits (F4 counts, F5 the `TryRegister` summary, F6 the
conflict-rule claim, F7 two dangling ticket paths).

Note for the parent: writing this file puts `SelfcheckManifestGateTests.EverySelfcheckFile_HasExactlyOneManifestRow`
red until this record gets its MANIFEST row — that is the gate doing its job, not a finding.
