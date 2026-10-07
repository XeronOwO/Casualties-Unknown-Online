# Content kinds with no provider: the vocabulary names what binds, the binder answers for the rest

- Status: Review — cut 2026-10-07 from `todo/mod-content-ceiling.md` Part 3 B (the second of its two
  first-in-line entries; the quality reference was the first, landed as
  `review/mod-crafting-quality-labels.md`). The development half landed 2026-10-07 (see *What landed*);
  every acceptance row is judged by this repository's own suite, because all of them are load-time and
  client-local. What a real client's log file shows is named in *Limits*, not claimed here.
- Priority: Medium-High — a defect of a surface the API already promises, so it is filed ahead of the
  umbrella's capability entries (Part 3 C), as that ticket's promotion rule requires.
- Category: Mod platform / content surface
- Related: `todo/mod-content-ceiling.md` (the inventory: this is Part 3 B's kind-versus-provider entry),
  `review/mod-crafting-quality-labels.md` (the sibling entry and the shape it closed),
  `docs/en/reference/mod-api.md` + `docs/zh/reference/mod-api.md` (the user-facing rule),
  `docs/backlog/review/cucorelib-migration-support.md` (which rules settings and locale out of CUO core),
  `docs/contracts/abstractions-api-baseline.txt` (the three removals are recorded there with tombstones)

## What was asked

The umbrella's Part 3 B measured one defect with two halves:

1. **The framework's content-kind vocabulary advertises kinds nothing binds.** `ModContentKind` declares
   twelve constants; the Game Adapter registers nine content providers, whose kinds are the other nine
   constants. `Entity`, `Setting` and `Locale` have no provider at all, so the vocabulary reads as a list
   of kinds a mod may register — and three of them can never exist in the game.
2. **A registration under a kind nothing binds is accepted in silence.** `TryRegister` returns `true`,
   the registry logs at Information that the content was registered, and the binder's only word on the
   entry is a Debug line. At the default log level a mod author sees a success and then nothing: the
   definition never materializes and nothing says so.

## Mechanism (read before implementing)

| Fact | Evidence |
|---|---|
| The vocabulary is twelve constants; the adapter binds nine kinds | `src/CasualtiesUnknownOnline.Abstractions/ModContentKind.cs` declares `Item`, `Recipe`, `Liquid`, `LiquidTile`, `Tile`, `Building`, `Entity`, `Structure`, `Status`, `Moodle`, `Setting`, `Locale`; `src/CasualtiesUnknownOnline.GameAdapter/GameAdapterComposition.cs` registers nine `IContentBindingProvider`s, and each provider's `Kind` is one of the other nine constants |
| `Setting` and `Locale` appear nowhere else under `src/` | the only mentions of either name are the constants themselves; the capabilities behind them are ruled out of CUO core by the migration ticket — `docs/backlog/review/cucorelib-migration-support.md`: settings are "**Out of scope for CUO core.** Settings are local UX; a future `IModSettings` or settings-menu seam is acceptable only if demand appears", and localization is "**Out of scope / mod-local.** Localization is a normal mod packaging concern. CUO should not own locale …" |
| `Entity`'s only other use is a RESOURCE label, not a registration | `src/CasualtiesUnknownOnline.Runtime/Session/Content/BuiltInResourceLocationSource.cs` builds `new(PlayerId, ModContentKind.Entity, localization.T("content.cu_player"))` — the kind word of the built-in `cu:player` selector id |
| A resource entry's kind is a display label with no in-repo consumer that routes on it | `ResourceLocationEntry.Kind` is documented as the source's own label; the only reader under `src/` renders it into the suggestion text (`CommandConsoleService` builds `$"{entry.Kind} · {entry.DisplayName}"`), and the entry itself is handed to a mod's own match stage (`IResourceLocationMatchStage`), which may read the word |
| Registration accepts ANY kind | `ModContentPolicy.IsValidKind` is `!string.IsNullOrWhiteSpace(kind) && kind!.Length <= MaxKindLength` — membership of `ModContentKind` is not checked, because kinds are documented as mod-defined tags |
| A successful registration is what the mod author sees | `ModContentAdapter.TryRegister` logs `"[Mods] {ModId} registered content {Id} ({Kind}, schema {SchemaVersion}, {Length} bytes)."` at Information and returns `true` |
| The only word on a provider-less registration is a Debug line | `ModContentBinder`'s map miss logs `"[ModContentBinder] no provider for content kind {Kind}; {ModId}/{Id} stays opaque."` at Debug, while its two sibling branches (a mod that is not a shared-content mode, a provider that refused) are Warnings |
| The provider set IS reachable at bind time, and the per-mod registry is not | `ModContentBinder` builds its map from the DI-registered `IEnumerable<IContentBindingProvider>` in one place (`BuildProviderMap(providers, log)`), which is the composition root's own declaration; the per-mod `ModContentAdapter` is constructed from a manifest and a logger and knows nothing about providers |
| What a provider-less registration still reaches | `ModContentCatalog` (kind/id resolution and conflict diagnostics), `IModContentOwnerQuery`, `ModContentResourceLocationSource` (the console's canonical-id vocabulary, whose display name falls back to the registered id through `ModContentDisplayName.Resolve`), and the binder's own skip. None of them materializes anything |
| The vocabulary is a gated public surface | `docs/contracts/abstractions-api-baseline.txt` records all twelve constants as `Stable` members; the baseline's own header states "A removal deletes the line AND adds a tombstone, `*REMOVED* <key> — <reason>`", enforced by `ApiSurfaceGateTests` |
| Nothing ties the vocabulary to the providers | no gate, test or table compares `ModContentKind`'s constants with the kinds the composition registers, which is how the two lists drifted apart in the first place |

## Design (as landed)

1. **The vocabulary names exactly the kinds CUO binds.** `Entity`, `Setting` and `Locale` leave
   `ModContentKind`, each with its own reason: settings and locale are ruled out of CUO core by the
   migration ticket, and `Entity` is a resource-vocabulary word for a capability the umbrella parks
   ("custom enemies" needs new capability and has no consumer) rather than a registration kind. A mod
   that wants a kind of its own still registers a literal tag — that half of the surface does not change.
2. **The built-in resource keeps its word, from its own home.** `cu:player`'s `entity` label is a
   resource kind for a CUO-owned id, not a registration kind, so `BuiltInResourceLocationSource` carries
   the constant itself and says so. The console's suggestion text is unchanged.
3. **The binder answers for every registration it cannot route.** The no-provider branch becomes a
   Warning naming the kind, the definition and the consequence — the registry took it, the catalog and
   the console will enumerate it, and no CUO layer will ever materialize it. This is the half that
   survives the vocabulary change: a mod that writes a removed kind as a literal, or invents one, still
   gets told instead of getting silence.
4. **A gate ties the three censuses together** so the two lists cannot drift again: the constants
   `ModContentKind` declares (with one name per kind), the kinds the `IContentBindingProvider` classes
   report, and the providers a DI registration statement names under `src/` — under whatever `using`
   alias a file spells the interface with, since the repository's convention prefers aliases over
   qualified names and an alias would otherwise be invisible to both censuses. A provider class that
   exists but is named nowhere binds nothing, so it is part of the same census; whether the statement
   that names it is the one a running container executes is not a syntax fact, and the gate says so
   instead of implying it.

## What landed

- `src/CasualtiesUnknownOnline.Abstractions/`: `ModContentKind` keeps the nine kinds a provider binds
  (`Entity`, `Setting`, `Locale` removed) and its own documentation now states what the vocabulary is —
  the kinds the framework materializes — and what a kind outside it means.
- `src/CasualtiesUnknownOnline.Runtime/Session/Content/`: `BuiltInResourceLocationSource` carries the
  console's `entity` word as its own constant, documented as a resource-vocabulary label rather than a
  registration kind.
- `src/CasualtiesUnknownOnline.Runtime/Session/Mods/`: `ModContentBinder`'s no-provider branch logs at
  Warning level, naming the kind and the definition and saying it is never materialized.
- `tests/`: `ContentKindProviderCoverageGateTests` (the three-way census with floors and matcher
  samples), `ModContentBinderTests.UnknownKind_IsReportedAsNeverMaterialized` (the red this cycle fixed,
  read from a recording logger) and `BuiltInResourceLocationSourceTests` (the console label and its
  localized name).
- `docs/`: the mod API page in both blocks, the register-content how-to in both blocks, the pair
  registry, the API baseline (three tombstones), the rule-to-gate map, the self-check MANIFEST, the
  umbrella's Part 3 B / layer table / promotion rule / open question, the cycle's self-check, and the
  other live documents that still printed the old vocabulary
  (`review/cucorelib-migration-support.md`'s implemented-base list, `todo/item-and-entity-data-commands.md`'s
  related-surface clause).
- Decision 245 records the rule and the rejected alternative.

## Acceptance

| # | Row | Judged by | Verdict |
|---|---|---|---|
| 1 | A registration under a kind no provider binds is reported at load time, naming the kind and the definition, instead of passing in silence | `ModContentBinderTests.UnknownKind_IsReportedAsNeverMaterialized` drives the production binder with a recording logger and reads the warning's own text (kind, `mod/id`, "never materialized"); red before the change | Verified 2026-10-07 |
| 2 | The framework's content-kind vocabulary names exactly the kinds a provider claims, one name per kind, and every provider is named by a registration statement | `ContentKindProviderCoverageGateTests.TheVocabulary_NamesExactlyTheKindsTheProvidersDeclare` (both directions, plus a duplicate-kind case and a second name for a declared kind) and `...EveryProvider_IsNamedByARegistrationStatement` (declaration ↔ registration, both directions, interface aliases resolved) | Verified 2026-10-07 |
| 3 | A mod can no longer name the three removed kinds through the API | the three constants are gone from `ModContentKind` (the gate's vocabulary census reads what is there, and the removal was a build break everywhere it was referenced — one site), with three reasons recorded as tombstones in `docs/contracts/abstractions-api-baseline.txt` | Verified 2026-10-07 |
| 4 | The nine bound kinds are unchanged: each still has its provider, its kind word and its materialization path | the gate pins the nine-way correspondence; the existing provider and binder suites stay green in the full run | Verified 2026-10-07 |
| 5 | The console's built-in `cu:player` entry keeps its kind word and its display name | `BuiltInResourceLocationSourceTests.Entries_CarryThePlayerId_WithTheEntityKindAndTheLocalizedName` and `...Entries_TakeTheDisplayNameFromTheLocalizationCatalogue` | Verified 2026-10-07 |

## Non-goals

- Not a provider for `entity`, `setting` or `locale`: those are the umbrella's Part 3 C capabilities
  (custom enemies; settings; locale, audio and art resources), each needing new capability and a real
  consumer. When one of them lands, its constant returns with its provider.
- Not Part 2's stage 2 (semantic predicates plus target-local execution) and not stage 3.
- Not the console's resource vocabulary: it still lists every registration with a canonical id,
  including one whose kind nothing binds. That is a discovery surface ("what did the mod register"),
  not a materialization promise; the load-time warning is what separates the two.

## Limits

- **The compile-time half of row 3 is a mod-side fact this repository cannot execute**: what is asserted
  here is that the vocabulary no longer declares the three names, not that a third-party mod fails to
  build. The tombstone is the record; the mod API page is where a mod author learns what to use instead.
- **Row 1 is a load-time, client-local behaviour** judged by the suite against a recording logger. Every
  client runs its own binder, so each client's log carries its own copy of the warning; nothing here is
  judged in a two-client session, and no wire, save or protocol surface is touched.
- **The binder answers once, at its single load-time pass.** `ModContentBinder.Update` returns
  immediately after its first run, so a definition registered later is neither bound nor warned — that is
  the registration window the how-to page already tells a mod author it owns ("register in `Bind` … a
  definition registered later is a race you own"), and the warning should not be read as covering it.
- **The gate reads SOURCE, not the built container.** The test host composes the Runtime without the Game
  Adapter (which only the plugin composes), so the provider set is read from the registration statements
  under `src/` plus each provider's own `Kind` member, in the style of the other composition-root pins.
  Two consequences are named rather than hidden: the census proves that a registration statement names the
  provider, not that the running composition executes it (a statement inside an extension method nothing
  calls satisfies it — the only registrations in this tree are the composition root's nine), and a
  registration or `Kind` member whose shape the census cannot read is reported as a failure rather than
  ignored.
- **A resource entry's kind word is display text in the framework, not a sealed vocabulary.** No CUO
  consumer routes on it, but `ResourceLocationEntry` is an `[ApiStability(ApiStabilityLevel.Experimental)]`
  mod-facing type and is handed to a mod's own match stage, which may read it. The built-in entry's word is
  unchanged (`entity` before and after), so nothing breaks; this is stated because "no consumer routes on
  it" is a claim about `src/`.
- **A provider that accepts a definition and then declines to materialize it** is a different story and
  stays each provider's own refusal (already a Warning naming the definition). This ticket closes the
  case where no provider is reached at all.
