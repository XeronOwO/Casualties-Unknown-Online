# Command completion for the ID system: id/name search

- Status: Review
- Priority: Medium
- Category: Tooling / Console / Mod API
- Depends on: [Namespaced ID system](id-system-namespaced-ids.md) — this ticket
  consumes its vocabulary and lands in the same cycle (bundled item).

## Delivered

Code-complete and verified; awaiting the final unified acceptance pass.

- `CommandConsoleService.SuggestResourceLocations` maps
  `IResourceLocationCatalog` entries to `CommandSuggestion(canonicalId, "kind ·
  displayName")`; the phase-18 static `ConsoleResourceLocationCatalog` is
  deleted.
- Matching: exact canonical id > canonical prefix > bare path prefix >
  display-name prefix, ordinal id tie-break, ≤20 results, empty prefix returns
  the first entries.
- Vanilla `cu:<item id>` entries carry the game-localised `ItemInfo.fullName`
  (`Item.cs:7096`); mod content entries carry the typed DTO display name.
- Selector vocabulary migrated `cuo:player` → `cu:player`
  (`CommandSelectorSuggestions`, `CommandSelectorFilter`).
- Evidence: `ResourceLocationCatalogTests`,
  `ModContentResourceLocationSourceTests`,
  `VanillaItemResourceLocationSourceContractTests`,
  `GameAdapterItemInjectionContractTests`, `CommandConsoleServiceTests`,
  `ModConsoleCommandTests`, `CommandSelectorResolverTests`.

## Goal

While the user is entering a `CommandArgumentKind.ResourceLocation` argument,
the console completes by canonical id, by bare content id, and by the
localized/common display name, and every accepted suggestion is the canonical
`namespace:path` id — never the raw input alias.

## Behavior contract

| Input typed after `/cmd ` | Suggestion text | Why |
|---|---|---|
| `fen` | `cu:fentanyl` | bare path prefix of a built-in item |
| `cu:fen` | `cu:fentanyl` | canonical id prefix |
| `芬太` | `cu:fentanyl` | display-name prefix (`芬太尼` is the game-localised name) |
| `FEN` | `cu:fentanyl` | matching is case-insensitive; output is canonical lowercase |
| `mymod:sw` | `mymod:sword` | namespaced mod content |
| `sword` | `mymod:sword` | mod content display name / bare id |
| `cu:player` (exact) | `cu:player` first | exact canonical match outranks prefix matches |

Rules:

- The catalog owns matching and ranking; the console only maps
  `ResourceLocationEntry` → `CommandSuggestion` (text = canonical id,
  description = `<kind> · <display name>`).
- Ranking: exact canonical > canonical prefix > bare path prefix >
  display-name prefix, then ordinal id order; at most 20 suggestions.
- An empty prefix returns the first ≤20 entries in id order (Tab completion is
  still useful on an empty argument).
- Pinyin matching is explicitly out of scope (see `todo/pinyin-search-mod.md`);
  the pinyin ticket will plug a matcher in front of this same catalog seam.

## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | `/cresource cu:` | suggestion `cu:player` present with a non-empty description |
| 2 | Catalog with a vanilla item `cu:fentanyl` / `芬太尼` | `Suggest("fen")`, `Suggest("cu:fen")`, `Suggest("芬太")`, `Suggest("FEN")` all return exactly `cu:fentanyl` |
| 3 | Catalog with a mod item `mymod:sword` / `Wooden Sword` | `Suggest("my")`, `Suggest("sword")`, `Suggest("mymod:sw")` return `mymod:sword` |
| 4 | Input alias never leaks | no suggestion text equals the raw display name or the bare path when a canonical id exists |
| 5 | Selector vocabulary | `@a[type=` suggests `@a[type=player` and `@a[type=cu:player`; resolver accepts `cu:player` |

## Verification

- `ResourceLocationCatalogTests` (matching/ranking/dedup/cap),
  `ModContentResourceLocationSourceTests` (canonical mod ids + display names),
  `CommandConsoleServiceTests.ArgumentSuggestions_ResourceLocationKind_ReturnsCatalog`,
  `ModConsoleCommandTests.ModConsoleCommand_ResourceLocationCompletion_ReturnsCatalog`,
  `CommandSelectorSuggestionsTests`, `CommandSelectorFilterTests`/`CommandSelectorResolverTests`.
- Full build + test + format + normative gates; deployed-DLL hash verification.
