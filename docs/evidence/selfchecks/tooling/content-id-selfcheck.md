# Namespaced content id + resource-location completion — self-check

Delivery fact sheet for the bundled backlog pair
`docs/backlog/review/id-system-namespaced-ids.md` +
`docs/backlog/review/command-id-name-completion.md`: CUO content identity is
now one canonical `namespace:path` vocabulary (`cu` built-in, mod-declared
namespaces), and the console's `ResourceLocation` argument completes by
canonical id, bare content id, or the localized display name — always inserting
the canonical id.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Id value type | `ContentId` (Abstractions) — `TryParse`/`TryCreate`/`IsValidNamespace`/`IsValidPath`/`Parse`, value equality, ordinal ordering, canonical `ToString`. Grammar `[a-z][a-z0-9_]{0,31}` / `[a-z0-9][a-z0-9_.-]{0,95}` derived from the vanilla item-id charset (`reversing/Assembly-CSharp/Assembly-CSharp/Item.cs`, 218 ids, `a-z0-9`) and the existing 128-char content-id cap. |
| 2 | Built-in namespace | `ContentId.BuiltInNamespace = "cu"`; `cu:fentanyl` is the canonical example from the ticket. |
| 3 | Mod namespace declaration | `[CuoMod(..., Namespace = "mymod")]` → `ModManifest.Namespace` (`CuoModAttribute.cs`, `ModManifest.cs`, `ModRegistry.cs:94-135`). Declared, not imperatively registered: the manifest is already the single declared source and discovery is the existing per-mod fail-closed validation point. |
| 4 | Discovery validation | `ModRegistry.Discover` rejects an invalid namespace (including empty/whitespace), the reserved `cu`, and — after dependency ordering — a namespace another **loaded** mod already owns. Rejection is tracked per candidate: the dependency closure runs before the duplicate-id pass, so a candidate rejected for a missing/cyclic dependency never consumes an id or a namespace, and a dependent of a namespace-rejected mod is dropped (`ModRegistry.cs` `OrderByDependencies`/`ClaimNamespaces`). |
| 5 | Canonical mod content id | `ModContentRegistration.TryGetCanonicalId` → `<namespace>:<registration id>`; `IdentityKey` (canonical or legacy bare id) drives conflicts and resolution (`ModContentRegistration.cs`, `ModContentCatalog.cs`). |
| 6 | Content id grammar rail | `ModContentPolicy.IsValidId` = `ContentId.IsValidPath`: lower-case ASCII, no `:`, ≤95 chars (`ModContentPolicy.cs`). |
| 7 | Catalog merge | `ResourceLocationCatalog` merges every `IResourceLocationSource` by canonical id, first source wins, deterministic id order (`ResourceLocationCatalog.cs`). |
| 8 | Matching/ranking | exact id > canonical prefix > bare path prefix > display-name prefix, then id order, cap 20 (`ResourceLocationCatalog.MatchRank`/`Suggest`). |
| 9 | Built-in source | `BuiltInResourceLocationSource` → `cu:player` with the localized `content.cu_player` name; keeps the selector `type=cu:player` vocabulary alive. |
| 10 | Vanilla game source | `VanillaItemResourceLocationSource` (GameAdapter) → `cu:<Item.GlobalItems id>` with `ItemInfo.fullName`, which the game assigns from `Locale.GetItem(id)` (`Item.cs:7096`) — the localized/common name the ticket requires. Ids the item provider actually injected are excluded via `GameAdapterItemContentProvider.InjectedItemIds` (a mod definition that collides with a vanilla id is never injected, so the vanilla `cu:<id>` stays). |
| 11 | Mod content source | `ModContentResourceLocationSource` (Runtime) → `<namespace>:<id>` for namespaced registrations, display name from the typed DTO (`ModContentDisplayName`), legacy namespace-less registrations skipped, and a bare id registered by several mods skipped (the game table can hold only one). |
| 12 | Console projection | `CommandConsoleService.SuggestResourceLocations` maps entries to `CommandSuggestion(canonicalId, "kind · displayName")`; the static phase-18 `ConsoleResourceLocationCatalog` is deleted. |
| 13 | Selector vocabulary | `cuo:player` → `cu:player` in `CommandSelectorSuggestions` and `CommandSelectorFilter.IsTypeMatch` (the `cuo:` placeholder vocabulary is gone). |
| 14 | DI wiring | Runtime registers the built-in + mod-content sources and the catalog (`CuoBootstrap.cs`); the plugin registers the Game Adapter vanilla source (`PluginDependencyRegistrar.cs`). |
| 15 | No wire change | Namespaces/content bytes stay process-local; `ModRegistry.CurrentModInfos()` still carries only id/version/mode/permissions, so no `NetMsg`/`ProtocolVersion` change. |

## 2. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Id grammar accept/reject | canonical/upper-case/whitespace-trimmed ids parse; `""`, `cu`, `:x`, `cu:`, `cu:a:b`, inner spaces, `/`, non-ASCII, leading digit/underscore namespace, over-length parts are refused | `ContentIdTests` (15 theory rows + 9 facts) |
| Id normalisation | `CU:Fentanyl` → `cu:fentanyl`; `TryCreate` normalises both parts but does not trim them; `IsValidPath` stays strict lower-case for registered ids; `default(ContentId)` hashes/formats safely | `ContentIdTests.TryParse_AcceptsAndNormalisesCase`, `TryCreate_NormalisesCaseAndRefusesInvalidParts`, `DefaultId_IsSafeToHashAndFormat` |
| Id equality/order | value equality, hash equality, ordinal `CompareTo`, `Parse` throws on invalid | `ContentIdTests.Equality_IsValueBased`, `Parse_ReturnsCanonicalIdOrThrows` |
| Manifest namespace | a declared namespace is carried in the manifest; an undeclared mod has null | `ModDiscoveryTests.DeclaredNamespace_IsCarriedInTheManifest` |
| Reserved/invalid namespace | `cu`, `Test NS!` are rejected at discovery | `ModDiscoveryTests.ReservedOrInvalidNamespace_Rejected` |
| Duplicate namespace | only the first surviving declaration wins; a mod rejected for a dependency never consumes its namespace | `ModDiscoveryTests.DuplicatedNamespace_OnlyFirstWins`, `RejectedMod_DoesNotConsumeItsNamespace` |
| Rejected candidate identity | a rejected candidate also never consumes its **mod id**: the first still-loadable declaration wins (dependency closure runs before the duplicate-id pass) | `ModDiscoveryTests.RejectedMod_DoesNotConsumeItsModId` |
| Content id rail | upper-case, `:`, whitespace, over-length ids are refused by `IModContent.TryRegister` | `ModContentTests.ContentIds_MustBeCanonicalPaths` |
| Canonical mod id | `testcontent:wooden.sword` resolves to the owning registration and exposes the namespace | `ModContentTests.ContentCatalog_ResolvesCanonicalIdAndExposesNamespace` |
| Canonical resolution policy | canonical first, legacy bare id second, ambiguous bare id fails closed; owner query follows the same rules | `ModContentCatalogTests.Catalog_ResolvesCanonicalNamespaceId`, `ModContentOwnerQueryTests.OwnerQuery_ResolvesCanonicalIdAndKeepsLegacyBareId` |
| Conflict identity | conflicts are keyed by the bare game id; two mods with different namespaces and the same bare id still conflict, while both canonical ids resolve | `ModContentCatalogTests.Catalog_NamespacedSameBareId_IsStillAGameKeyConflict`, `Catalog_ConflictIdentity_IsTheBareGameId` |
| Catalog merge | sources merge by canonical id, duplicates keep the first entry, id-ordered | `ResourceLocationCatalogTests.Entries_MergeSourcesAndDeduplicateByCanonicalId` |
| Completion matching | bare path, canonical prefix, localized display name, case-insensitive, alias never returned | `ResourceLocationCatalogTests.Suggest_MatchesBarePathCanonicalPrefixAndDisplayName`, `Suggest_ReturnsCanonicalIdNeverTheInputAlias` |
| Completion ranking/cap | exact > prefix > path > name; path beats name; ≤20 deterministic | `ResourceLocationCatalogTests.Suggest_ExactCanonicalMatchRanksBeforePrefixAndNameMatches`, `Suggest_PathPrefixRanksBeforeDisplayNamePrefix`, `Suggest_IsCappedAndDeterministic` |
| Empty/no-match | empty/null/blank prefix returns the first entries; no match is empty | `ResourceLocationCatalogTests.Suggest_EmptyOrNullPrefix_ReturnsFirstEntriesInIdOrder`, `Suggest_NoMatch_ReturnsEmpty` |
| Mod content source | typed display name, id fallback, legacy skip, invalid-id defensive skip, ambiguous-bare-id skip | `ModContentResourceLocationSourceTests` (6 facts) |
| Vanilla game source | real mapping (not a stub): canonical ids + localized names, injected-mod-id exclusion, invalid-id skip, display-name fallback, empty table, seam implementation | `VanillaItemResourceLocationSourceContractTests` (6 facts, reflective GameAdapter contract) |
| Accept-vs-inject rule | a mod definition whose id collides with a vanilla item is accepted but never injected, so `InjectedItemIds` stays empty and `cu:<vanilla id>` survives | `GameAdapterItemInjectionContractTests.CollidingModItemId_IsAcceptedButNeverInjected_AndVanillaResourceIdSurvives` |
| Uninitialised id defence | a source contributing `default(ContentId)` is skipped with a warning instead of breaking matching | `ResourceLocationCatalogTests.Entries_SkipUninitialisedIdsFromSources` |
| Console completion | `ResourceLocation` returns `cu:player` with a description; mod content completes by bare id and namespace to the canonical id | `CommandConsoleServiceTests.ArgumentSuggestions_ResourceLocationKind_ReturnsCatalog`, `..._CompletesModContentByBareIdAndNamespace` |
| Mod command completion | `/cresource cu:` → `cu:player`; `/cresource wooden` → `testcontent:wooden.sword` (never the bare alias) | `ModConsoleCommandTests.ModConsoleCommand_ResourceLocationCompletion_ReturnsCatalog`, `..._InsertsCanonicalModId` |
| Selector vocabulary | `@a[type=` suggests `player` and `cu:player`; the resolver accepts `cu:player` and no longer `cuo:player` | `CommandSelectorSuggestionsTests.TypeValueSuggestions_AreFullSelectorPrefixes`, `CommandSelectorResolverTests.BracketedTypeFilter_AcceptsPlayerAndRejectsOtherTypes` |
| Legacy compatibility | bare-id resolution, `IModContentOwnerQuery`, content binder and provider tests unchanged and green | full suite (see §4) |
| Structure | one top-level type per file; touched types stay under the 600-line / 5-bool gates; static placeholder deleted | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` |

## 3. Verification design

- **L0 pure units:** `ContentIdTests`, `ResourceLocationCatalogTests`,
  `ModContentResourceLocationSourceTests`, `ModContentCatalogTests`,
  `ModDiscoveryTests`, `ModContentTests` — the whole matching/ranking/grammar
  contract is Unity-free.
- **L0 integration:** `CommandConsoleServiceTests` / `ModConsoleCommandTests`
  drive the real DI stack (`TestNode`) including a real `TestContentMod` with a
  declared namespace, so the runtime catalog → console path is covered without
  Unity.
- **Static evidence:** the game-localized display name chain
  (`Item.GlobalItems` → `ItemInfo.fullName` → `Locale.GetItem`, `Item.cs:7096`)
  and the existing `IModContentCatalog`/content-binder surfaces (unchanged
  behavior for legacy ids).
- **Runtime:** build + full `dotnet test` + `dotnet format` + normative gates,
  then `tools/deploy.ps1` to the real game directory and a deployed-DLL
  hash/timestamp comparison against the build output.
- **User acceptance:** the IMGUI completion panel itself is not machine-visible
  here; the user's final acceptance pass covers the in-game feel (suggestion
  list rendering, Tab insertion) — everything the console *returns* is locked by
  the tests above.

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 2593 passed / 0 failed (17 normative gates included) |
| `dotnet format CasualtiesUnknownOnline.slnx` + `--verify-no-changes` | clean for every tracked source; the only verify findings are the pre-existing generated `obj/.../MyPluginInfo.cs` (untracked build output) |
| Independent adversarial review (fresh subagent context) | FAIL → 12 findings fixed: namespace reservation leak, cross-namespace bare-id conflict, `default(ContentId)` hash crash, ambiguity diagnostics, owner-query canonical resolution, untested vanilla source, injected-vs-accepted item ids, whitespace namespace, selector resolver coverage, `"cu : a"` leniency, null display name, stale docs/tests |
| `tools/deploy.ps1 -GameDir <game-dir>` | deployed; every build-output DLL SHA256-matched the build output (`steam_api64.dll` / `Steamworks.NET.dll` matched `references/`); the deployed assemblies expose `ContentId`, `CuoModAttribute.Namespace`, `IResourceLocationCatalog`, `ResourceLocationCatalog`, `ModRegistry.ClaimNamespaces` and `ModContentRegistration.TryGetCanonicalId` |
| Protocol | unchanged |

## 5. Accepted residuals

- **Game entity prefabs are not enumerated.** The only authoritative source is
  `Resources.LoadAll<GameObject>("")` (`ConsoleScript.cs:98-100`), which loads
  every game prefab; that cost needs its own evidence/decision. `cu:player` is
  the one entity id the console vocabulary already used, and
  `IResourceLocationSource` is the seam a future prefab source plugs into.
- **Pinyin matching is out of scope** and stays in
  `todo/pinyin-search-mod.md`; it will plug a matcher in front of the same
  catalog.
- **Mod content ids keep the game-table key as the bare registration id.** The
  canonical `namespace:id` is the CUO vocabulary; the game's `Item.GlobalItems`
  key, wire item ids and save ids are unchanged. Consequence: two mods in
  different namespaces registering the same bare id for the same kind are still
  a conflict (the game table can hold only one) — the catalog reports it, the
  bare id fails closed, and neither canonical id is advertised for completion
  until one of them stops using the bare id.
- **The display name is only as good as its source.** Vanilla items use the
  game's localized name; mod content uses the typed DTO's `DisplayName`, and
  kinds without one (recipes) fall back to the registered id.

## 6. Structure review

- `ContentId` is one immutable value type in Abstractions (the only mod-facing
  assembly); parsing/validation is pure and side-effect free.
- `ResourceLocationCatalog` owns only merge/matching/ranking; sources own their
  own enumeration. The console maps entries to UI rows and holds no vocabulary
  policy.
- `ModContentCatalog` still only reads the registry; canonical resolution is
  derived from `ModContentRegistration` (no new global state, no second source
  of truth), and namespace ownership is a single post-ordering pass in
  `ModRegistry` (`ClaimNamespaces`) rather than scattered reservations.
- The Game Adapter source is the only game-table reader; it reuses the existing
  `GameAdapterItemContentProvider` to exclude mod-injected ids.
- The static `ConsoleResourceLocationCatalog` placeholder is deleted, not kept
  as a fallback — one vocabulary, one code path.
- No touched type exceeds the architecture line/bool gates; no wire or protocol
  change.
