# Content kinds with no provider — mechanism inventory and self-check

Owner cycle: `review/mod-content-kind-with-no-provider.md` (2026-10-07), cut from
`todo/mod-content-ceiling.md` Part 3 B — the second of its two first-in-line entries, after
`review/mod-crafting-quality-labels.md`.

Decision (entry 245): the framework's content-kind vocabulary names EXACTLY the kinds a CUO provider
binds, and a registration nothing can bind is answered at load time by the runtime binder. Both halves
are needed: the vocabulary change removes the names a mod could not have seen materialized, and the
binder's warning answers a kind written as a literal or invented by a mod.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | The two lists that had drifted | `src/CasualtiesUnknownOnline.Abstractions/ModContentKind.cs` declared twelve constants; `src/CasualtiesUnknownOnline.GameAdapter/GameAdapterComposition.cs` registered nine `IContentBindingProvider`s, and each provider's `Kind` member was one of the other nine constants (checked provider by provider with a source scan). Nothing compared the two lists: no gate, no test, no table |
| 2 | What the three provider-less names were | `Setting` and `Locale` appeared nowhere else under `src/` at all, and the capabilities behind them are ruled out of CUO core by `docs/backlog/review/cucorelib-migration-support.md` ("**Out of scope for CUO core.** Settings are local UX"; "**Out of scope / mod-local.** Localization is a normal mod packaging concern"). `Entity` had exactly one other consumer, `BuiltInResourceLocationSource`, which labels CUO's own `cu:player` selector id with it |
| 3 | Why a registration under them succeeded | `ModContentPolicy.IsValidKind` is `!string.IsNullOrWhiteSpace(kind) && kind!.Length <= MaxKindLength` — the policy rails the id and the kind's SHAPE, never its membership, because kinds are documented as mod-defined tags. `ModContentAdapter.TryRegister` returns `true` and logs `"[Mods] {ModId} registered content {Id} ({Kind}, schema {SchemaVersion}, {Length} bytes)."` at Information |
| 4 | The only word on a provider-less registration | `ModContentBinder`'s map miss was `log.LogDebug("[ModContentBinder] no provider for content kind {Kind}; {ModId}/{Id} stays opaque.")`, while both sibling branches in the same loop (a mod that is not a shared-content mode; a provider that refused the definition) are Warnings. At the default log level the mod author saw a success line and then nothing |
| 5 | Where the provider set lives, and where registration happens | the binder builds its map from the DI-registered `IEnumerable<IContentBindingProvider>` in one method; the per-mod `ModContentAdapter` is constructed from a manifest and a logger and holds no provider knowledge. The map is therefore the only place that can judge a registration against what CUO can bind — which is why the fix is the binder's warning and not a registration-time refusal |
| 6 | What a provider-less registration still reaches | `ModContentCatalog` (kind/id resolution and conflict diagnostics), `IModContentOwnerQuery`, `ModContentResourceLocationSource` (the console's canonical-id vocabulary, whose display name falls back to the registered id through `ModContentDisplayName.Resolve`), and the binder's own skip. None of them materializes anything — the entry is discoverable, not present |
| 7 | How a resource entry's kind is consumed | `ResourceLocationEntry.Kind` is the source's own label; the only reader under `src/` renders it into the suggestion text (`CommandConsoleService` builds `$"{entry.Kind} · {entry.DisplayName}"`). The type is `[ApiStability(Experimental)]` and is handed to a mod's own match stage, which may read the word — the reason the built-in entry's `entity` is a resource-vocabulary word and not a `ModContentKind` |
| 8 | The vocabulary is a gated public surface | `docs/contracts/abstractions-api-baseline.txt` recorded all twelve constants as `Stable` members, and its own header states the rule: "A removal deletes the line AND adds a tombstone, `*REMOVED* <key> — <reason>`" (`ApiSurfaceGateTests`) |

## 2. Whole-family audit

| Surface | What was checked | Outcome |
|---|---|---|
| `ModContentKind`'s twelve constants | each against the nine providers | nine kept (one provider each); `Entity`, `Setting`, `Locale` removed, each with its own tombstone reason |
| Every other consumer of a removed constant | a repository-wide scan for `ModContentKind.Entity`, `.Setting`, `.Locale` | one hit: `BuiltInResourceLocationSource`; `Setting` and `Locale` had none. The built-in source now carries `PlayerKind` itself, with the console's displayed word unchanged |
| `ModContentDisplayName` | its switch over the vocabulary | reads eight of the nine kinds; `Recipe` deliberately falls back to the registered id, unchanged by this cycle |
| `ModContentResourceLocationSource` | whether it should also skip a provider-less kind | it keeps listing it: the console vocabulary answers "what did the mod register", the binder answers "will it exist". The distinction is now stated on the mod API page instead of implied |
| The nine providers | class ↔ registration statement ↔ constant, in both directions | all nine agree (the gate asserts it) |
| Docs stating the old vocabulary | both blocks of `reference/mod-api.md` and `how-to/register-content.md`, the umbrella, and every other live document that printed the twelve-kind list | updated — including `review/cucorelib-migration-support.md`'s implemented-base list (the very ticket this cycle cites as its authority, found by the independent review after the first sweep missed it) and `todo/item-and-entity-data-commands.md`'s related-surface clause |
| Tests and tools naming a removed constant | a repository-wide scan | none; the change compiled with no test edits beyond the binder fact and the new built-in source fact |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| The vocabulary cannot promise a kind nothing binds | `ModContentKind`'s public string constants equal the kinds the provider classes declare, one name per kind | `ContentKindProviderCoverageGateTests.TheVocabulary_NamesExactlyTheKindsTheProvidersDeclare` |
| A provider nothing names binds nothing | every type declaring `IContentBindingProvider` under `src/` (directly or through the file's alias for it) is named by a DI registration statement, and every registration names such a type | `ContentKindProviderCoverageGateTests.EveryProvider_IsNamedByARegistrationStatement` |
| The load-time answer | a registration whose kind no provider claims is reported at Warning level, naming the kind and the definition | `ModContentBinderTests.UnknownKind_IsReportedAsNeverMaterialized` (recording logger; the kind-less entry is a red before the change) |
| The built-in resource keeps its label | `cu:player` still carries the kind word `entity` and the localized `content.cu_player` name, from its own constant | `BuiltInResourceLocationSourceTests.Entries_CarryThePlayerId_WithTheEntityKindAndTheLocalizedName` + `...Entries_TakeTheDisplayNameFromTheLocalizationCatalogue` |
| The removal is recorded, not silent | three baseline tombstones, each naming its own reason | `docs/contracts/abstractions-api-baseline.txt`; `ApiSurfaceGateTests` |
| Each census reads what it claims | the vocabulary census, the provider census and the registration census each have their own matcher samples, positive and negative — including the alias spelling that the independent review proved was invisible to both before the fix | the four `[Theory]` facts in the gate (28 sample rows) |

## 4. Verification design

- The gate reads SOURCE, because the adapter's providers are not in the test host: the Runtime composes
  without the Game Adapter (only the plugin composes it), which is why the existing binder fact injects its
  own provider. The census is therefore taken from the three places that decide the outcome — the vocabulary
  file, each provider class's own `Kind` member, and the registration statements under `src/` — and each of
  the three carries its own census floor *inside the same failure list as the semantic comparisons*, so a
  count-reducing mutation reports both the floor line and the reason it happened instead of only the floor.
- The load-time half drives the production path (`ModContentBinder` over a fake control and a fake manifest
  set) with a recording logger, so "the entry is reported" is read from the log rather than inferred from the
  absence of a throw — the case that used to assert only `// no throw` — and it pins the branch's other half
  by asserting the provider was handed nothing.
- Six mutation controls, each expected to turn one named case red, each restored byte-identically (SHA-256
  compared) with the affected suites re-run green afterwards: a constant added without a provider (M1), a
  registration deleted (M2), the binder's warning reverted to Debug (M3), a provider's `Kind` pointed at
  another kind (M4), a provider and its registration spelled through an alias of the interface (M5, the hole
  the independent review found), and a second constant carrying a declared kind's value (M6, its second).
- No session is needed for any row: every behaviour here is load-time and client-local.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| Focused, each with the exact filter that produced it | `--filter "FullyQualifiedName~ContentKindProviderCoverageGateTests"` 30 passed / 0 failed (2 facts + 28 theory rows); `~ModContentBinderTests` 6; `~BuiltInResourceLocationSourceTests` 2; `~ApiSurfaceGateTests` 11; `~BacklogIntegrityGateTests` + `~BacklogReferenceGateTests` + `~DocumentationTreeGateTests` + `~SelfcheckManifestGateTests` 35 |
| `dotnet test CasualtiesUnknownOnline.slnx` (with build) | behaviour 4796 passed / 0 failed (4794 before this cycle, +2 for the built-in source fact); gates 480 passed / 1 failed / 481 — the failure is `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`, i.e. the checklist gate refusing the not-yet-checked build box, not a defect |
| `dotnet format CasualtiesUnknownOnline.slnx` | exit 0, run after the review's findings were fixed |
| Mutations | M1 `ModContentKind.Entity = "entity"` added back → the vocabulary fact red: "'Entity = \"entity\"' is declared but no provider binds that kind"; M2 the Moodle registration deleted from `GameAdapterComposition` → the registration fact red with both lines: "the registration census read 8 `IContentBindingProvider` registration(s); the measured registrations are 9" and "'GameAdapterMoodleContentProvider' declares `IContentBindingProvider` but no registration reaches it"; M3 the binder's warning reverted to the Debug line it replaced → `ModContentBinderTests.UnknownKind_IsReportedAsNeverMaterialized` red; M4 `GameAdapterMoodleContentProvider.Kind` repointed at `ModContentKind.Status` → the vocabulary fact red with the duplicate-kind line, the provider-count line and "'Moodle = \"moodle\"' is declared but no provider binds that kind"; M5 a provider and its registration spelled through `using Cbp = …IContentBindingProvider;` → the vocabulary fact red: "AliasKindProbeProvider binds kind 'aliasprobe', which …ModContentKind.cs does not declare" (the hole the independent review proved was green before this fix, and the probe file was deleted after the control); M6 a second constant carrying a declared kind's value → the vocabulary fact red: "'Item', 'ItemAlias' all declare the value 'item'". Each control turned exactly one named case red; every mutated file was restored byte-identically (SHA-256 compared before and after) and the affected suites re-ran green |

## 6. Independent review

Adversarial review in a fresh context against the frozen tree, read-only, FULL tier: **0 blocker, 1 major,
4 minor, 8 nit**, every finding fixed in the same commit. The full report is
`%TEMP%\cuo-review-mod-content-kind-coverage.md` (not committed). What it found and what changed:

- **major — a live document the cycle itself cites still printed the old vocabulary.**
  `review/cucorelib-migration-support.md`'s implemented-base list still named `setting` and `locale`; its
  list and this cycle's swept-docs row are corrected.
- **minor — two quoted mutation messages did not occur.** The census floors sat exactly at the measured
  counts and were asserted before the semantic comparisons, so a count-reducing mutation reported only the
  floor. The floors now live in the same failure list as the semantic checks, and the six controls above
  record the messages that actually occur.
- **minor — an interface alias was invisible to both censuses.** A provider spelled through
  `using Cbp = …IContentBindingProvider;` and registered as `AddSingleton<Cbp, …>()` passed 27/27 while
  binding a kind outside the vocabulary. Both censuses now resolve a file's own aliases (they are the
  spelling this repository's conventions prefer), the matcher samples cover it, and M5 pins it.
- **minor — a second constant carrying a declared kind's value passed.** "One name per kind" is now
  asserted, with M6 as its control.
- **minor — the registration census proves a statement, not an executed composition.** The test name, the
  gate's doc comment, the ticket and decision 245 now say "named by a registration statement under `src/`"
  and name the limit instead of implying the running container.
- **nits** — the gate's doc comment cited the ticket in `todo/`; `ResourceLocationEntry.Kind`'s public doc
  still called it "the owning content kind" although the change makes it the source's own label;
  the self-check's composite "44 doc-gate cases" was not reproducible (now four named filters);
  a live todo ticket still listed "the content kinds with no provider" as a related surface; the binder fact
  did not pin the branch's skip half (`Assert.Empty(provider.Bound)` added); the binder's single load-time
  pass was missing from *Limits*; and "no consumer routes on it" was a claim about `src/` while the entry is
  handed to a mod's own match stage — all fixed, and the index row was shortened for headroom.

## 7. Limits

- **The compile-time half is a mod-side fact this repository cannot execute.** What is asserted is that the
  vocabulary no longer declares the three names; that a third-party mod writing `ModContentKind.Locale`
  now fails to build follows from the constant being gone, not from a test here.
- **Row 1 is judged against a recording logger, not against a real client's log file.** Every client runs
  its own binder, so each client's own log carries its own copy of the warning; how that line looks in
  `BepInEx\LogOutput.log` is not observed by this cycle.
- **The gate reads sources, not the built container.** A registration or `Kind` member whose shape the
  census cannot resolve is reported as a failure rather than skipped (a `Kind` that names the vocabulary
  through an alias of `ModContentKind` is reported, not resolved), and a provider that inherits the
  interface through another type without naming it is out of the declaration census — the registration half
  then fails the set comparison. The census also proves only that a registration STATEMENT names the
  provider: a statement inside an extension method nothing calls satisfies it, because which statements a
  container executes is not a syntax fact. Neither shape exists in this tree.
- **The binder answers once, at its single load-time pass.** `ModContentBinder.Update` returns immediately
  after its first run, so a definition registered after that frame is neither bound nor warned; the ticket's
  *Limits* names this window.
- **A provider that accepts a definition and then declines to materialize it** keeps its own refusal with its
  own reason (already a Warning naming the definition); this cycle closes only the case where no provider is
  reached at all.
- **The console's resource vocabulary still lists a provider-less registration** under its canonical id. That
  is deliberate (discovery, not materialization) and now stated on both mod API pages; nothing here changes
  what the console offers.
