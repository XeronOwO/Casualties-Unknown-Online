# Mod API contract governance — Self-Check (2026-09-20)

Cycle: backlog `review/mod-api-contract-governance.md` (Medium-High; Loomi architecture review
items 7–8 and the visibility discussion). Every number below is reproducible from the committed tree
by the command named beside it, and the cycle includes the fixes for its own independent adversarial
review (§ Review round).

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Before | This cycle's change | Evidence |
|---|---|---|---|---|
| 1 | Which public surface is a promise | — (nothing recorded it: `Abstractions` carried 90 public types before this change and "public" was an accident of compilation) | one binding rule (minimum visibility; only a designed/documented/reviewed capability is a contract) plus the policy page that defines contract vs implementation | `AGENTS.md` Engineering Conventions #14; `docs/api/advanced-modification-policy.md` §1 |
| 2 | Stability of a surface | — (`AccessNativeApi`'s escape hatch carried the same implicit promise as the core) | `ApiStabilityLevel` + `[ApiStability]` with `Stable` as the documented meaning of ABSENCE; `IModNativeApi` and `IModNativeLocalPlayerState` declared `Advanced` on evidence | `src/CasualtiesUnknownOnline.Abstractions/ApiStabilityLevel.cs`, `ApiStabilityAttribute.cs`, `IModNativeApi.cs`, `IModNativeLocalPlayerState.cs`; 17 `Advanced` entries in the baseline (2 types + 15 members) |
| 3 | Public-surface record | — (adding or removing a public member was free and invisible) | a committed baseline (one entry per type and per member, carrying its level, modifiers and signature) that the gate re-derives and compares: ADDED / REMOVED / CHANGED / DUPLICATE / TOMBSTONE / MALFORMED findings, a census floor, and a regeneration candidate | `docs/api/abstractions-api-baseline.txt` (769 lines: 13 header + 92 type + 664 member entries); `tests/.../ApiSurfaceGate.cs`, `ApiSurfaceGateTests.cs` |
| 4 | Surface extraction | — | Roslyn syntax scan of the project's 92 sources: type kind + base list, member signatures with accessors, default values and DECLARATION MODIFIERS (`static` versus instance is an API difference), enum members as `implicit #<ordinal>`, primary constructors (which have no `ConstructorDeclarationSyntax`), a C# 14 `extension` block with its receiver folded into each member's parameters, identical `partial` declarations collapsed, and an unresolvable `[ApiStability]` argument reported as a finding instead of silently inheriting the default level | `ApiSurfaceGate.ExtractFromSources`; `LanguageVersion.Preview` parse, same style as `FullyQualifiedNameGate` |
| 5 | Protocol version truth | `docs/api/mod-api.md` §7 stated the value as `31` (the constant is 34) and decision 137's row said "currently 31"; §7 also kept a second, stale wire-change list | both point at `ProtocolVersion.Current`; a gate fails a LIVE governance document that restates the value | `docs/api/mod-api.md` §7; `docs/decisions/active.md` #137; `tests/.../ProtocolNumberGateTests.cs` |
| 6 | Mod API contract pointer | §5 said compatibility ranges are "deliberately not inferred until a formal API-compatibility contract exists" | §5 names the two artifacts that now exist (policy page + baseline); §1 links the policy page | `docs/api/mod-api.md` §1/§5 |
| 7 | Absolute-path gate reach | the gate enumerated `git ls-files` (tracked files only), so a brand-new file was never checked before the commit that added it | the gate now enumerates `--cached --others --exclude-standard` (tracked plus untracked-and-not-gitignored) and reports how many files it scanned; this is the root cause of the pre-existing red below AND of this cycle's own blocker (see § Review round) | `tests/.../RepositoryGateTests.EnumerateScannedFiles`; `docs/evidence/normative-gates.md` rule #5 row and its note |
| 8 | Pre-existing gate red | `AdapterCapabilityReporter` logged the report through a one-line template containing a literal colon-backslash-n, which `NoAbsolutePaths_NoTrackedMachinePaths` reads as a drive-letter path — RED at HEAD `bc606a4f` (gate code and that line both unchanged from HEAD) | the message text is composed instead; the rendered message stays byte-identical (a single LF, not `Environment.NewLine`) | `src/CasualtiesUnknownOnline.GameAdapter/Capabilities/AdapterCapabilityReporter.cs` |
| 9 | Wire / protocol / save | — | untouched: no wire shape, no save shape, no `ProtocolVersion` value change | `git status` (no `Protocol/`, `GameState/` or wire file touched) |

## Design (decisions worth recording — decision 203)

- **Marker form: an attribute, not a namespace.** `[ApiStability]` keeps one assembly, avoids moving
  90 types (which would rewrite every `using` for no behavioral gain), is visible in IntelliSense and
  greppable. A level is declared only when it is NOT `Stable`; the baseline records the effective
  level, so "nobody marked it" cannot silently mean "it was reviewed as Stable" — the baseline review
  is the decision point.
- **Why a committed baseline + a repo-owned gate, and not a public-API analyzer package.** The record
  must carry the stability level (an analyzer's shipped-API file cannot), the repo's gate style wants
  a census floor and synthetic negative samples, and the project targets `net48` with
  `LangVersion = preview` — a compiler-coupled analyzer package is a dependency this gate does not need.
- **Baseline format.** One `|`-separated entry per line, key = `type|<fqn>` or
  `member|<owner>.<name>(<parameter type list>)`. Type references are recorded by their simple name, so
  a `using` edit is not an API change; implicit enum values are `implicit #<ordinal>` so reordering is
  detectable without evaluating constants; accessors, default values, modifiers and constants are
  recorded as written after `dotnet format`'s whitespace normalization.
- **Removal semantics.** A removal deletes the line AND adds `*REMOVED* <key> — <reason>`: a removal
  is a deliberate act with a stated reason. The gate also fails a tombstone whose entry came back, and
  a tombstone for an entry that is still listed as live.
- **Regeneration.** The failing gate writes the candidate to the gitignored
  `artifacts/api-surface/abstractions-api-baseline.txt`; approval is copying the reviewed lines into
  the baseline in the same commit. No test writes a tracked file.
- **Scan surface of the protocol gate.** Eleven LIVE documents: `AGENTS.md`, `docs/README.md`, the
  active decision register, the live architecture pages (`README.md`, `current.md`, `protocol.md`,
  `domains.md`), `docs/development/agent-reference.md`, `docs/evidence/normative-gates.md`, and
  `docs/api/**/*.md`. Self-checks, backlog tickets and the evolution logs are records of a past state:
  they keep the number they were written with, and rewriting them would destroy the record.

## Verification design

- **Red on the real tree, not only on synthetic input** (new-gate acceptance): a temporary
  `src/CasualtiesUnknownOnline.Abstractions/ZzNegativeControl.cs` declaring one public interface and
  one member made the gate report both as ADDED and emit a candidate; a temporary
  `docs/api/zz-negative-control.md` claiming "the protocol version is 31 today" was reported by the
  protocol gate. Both controls were deleted and the tree re-ran green.
- **Matcher contract (synthetic, in the suite):** ADDED; REMOVED without a tombstone; REMOVED accepted
  WITH a tombstone; CHANGED on a stability-level change; MALFORMED for a missing level and for a
  tombstone without a reason; reference-spelling neutrality (a fully qualified reference set and a
  `using`-based set produce identical surfaces); a C# 14 `extension` member is ADDED; a modifier-only
  change (`static` → instance, `struct` → `readonly struct`) is CHANGED; a `partial` type split across
  two files is clean; a nested type declared in an interface without a modifier is ADDED; an
  unresolvable `[ApiStability]` argument is MALFORMED.
- **Census:** floors are asserted on BOTH sides (the tree's scan and the committed baseline), so a scan
  that finds nothing cannot pass.
- **Runtime judge:** the gate is part of `dotnet test`; the extraction is re-derived on every run, so
  the baseline cannot rot silently.

## Self-check table

| Area | Change | Evidence |
|---|---|---|
| Contract vs implementation | the rule is binding in `AGENTS.md` and defined in the policy page (Harmony allowed and not hostile, no promise; the promotion funnel patch → Experimental → Stable) | `AGENTS.md` #14; `docs/api/advanced-modification-policy.md` §1/§4/§6 |
| Stability levels | four levels defined; two game-build-sensitive surfaces marked `Advanced` with the evidence for each | `ApiStabilityLevel.cs`; `IModNativeApi.cs` (operation registry follows the adapter's registration); `IModNativeLocalPlayerState.cs` (members cite the game's `Body` fields) |
| Baseline record | 769 lines / 92 type entries / 664 member entries, committed and reviewed before commit | `docs/api/abstractions-api-baseline.txt` |
| Addition / removal / change detection | the gate fails each, with the tombstone rule for removals | `ApiSurfaceGateTests.AbstractionsPublicSurface_MatchesTheReviewedBaseline`, `...TheMatcher_FlagsAnAddedMemberAsAnApiChange`, `...FlagsARemovalWithoutATombstoneAndAcceptsOneWith`, `...FlagsALevelChangeAndAMalformedBaselineLine` |
| Reach: language shapes | an `extension` member, a modifier-only change, a `partial` split and an interface-nested type are all seen; an unresolvable marker fails | `...TheMatcher_SeesACSharp14ExtensionMember`, `...FlagsAModifierOnlyChange`, `...AcceptsAPartialTypeSplitAcrossFiles`, `...SeesANestedTypeDeclaredInAnInterfaceWithoutAModifier`, `...FlagsAStabilityArgumentItCannotResolve` |
| Normalization contract | a `using` edit and a fully qualified spelling produce the same surface | `ApiSurfaceGateTests.TheMatcher_IgnoresHowAReferenceIsSpelled` |
| Census floor | both sides floored (60 files / 54 types / 450 entries against a measured 92/92/756) | `ApiSurfaceGateTests.TheBaselineAndTheScan_MeetTheCensusFloor` |
| One protocol number | no live governance document restates the value across eleven live documents; the constant exists (checked as the truth source) | `ProtocolNumberGateTests.LiveGovernanceDocuments_DoNotRestateTheProtocolVersionNumber`, `...TheMatcher_FlagsACurrentValueClaimAndIgnoresThePointerForm` |
| Doc truth | §5 no longer says the contract is missing; §7 no longer keeps a second wire-change list; decision 137 points at the constant | `docs/api/mod-api.md`; `docs/decisions/active.md` #137/#203 |
| Gate registration | both gates are in the rule→gate inventory, and rule #5's scan surface is recorded | `docs/evidence/normative-gates.md` (#14 row, protocol-number row, #5 row + note) |
| Pre-existing red cleared | the absolute-path gate is green again without weakening the gate, and it now sees untracked files | `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths`; the `EnumerateScannedFiles` change |

## Review round (independent adversarial review, 2026-09-20)

The review ran against the frozen tree before the commit and returned 1 blocker / 2 major / 6 minor /
3 nit; every finding was fixed in this same cycle.

- **Blocker — the new gate's own source re-created the absolute-path failure it was written to clear**
  (`ApiSurfaceGate.BaselineTextFor` built a header line ending in `comment:` plus a literal
  colon-backslash-n). It was invisible to the local run only because the file was still untracked and
  the gate enumerated tracked files. Fixed by building the header with `AppendLine` (no escape shapes
  at all) AND by widening `EnumerateScannedFiles` to untracked-and-not-gitignored files, so the class of
  miss is closed rather than the instance.
- **Major — C# 14 `extension` members were invisible.** `extension(...)` blocks are
  `ExtensionBlockDeclarationSyntax` (a `TypeDeclarationSyntax`, which is why the generic case swallowed
  them); they carried no `public` and were neither types nor methods. Now modeled: each member is
  recorded with the block's receiver folded into its parameter list, and an unknown declaration kind is
  recorded as `unmodeled` with its own members visited, so a future C# shape cannot hide surface the
  same way.
- **Major — no declaration modifier was recorded**, so `static` → instance, `struct` → `readonly
  struct`, `class` → `sealed class`, `const` → `static readonly` and similar were silent changes. The
  recorded kind now carries the non-accessibility modifiers.
- **Minors fixed:** the log message is now byte-identical to HEAD (a single LF, not
  `Environment.NewLine`); §7 no longer over-states the constant's doc comment as an exhaustive per-bump
  log; the protocol gate's scan surface was widened to the live architecture/development/evidence pages
  the policy sentence implies (eleven documents, floor raised to eleven); an identical `partial`
  declaration in two files no longer reports DUPLICATE; an implicitly-public nested type in an
  interface is seen; an unresolvable `[ApiStability]` argument is a finding.
- **Nits fixed:** the ticket's Problem section now reads "90 public type declarations before this
  change"; the regeneration candidate under `artifacts/` is regenerated (and cleaned at hand-off).

## Delivery evidence (2026-09-20)

- `dotnet build CasualtiesUnknownOnline.slnx` → 0 warnings, 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx` → `CasualtiesUnknownOnline.Tests` 3703 passed / 0 failed;
  `CasualtiesUnknownOnline.NormativeGates.Tests` 81 / 82 with the ONLY red being
  `DeliveryChecklist_NoIncompleteRequiredBoxes` while this cycle's boxes are still unchecked (the gate
  working as intended), green after the checklist is filled.
- `dotnet format CasualtiesUnknownOnline.slnx` → exit 0.
- New gate tests: 13 (11 `ApiSurfaceGateTests` + 2 `ProtocolNumberGateTests`); the gate project grows
  from 69 to 82 tests.
- Baseline census: 769 lines, 92 type entries, 664 member entries, 17 `Advanced` entries, 6 primary
  constructor entries.

## Known boundaries (what this cycle does NOT prove)

- The gate proves that the tree's DECLARED public surface and the reviewed record agree. It does not
  judge whether a recorded line is a good API, it does not see compiler-synthesized members (a
  record's equality members) or method bodies, it ignores attribute lists other than `[ApiStability]`,
  and it cannot tell whether a `Stable` promise is kept in behavior — that stays a review question.
  Two `partial` declarations of one type collapse only when their recorded lines are IDENTICAL; two
  different lines for one key stay a duplicate finding.
- The protocol gate recognizes the CLAIM shape (a claim word or `=` before a number on a line that
  mentions the protocol version). A phrasing that claims the current value without one of those words
  is a documented blind spot; the matcher's contract is pinned by its own test.
- No runtime behavior change reaches the game: the only production edit is how one log message's text
  is composed (byte-identical rendering, not observed in a live log), so no deployment was performed
  and no in-game verification is claimed. Wire, protocol number and save shape are untouched.
- Process note for the next reader: the absolute-path red above was present at HEAD while the previous
  cycle's evidence reported the gates green, and this cycle's own blocker was the same defect class in
  reverse (a new file the gate could not see). Both were the tracked-only scan surface; the gate now
  scans what git would carry, and the lesson is the one the workflow already states: the gate run
  belongs to the same sequence as the final edit, and the committed tree is the only state that counts.
