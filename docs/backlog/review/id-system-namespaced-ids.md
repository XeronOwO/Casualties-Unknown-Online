# Namespaced ID system

- Status: Review
- Priority: Medium
- Category: Architecture / Mod API / Content IDs
- Source: User request (2026-09-07) — CUO IDs should use the `namespace:id` form,
  built-in namespace `cu`, mods declare their own namespace, scope includes (but
  is not limited to) items and entities, canonical form `cu:fentanyl`.
- Bundle: implemented together with
  [Command completion for the ID system](command-id-name-completion.md); that
  ticket consumes this vocabulary and is the only user-visible consumer in this
  cycle. Pinyin search stays separate (`todo/pinyin-search-mod.md`).

## Delivered

Code-complete and verified; awaiting the final unified acceptance pass.

- `ContentId` (`namespace:path`) value type in `CUO.Abstractions`, with
  `TryParse`/`TryCreate`/`Parse`/`IsValidNamespace`/`IsValidPath`, value
  equality, ordinal ordering and case normalisation (`ContentId.cs`).
- `[CuoMod(..., Namespace = "mymod")]` → `ModManifest.Namespace`; discovery
  rejects an invalid/reserved/empty namespace and claims ownership only for
  candidates that actually load (`ModRegistry.cs`).
- Canonical mod content ids via `ModContentRegistration.TryGetCanonicalId`;
  `ModContentCatalog` resolves canonical ids first and legacy bare ids second,
  with conflicts keyed by the bare game id (`ModContentCatalog.cs`).
- `ModContentPolicy.IsValidId` requires a canonical lower-case path segment.
- `IResourceLocationCatalog` + built-in/mod-content/vanilla sources; the console
  `ResourceLocation` argument completes by canonical id, bare id, or localized
  display name (`content-id-selfcheck.md`).
- 2593 + 17 tests green, `dotnet format` clean, deployed-DLL hashes verified,
  two independent adversarial review rounds (12 + 9 findings) closed.

## Goal

Turn "content identity" from an ad-hoc bare string into one validated,
namespaced vocabulary that every CUO layer can share:

1. A canonical `namespace:path` id type with a documented grammar.
2. `cu` as the reserved built-in namespace; mods declare their own namespace and
   the framework refuses collisions with `cu` or with another mod.
3. A single framework-wide content-id catalog that can enumerate and match
   built-in and mod content by canonical id, bare path, and display name.
4. A console completion provider that turns the existing `ResourceLocation`
   argument kind into a real, content-driven vocabulary.

## Frozen design decisions

| # | Decision | Rationale / evidence |
|---|---|---|
| D1 | Id type `ContentId` in `CUO.Abstractions`: `Namespace`, `Path`, `TryParse`, `TryCreate`, `IsValidNamespace`, `IsValidPath`, `ToString()` = `namespace:path`. | Abstractions is the only assembly mods may reference (`docs/api/mod-api.md` §1), so the id vocabulary must live there to be a mod-facing seam. |
| D2 | Grammar: namespace `[a-z][a-z0-9_]{0,31}`; path `[a-z0-9][a-z0-9_.-]{0,94}`; canonical length ≤128; input is lower-cased before validation, so `cu:Fentanyl` parses to canonical `cu:fentanyl`. | Vanilla item ids are `[a-z][a-z0-9]*` (218 ids measured in `reversing/Assembly-CSharp/Assembly-CSharp/Item.cs`, charset `a-z0-9`); mod content ids already allow dots (`wooden.sword`, `docs/api/mod-api.md` §4f); `32 + 1 + 95 = 128` keeps the canonical id within the previous content-id cap. |
| D3 | `cu` is the reserved built-in namespace (`ContentId.BuiltInNamespace`). A mod declaring `cu` is rejected at discovery. | Ticket requirement; `cu:fentanyl` is the canonical example. |
| D4 | The namespace is **declared in the mod manifest**, `[CuoMod(..., Namespace = "mymod")]`, not registered imperatively at bind time. | The manifest is already the single declared source for id/version/mode/permissions (`CuoModAttribute.cs:13-18`, `ModRegistry.cs:94`); discovery already rejects bad manifests per mod (`ModRegistry.cs:50-92`), so namespace validation and collision refusal land in the same fail-closed place, before any `Bind` side effect. |
| D5 | `ModManifest.Namespace` (nullable) carries the declared namespace; `ModContentRegistration.Namespace` exposes it to the content catalog (optional third positional parameter — existing 2-arg call sites keep compiling). | Keeps `IModContentCatalog` able to compute a canonical id without new global state. |
| D6 | Canonical id of mod content = `<namespace>:<registration id>`; a mod without a declared namespace keeps the legacy bare id (mod-scoped). The **bare id remains the game-table key** that content providers materialise, so two mods in different namespaces registering the same bare id are still a conflict and the ambiguous bare id fails closed. | Backward compatible with `ModContentPolicy`/`IModContent`; the game's `Item.GlobalItems`/provider tables are keyed by the bare id, so namespaces can add a canonical address but cannot make two game entries coexist. |
| D7 | `ModContentPolicy.IsValidId` tightens to "valid `ContentId` path": lowercase ASCII, no `:`, ≤95 chars. | Canonicalisation must be unambiguous; the project is pre-release and may break unused compatibility (`AGENTS.local.md`, 破坏性修改与版本策略). |
| D8 | `IResourceLocationCatalog` (Runtime) aggregates `IResourceLocationSource` implementations and owns matching/ranking; the console maps entries to `CommandSuggestion`. | Console stays a presenter; the catalog is Unity-free and testable, matching the existing `ICommandArgumentSuggestions` seam. |
| D9 | Built-in sources: `BuiltInResourceLocationSource` (Runtime, `cu:player` entity id used by the selector `type=` vocabulary) + `VanillaItemResourceLocationSource` (GameAdapter, `Item.GlobalItems` → `cu:<id>` with the game-localised `ItemInfo.fullName`, excluding ids the item provider actually injected). Mod content: `ModContentResourceLocationSource` (Runtime, `<ns>:<id>` + typed DTO display name, skipping ambiguous bare ids). | `ItemInfo.fullName = Locale.GetItem(id)` (`Item.cs:7096`) proves the display name is the localised/common name the ticket requires; excluding injected ids keeps `cu:` for vanilla only. |
| D10 | Completion accepts canonical-id prefix, bare-path prefix, and display-name prefix; every suggestion returns the canonical id, ranked exact → canonical prefix → bare path → display name, capped at 20. | Ticket requirement ("completed suggestion must be the canonical namespaced id, not the raw input alias"). |
| D11 | `cuo:` (the phase-18 placeholder vocabulary) is replaced by `cu:` everywhere: `ConsoleResourceLocationCatalog` is deleted, `CommandSelectorSuggestions`/`CommandSelectorFilter` accept `player` and `cu:player`. | Ticket fixes the built-in namespace as `cu`; the old static catalog was the explicit "static, game-content-free" gap recorded in `docs/evidence/selfchecks/ui/command-console-selfcheck.md` §3. |
| D12 | Identity claims are decided per **surviving candidate**, not during validation: rejection is tracked per candidate, the dependency closure runs first, and only then does the duplicate-id pass keep the first still-loadable declaration; namespace ownership is claimed after ordering, with dependents of a namespace-rejected mod dropped. A non-null but empty/whitespace `Namespace` is a typo and is rejected. | Independent review found that reserving during validation let a mod rejected for a missing/cyclic dependency deny its namespace (and, in a first fix attempt, its mod id) to a valid mod, contradicting `docs/api/mod-api.md` "one rejected mod never blocks the scan". |
| D13 | No wire change, no protocol bump: content bytes and namespaces stay process-local; state-bearing handshakes already require mod version equality, so a namespace mismatch implies a rejected mod pair. | `docs/api/mod-api.md` §4f ("Process-local", "No wire change"); `ModRegistry.CurrentModInfos()` carries only id/version/mode/permissions (`ModRegistry.cs:112-119`). |

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | `ContentId.TryParse("cu:fentanyl")` / `"CU:Fentanyl"` | canonical `cu:fentanyl`; `Namespace="cu"`, `Path="fentanyl"` |
| 2 | Invalid ids: `""`, `":x"`, `"cu:"`, `"CU Fentanyl"`, `"cu :a"`, `"cu: fen"`, `"cu:fen tanyl"`, `"cu:fen/tanyl"`, namespace >32, path >95, non-ASCII path | `TryParse` false; `TryCreate` false |
| 3 | Path grammar accepts `wooden.sword`, `a-b_c.9`, `9mm` | valid (dots/hyphens/underscores/digits) |
| 4 | Mod declares `Namespace="mymod"` | `ModManifest.Namespace="mymod"`; its item `sword` resolves as `mymod:sword` |
| 5 | Mod declares `Namespace="cu"` | mod rejected at discovery with a reserved-namespace log; no content bound |
| 6 | Two mods declare the same namespace; a mod whose namespace claimant is rejected for a missing dependency; and two candidates declaring the same mod id where the first is rejected | only a surviving candidate owns the namespace/id; the valid mod keeps it |
| 7 | Mod declares an invalid namespace (`"My Mod"`, `"9mod"`, `"mod!"`, `""`, `"   "`) | mod rejected at discovery |
| 8 | Mod without a namespace (legacy) | bare id resolution and existing conflict diagnostics unchanged |
| 9 | `IModContentCatalog.TryResolve("item", "mymod:sword")` | resolves the owning registration |
| 10 | `TryResolve("item", "sword")` with one legacy owner; and with two legacy owners | first resolves (backward compatible), second is refused as ambiguous |
| 11 | `TryRegister` with `"Bad Id"`, `"ns:x"`, uppercase id, or 96-char path | refused with a log (D7) |
| 12 | Catalog with a vanilla item source | `Suggest("fen")`, `Suggest("cu:fen")`, `Suggest("芬太")` all return canonical `cu:fentanyl` |
| 13 | Mod content with display name | `Suggest("my")` / `Suggest("sword")` / `Suggest("my:sw")` return `mymod:sword` |
| 14 | Exact canonical match outranks prefix matches; ties are alphabetical; result count ≤20; empty prefix returns the first ≤20 entries | deterministic ordering |
| 15 | Console: `/cresource cu:` (mod command with a `ResourceLocation` argument) | suggestion text is `cu:player` (canonical), not the input alias |
| 16 | Selector: `@a[type=cu:player]` matches remote players; `@a[type=cuo:player]` no longer matches | new built-in vocabulary |
| 17 | Existing mod-content surfaces (`IModContentCatalog`, `IModContentOwnerQuery`, content providers) | unchanged behavior for legacy ids; canonical ids also resolve in the owner query; full suite green |
| 18 | Two mods in different namespaces register the same bare id | both canonical ids resolve, the bare id is ambiguous, `Conflicts` reports it, and neither canonical id is advertised for completion (the game table can hold only one) |

## Verification plan

- Unit tests: `ContentIdTests`, `ModDiscoveryTests` (namespace rows, rejected
  candidate id/namespace reclaim), `ModContentCatalogTests` (canonical/legacy
  resolution + conflict rows), `ResourceLocationCatalogTests`
  (matching/ranking/dedup/uninitialised-id), `ModContentResourceLocationSourceTests`,
  `ModContentOwnerQueryTests`, `CommandConsoleServiceTests`,
  `ModConsoleCommandTests`, `CommandSelectorSuggestionsTests`,
  `CommandSelectorResolverTests`, `VanillaItemResourceLocationSourceContractTests`,
  `GameAdapterItemInjectionContractTests`.
- Gates: `dotnet build` + `dotnet test` + `dotnet format` + the normative gates
  (architecture/line-count, no-legacy, delivery checklist).
- Runtime: deployed-hash verification of the plugin DLLs; the in-game
  completion panel itself is user-acceptance territory (no Unity-free proof of
  IMGUI rendering).
- Selfcheck: `docs/evidence/selfchecks/tooling/content-id-selfcheck.md`.

## Non-goals

- Pinyin matching (separate ticket `todo/pinyin-search-mod.md`).
- Enumerating game entity prefabs for `cu:` ids: the only authoritative source
  is `Resources.LoadAll<GameObject>("")` (`reversing/.../ConsoleScript.cs:98-100`),
  which loads every game prefab; the ID model and the source seam support it,
  but the cost needs its own evidence and decision. `cu:player` covers the one
  entity id the console vocabulary already used.
- Changing wire ids, save ids, or the game's own item-table keys: the canonical
  id is a CUO vocabulary; the game item id stays the bare registration id.
