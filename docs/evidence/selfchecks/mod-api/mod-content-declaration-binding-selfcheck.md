# The `[ModContent]` scan: a declaration next to the mod, registered through the code path's own rail

Cycle 2026-10-09. Ticket `docs/backlog/review/mod-content-attribute-declarations.md`, **stage B** (the
attribute and the scan). Decision 252; stage A is decision 251 and
`mod-content-declaration-contract-selfcheck.md`. Baseline commit `08873a6c`; this record describes the
working tree that carries the change.

Stage B answers the other half of the user's question: **how does the framework FIND a declaration?**
Stage A made a declaration a class a mod may write; stage B lets that class say so itself — a bare
`[ModContent]`, no data, no registration call — and gives the scan the rules that keep the kind and the
owner from being guessed.

## §1 Mechanism inventory

One row per touched mechanism, with the evidence that covers it.

| # | Mechanism | What changed | Evidence |
|---|---|---|---|
| 1 | `ModContentAttribute` | the bare marker, and the doc that states the three rules a mod author needs: the kind is the contract, ownership is nesting, and the declaration is instantiated and read at discovery | `src/CasualtiesUnknownOnline.Abstractions/ModContentAttribute.cs` |
| 2 | `ModContentContract` | the ONE home of "`IModItemDefinition` means `ModContentKind.Item`": one table, one `TryGetKind(Type, out string)`, so a class that implements none of the nine is answered rather than guessed at | `src/CasualtiesUnknownOnline.Abstractions/ModContentContract.cs`; `ModContentContractTests.EveryKindConstant_HasExactlyOneContract_AndOneReadyMadeImplementation` derives both halves from the assembly and asserts the three representations agree |
| 3 | `ModContentDeclarationScanner` | the census (which assemblies are read), the ownership plan (nesting, else the assembly's only mod, else refused by name), the named refusals, the member reads and the delegation to `IModContent.TryRegister` | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModContentDeclarationScanner.cs`; `ModContentDeclarationScannerTests` — 17 cases: 7 over ownership and 10 over registration |
| 4 | `ModLifecycle` | the census runs once before the load loop (so an assembly no mod of which loads still reports its declarations), and the scan runs per mod BEFORE `ICuoMod.Bind` | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModLifecycle.cs` (`RegisterDeclarations`); `ModContentDeclarationTests.DeclaredContent_IsRegisteredBeforeTheModsOwnBind` |
| 5 | `AssemblyTypes` | the one home of "the loadable types of an assembly" — `ModRegistry` and the scan both read their types through it, so a partially loadable mod DLL cannot take either down | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/AssemblyTypes.cs`; `ModRegistry`'s private copy is deleted |
| 6 | `ModExample` | the production consumer: one item and one recipe declared by attribute and NESTED in `ExampleMod` (that assembly declares two mods, so nesting is what gives them an owner), two members computed from the mod's own value, and `exampleweight` to move it — beside the item `Bind` still registers by code, which is the other path on the same registry | `src/CasualtiesUnknownOnline.ModExample/ExampleMod.cs` |
| 7 | The end-to-end fixture | `TestDeclaredContentMod` registers nothing in `Bind`, declares an item, a recipe and a throwing item by attribute, and every TestNode loads it — so the registry, the catalog and the owner query are exercised against the real stack (the cross-mod conflict rule itself is pinned by `ModContentCatalogTests`) | `tests/CasualtiesUnknownOnline.Tests/Mods/TestDeclaredContentMod.cs`, `ModContentDeclarationTests` — 5 cases |
| 8 | The docs a mod author reads | the reference page's content section and the how-to page gained the attribute path (example, the three rules, ownership, what the scan refuses); both blocks, hashes re-recorded | `docs/en/reference/mod-api.md`, `docs/zh/reference/mod-api.md`, `docs/en/how-to/register-content.md`, `docs/zh/how-to/register-content.md`; `docs/standard/alignment.txt` (39 pairs recomputed, 2 changed) |
| 9 | The reviewed API baseline | three ADDED entries and no removal: `ModContentAttribute`, `ModContentContract` and its `TryGetKind` | `docs/contracts/abstractions-api-baseline.txt` — 1112 → 1115 lines, 1024 → 1027 entries, 75 tombstones unchanged; `ApiSurfaceGateTests` re-derived the delta itself |

## §2 Whole-family audit

Five measurements drove this change, each taken over the frozen tree rather than assumed:

1. **The scan adds a front-end, not a second path.** Every declaration it accepts goes through
   `IModContent.TryRegister`, so the permission rail, the id/kind/schema rails, the per-mod cap, the
   duplicate rule, the binder and every consumer stage A converted are untouched. The one new read of a
   declaration is the scan's own member sweep (`ModContentDeclarationScanner.Members`), which reads the
   contract's members plus `IModContentDefinition`'s three; nothing else in the tree reads a declaration
   differently than it did at HEAD.
2. **Ownership was derived from the two shapes the tree actually has.** `CasualtiesUnknownOnline.ModExample`
   declares `cuo.example` and `cuo.example.machine` in ONE assembly, and the test assembly declares dozens
   of fixture mods that must load together — so the ticket's "a second `[CuoMod]` in one assembly is
   refused and does not take effect" was unbuildable without taking the example and the whole test-mod
   architecture down. The declared half of the ticket's purpose (an unambiguous owner for a declaration)
   is served instead by nesting, and the ambiguous case is refused by name with the mods still loading.
   The nested member contracts handed to `docs/backlog/review/mod-content-nested-member-contracts.md` landed
   in the next cycle: every nested value type a kind contract names is an interface now, member types
   included, and decision 253 records the two calls that took (the collection members follow, and
   `RollCondition` left the mod surface rather than joining it).
3. **The base classes the ticket asked for cannot exist in that shape.** The first implementation attempt
   (nine `abstract` classes implementing only `Id`/`Kind`/`SchemaVersion`) failed the build with 148
   CS0535 errors — one per unimplemented interface member — because C# requires an abstract class to
   declare the members it does not implement. Restating ~20 members per kind to save three is not a
   convenience, and the shape that WOULD be convenient (virtual members defaulting to the data class's
   values) puts the same default in two places. Both the files and the idea are gone; §3 records it as a
   recorded decision rather than an omission.
4. **The refusal family is the code path's own rules, named earlier.** A null definition, an invalid id,
   a non-positive schema version, a duplicate id and the cap are still decided in `ModContentAdapter`;
   what the scan adds is the shapes only IT can see — a class that is not public/concrete/constructible,
   an open generic, more than one kind contract, none, a `Kind` that disagrees with the contract, a
   `[CuoMod]` class declaring content on itself, a class a discovery that never loads owns, and a
   constructor or member getter that throws. Every one of them is a log line naming the class, and none
   of them takes a sibling down — construction included, which is the guard the independent review found
   missing (§5 F1).
5. **A null collection is unaffected, and the census agrees.** The scan hands the object over without
   reading a collection's contents, and the null rule (decision 244's member half) is answered where it
   always was — `ModDeclarationCollections` at each read seam. `ModNullCollectionRuleTests` still finds
   its 26 constructed members and its interface/class split, unchanged.

## §3 What landed, what deliberately did not

**Landed.** The bare attribute; the scan with the census, the ownership plan and the refusals; the kind
mapping; the lifecycle wiring (census once, registration before `Bind`); the shared loadable-types helper;
the test families; the example mod as the production consumer; both documentation blocks; the re-reviewed
baseline; the new tickets that carry what this one does not.

**Not landed, on purpose** (each is recorded in the ticket's *Superseded and handed on* and in decision
252, not dropped):

- **"One assembly, one mod"** — superseded by nesting plus the single-mod-assembly rule, with the reason
  and the two real shapes above.
- **The optional per-kind base classes** — not built, for the compiler's reason measured in §2.3.
- **The content fingerprint** — moved with its acceptance row to `review/mod-content-fingerprint.md` (landed 2026-10-10, decision 254).
- **The nested member contracts** (`IModItemTool` and its thirteen siblings) — moved with the acceptance
  row that names them to `review/mod-content-nested-member-contracts.md`; the kind contracts still type
  those members as the framework's classes and the collection members as `List<ModT>`.

## §4 Verification

| Layer | What it proves | Result |
|---|---|---|
| Build | the contract, the scan and every consumer compile with warnings as errors | `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors |
| Format | the tree is the formatter's own output | `dotnet format CasualtiesUnknownOnline.slnx` — exit 0, and every new C# file is CRLF afterwards (measured byte-wise) |
| Focused suite | the declaration family, its registry, its binder, its catalog and the mod discovery around it | 153/153, driven by 15 filter conditions each written in full |
| Behaviour suite | nothing else moved | 4878/4878 (was 4854; the 24 new cases are this seam's — 17 scanner, 5 end-to-end, 2 contract; the review's own two findings added two of them) |
| **Ownership** | nesting decides an owner, the single-mod assembly owns a top-level declaration, a multi-mod assembly refuses one by name, an assembly with content but no mod reports it once, a mod class declaring content on itself is refused, and a declaration owned by a mod discovery never loads is refused too | `ModContentDeclarationScannerTests` — `Plan_*` and `Census_ReportsEachAssemblyLevelRefusalOnce` (7 cases, each asserting its log line) |
| **Refusals and isolation** | two kind contracts, no kind contract, a `Kind` that disagrees, a throwing member, a throwing CONSTRUCTOR, a non-public/abstract/ctor-less/open-generic class — each refused by name, each leaving its siblings registered | `ModContentDeclarationScannerTests.Register_*` (4 cases plus a 6-row theory); the throwing member also runs against the REAL registry in `ModContentDeclarationTests.AThrowingDeclaration_IsRefused_WhileItsSiblingsBind` |
| **The seam, end to end** | a mod that registers nothing in `Bind` still lands its declared classes in the registry, the catalog and the owner query, and its own bind already sees them | `ModContentDeclarationTests` — 5 cases over the real `TestNode`/`ModService` stack |
| **One list, four representations** | every `ModContentKind` constant has exactly one contract, and that contract's ready-made class reports the same kind | `ModContentContractTests` — 2 cases, deriving the constants and the contracts from the assembly |
| The contract's shape | the public surface is exactly what was reviewed | `ApiSurfaceGateTests` — red with three ADDED entries, green after the baseline was reviewed and updated (no removal, no tombstone) |
| The whole gate set | nothing else in the repository's rules moved | 573/573 on the frozen tree. The mid-cycle reds were this checklist's own boxes, the stale ticket path inside it, and — after the review's report file landed — that report's own missing MANIFEST row, all three resolved in this commit |

## §5 Independent review

Tier FULL, fresh context, against the frozen tree. Report:
`docs/evidence/selfchecks/mod-api/mod-content-declaration-binding-review.md`. **Verdict: no blocker** — one
major, two minors and four nits, every one dispositioned here and landing in this commit.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| F1 | major | The scan promised that a broken declaration never blocks its siblings, but `Activator.CreateInstance` sat OUTSIDE every guard: a constructor that throws left `Register`, landed in the per-mod discovery catch and skipped the whole mod — `Bind` included — while §2.4 listed "constructible" among the shapes that are only a named log line. | **Fixed in the code**, not in the wording: `TryCreate` gives construction its own named refusal (`threw while it was constructed — refused; the other declarations still bind`), so a declaration that cannot be constructed costs exactly that declaration. Pinned by `ScanFixtureMod.ThrowingConstructorItem` in the refusal theory. |
| F2 | minor | The record said the example declares two items by attribute (it declares ONE item and one recipe; `example.trophy` stays a code registration), and that `tools/deploy.ps1` deploys it — that script ships the plugin project's own output, and the repository's own records state the example DLL is copied by hand. | Both corrected in this record (§1 row 6, §6). The deploy half matters to the acceptance batch, which installs the example itself. |
| F3 | minor | A declaration owned by a `[CuoMod]` type discovery can never load (not public, abstract, not constructible) was planned and then dropped in silence: the registry's candidate filter never reaches a skip line for such a class, so no other line reported it. | **Fixed in the scan**: an owner that is not a DISCOVERABLE mod (the structural subset of the registry's own candidate filter, `IsDiscoverableMod`) has its declarations refused with one line per owner naming it and the count, and they are not planned. Pinned by `Plan_RefusesDeclarationsOwnedByAModDiscoveryNeverLoads`. |
| F4 | nit | Three figures did not reproduce: §1's case split (13 + 10), §4's "5 cases, each asserting the log line" (the class has 7 ownership cases, two of which assert that NOTHING is logged), and the checklist's "§4 (nine verification layers)" (§4 has ten rows). | All three corrected against the tree. |
| F5 | nit | `IModContent.TryRegister`'s summary still said "Register during `ICuoMod.Bind`" while the framework now registers a mod's declarations just before that bind. | Rewritten: a mod registers during `Bind`, and the framework registers a mod's `[ModContent]` declarations through the same method just before it. |
| F6 | nit | §1 row 7 claimed the end-to-end fixture exercises "the cross-mod conflict rule", which the new tests only observe as `HasConflicts == false`. | Reworded to what the fixture does; the conflict rule stays pinned by `ModContentCatalogTests`. |
| F7 | nit | Two still-`current` records pointed at the ticket path that moved from `todo/` to `review/` — the reference gate exempts `evidence/selfchecks/` by design, so it stayed green. | Both record texts now name the moved path and note the move; no finding was rewritten. |

The review reproduced build 0/0, behaviour 4876/4876 (decomposed 2 + 15 + 5 at the freeze), gates 573/573 at
the time it ran, the baseline delta (exactly three ADDED entries, 1024 → 1027 entries, 75 tombstones, +3
lines and no removal), all 39 alignment pairs with the two changed ones being exactly the pairs this change
edited, every file line count §1/§4 states, the CS0535 figure (148 = the nine contracts' own member counts),
and CRLF on every new C# file with no bare LF. It could NOT reproduce the focused run's 15 filter conditions
from the tree (none is recorded there; the full suite is the stronger evidence), could not re-produce the
mid-cycle 571/573 (the frozen tree reads 573/573), and could not check anything needing a real game — whether
the example's item and recipe reach the game's own tables, or whether `rippeddressing` is a real vanilla id —
nor `dotnet format` (forbidden against a frozen tree).

## §6 Limits

- **No game process ran.** The attribute path's runtime evidence is the registry/catalog/owner-query chain
  over the real mod stack plus the bind boundary the stage A suite drives; that an example item and recipe
  reach the GAME's tables is the acceptance batch's row, and the example mod now carries them for it.
- **The "content but no mod" report is proven at the `Plan` seam**, with an explicit type list, because
  this repository's test process has no assembly that declares `[ModContent]` and no mod: both test
  assemblies declare mods. The refusal itself, its wording and its once-per-assembly caching are the same
  code path production calls.
- **A getter that throws only on a LATER read is not bounded.** The scan reads each member once and hands
  the instance over; a member whose second read throws (stateful computation) would still throw inside a
  provider. What the scan closes is the shape the ticket named — a declaration that cannot be read at all
  — and, since the review of this cycle (§5 F1), a declaration that cannot be CONSTRUCTED, which used to
  take the whole mod down with it.
- **The scan reads every `[ModContent]` type in an assembly, including non-public ones.** That is what
  makes a private declaration inside a mod refused by name instead of silently ignored; the cost is that
  a private declaration with no owner is also reported as ownerless in a multi-mod assembly.
- **Ownership counts every `[CuoMod]` DECLARATION, and an owner discovery cannot load is refused by the
  scan.** The count is read from the declarations rather than from the validated manifests, so an
  assembly whose second mod is rejected for a manifest rule still makes its top-level declarations
  ambiguous — fail-closed, and the registry logs that rejection by name anyway. An owner that is not even
  a discovery CANDIDATE (not public, abstract, not constructible with no arguments) is refused by the
  scan with one line per owner, which is the shape no other line reports; a declaration owned by a mod
  that passes the candidate filter and then fails a manifest rule is planned and never registered, and
  the signal there is the registry's own skip line for that mod.
- **The fingerprint and the nested member contracts are not here**; §3 names the two tickets that own
  them and the acceptance rows moved with them.
- **The example's declared content is not exercised in a real game yet, and it is not installed by
  `tools/deploy.ps1`.** That script copies the plugin project's own output (the plugin, Runtime,
  Abstractions and the NuGet runtime assemblies), and `CasualtiesUnknownOnline.ModExample.dll` is a
  plugin of its own — the acceptance batch installs it beside the framework, as the repository's records
  for the declared-packet and typed-registration cycles already state.
