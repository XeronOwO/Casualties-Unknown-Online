# Normative Rule Gate Inventory

This page maps the repository's binding normative requirements to the gate that
actually enforces them. The former `tools/check-*.ps1` scripts have been ported
to C# xUnit tests and removed; every gate below now runs as part of the ordinary
`dotnet test` loop.

Automation status legend:

- **dotnet test (C# port)** — implemented directly in
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests`.
- **dotnet format / .editorconfig** — build-enforced formatting and code-style
  IDE rules.
- **Review / process** — enforced by human review, delivery checklist, or an
  explicit process step; not reliably expressible as a source gate without
  false positives or a semantic judgment.

## Engineering conventions

| Rule (AGENTS.md) | Automation status | Gate / evidence |
|---|---|---|
| #1 English in code/comments/docs | Review / process | Not reliably automatable; no language-quality gate. |
| #2 Modern idiomatic C# (`var`, nullable, `is null`, collection expressions) | dotnet format / .editorconfig | `.editorconfig` + `EnforceCodeStyleInBuild`; nullable is project-wide. |
| #2 Unity objects use `== null` / `!= null` | Review / process | Documented Unity exception; IDE0031 deliberately disabled because a global `?.` rewrite would break Unity object semantics. |
| #3 One top-level type per file; file name matches type name | dotnet test (C# port) | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits`. |
| #4 Evidence-based changes / cite decompiled sources | Review / process | Human process; not a source-shape rule. |
| #5 Absolute-machine-path red line (nothing git would carry may contain a drive-letter, UNC or `/home`-style path) | dotnet test (C# port) | `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` — the scan covers tracked files AND untracked-but-not-gitignored ones, so a brand-new file is checked before it is committed. |
| #6 Requirement triage | Review / process | Human judgment. |
| #7 Self-learning / record reusable knowledge | Review / process | Human process. |
| #8 Architecture-first, get consent | Review / process | Approval process before risky changes. |
| #9 Patch hooks report only verified writes | dotnet test + runtime | Existing patch-contract tests and adapter contract tests; this is behavioral, not a syntax gate. |
| #10 Prefer `using` / aliases over fully qualified names | **dotnet test** | Roslyn gate: `FullyQualifiedNameGateTests`. |
| #11 Attribute/reflection registration for large families | Review / process | Design preference; no reliable syntax gate without false positives. |
| #12 Reuse native game UI | Review / process | Acceptance-readiness audit; explicitly a human acceptance decision. |
| #13 Extension methods use the C# 14 `extension` syntax | dotnet test (C# port) | `SourceShapeGateTests.ExtensionMethods_UseTheCsharp14ExtensionSyntax` (fails on a classic `this X` declaration under `src` or `tests`; its own matcher contract is pinned by `TheMatcher_SeesEveryClassicExtensionShapeAndIgnoresOrdinaryStatics`). |
| #14 Minimum visibility, declared stability: only a designed/documented/reviewed capability is a public contract, and the `Abstractions` public surface is a recorded baseline — an addition or removal fails until the baseline is reviewed and updated, a removal names its reason, and a non-`Stable` surface declares its level | dotnet test (C# port) | `ApiSurfaceGateTests.AbstractionsPublicSurface_MatchesTheReviewedBaseline` + `...TheBaselineAndTheScan_MeetTheCensusFloor` + the matcher's own contract (`...TheMatcher_FlagsAnAddedMemberAsAnApiChange`, `...FlagsARemovalWithoutATombstoneAndAcceptsOneWith`, `...FlagsALevelChangeAndAMalformedBaselineLine`, `...IgnoresHowAReferenceIsSpelled`); baseline at [`abstractions-api-baseline.txt`](../api/abstractions-api-baseline.txt), policy at [`advanced-modification-policy.md`](../api/advanced-modification-policy.md) |
| Dependency pin — Microsoft.Extensions 3.1.x on net48 (architecture blueprint §5) | dotnet test (C# port) | `SourceShapeGateTests.MicrosoftExtensionsPinnedToNet48CompatibleLine`. |
| Project dependency direction — GameState, Protocol and Abstractions reference no project, Application is the only path from Runtime up to the kernel and may reference GameState/Protocol only, GameAdapter/Plugin never reach GameState directly, and every solution project is either declared or explicitly listed as a consumer | dotnet test (C# port) | `ProjectDirectionGateTests.Tree_FollowsTheDeclaredProjectDirection` (graph read from `CasualtiesUnknownOnline.slnx` — project references and raw CUO assembly references — with a census floor, a classification census and every declared project checked for presence) + the synthetic contract cases `ProjectDirectionGateTests.GameStateReferencingUpward_IsRefused`, `...RuntimeReachingGameStateWithoutTheLayer_IsRefused`, `...ApplicationReferencingUpward_IsRefused`, `...ApplicationReferencingItsLowerLayers_IsAccepted`, `...PluginReachingGameStateDirectly_IsRefused`, `...ConsumersAreNotConstrained`, `...UndeclaredProject_IsRefusedInsteadOfSilentlyExempt`, `...AbstractionsReachingDown_IsRefused`; the declared table is `ProjectDirectionPolicy.AllowedReferences` and the exempt consumers are `ProjectDirectionPolicy.ConsumerProjects`. |

## Test infrastructure

| Rule | Automation status | Gate / evidence |
|---|---|---|
| A test class that writes a process-global static field must join the non-parallel `GameAssembly` collection (xUnit v2 runs different collections in parallel, so two such classes would race on the same game-assembly/Unity static, and a reader in another collection could observe a half-applied write) | dotnet test (C# port) | `TestIsolationGateTests.StaticGameStateMutations_JoinTheGameAssemblyCollection` (Roslyn per-class detection) + `TestIsolationGateTests.StaticGameStateMutationDetection_FlagsUnisolatedTestClasses` (the gate's own negative/positive contract) + `TestIsolationGateTests.GameAssemblyCollection_IsDeclaredAsTheNonParallelCollection` |
| The test composition must not write a rolling file log per node (shared file name, exclusive write handle, temp directory per node) | Review / process | `tests/CasualtiesUnknownOnline.Tests/Fakes/TestLogging.cs`; the sink keeps direct coverage in `LoggingOptionsTests`. |
| A test class that constructs the production composition root, a full simulation world/replay harness, a shared full-stack fixture, the game-assembly reflection host, or real loopback sockets carries `[Trait("Category", "Integration")]`; untagged classes form the fast inner loop | Review / process | [`test-parallelization.md`](test-parallelization.md) §9 — 260 classes / 1 694 cases tagged; the `Category!=Integration` subset is 2 097 cases (re-measured 2026-09-22 after the plugin-host-shell cycle). |
| No test class may exceed 40 real xUnit cases/data rows, including `MemberData` expansion | dotnet test (runtime reflection) | `TestClassSizeGateTests.NoTestClass_ExceedsTheCaseLimit` (enumerates public test classes, counts `Fact`/`Theory` data rows through xUnit's own data attributes, and includes inherited/static test classes) + `TestClassSizeGateTests.CaseCounting_SeesMemberDataRowsAndFlagsTheLimit` (counting and limit contract). A syntax-tree gate cannot see `MemberData` rows; the original long pole was a `MemberData` class. |
| The parallel model, thread cap and long-pole policy stay measured | Review / process | [`test-parallelization.md`](test-parallelization.md) — three-run median method, the `1x` thread decision (§7.5, §9.3) and the current numbers. |
| Every synced feature declares its event-sync + periodic-fallback policy: every `NetMsg` / `WireCommandKind` / `WireEventKind` / `AdaptiveStreamId` member is indexed in the sync coverage matrix, the index points at a row that mentions it, each matrix row declares how many evidence anchors it owns and that count matches the entries `sync-coverage-evidence.json` holds for its row id (an entry may not name an unknown row unless it is marked `(none)`, document-level evidence owned by no row, capped at five), no row repeats a `path 'quote'` pair (the quote lives only in the evidence file), and every evidence entry's quoted text is still present in the file it names, with the evidence file's declared `count` matching its entries (references carry a path plus the quote, never a line number; the count is a declaration check, not a completeness proof — a quote's relevance to its row stays a review responsibility) | dotnet test (C# port) | `SyncCoverageGateTests.SyncCoverageMatrix_EveryWireDiscriminatorIsIndexed` + `SyncCoverageGateTests.SyncCoverageMatrix_EveryRowHasAVerdictAndEvidence` + `SyncCoverageGateTests.SyncCoverageMatrix_DeclaredAnchorCountsMatchTheEvidence` + `SyncCoverageGateTests.SyncCoverageEvidence_EveryQuoteMatchesItsSourceLine` (negative-contract self-tests included); matrix at [`sync-coverage-matrix.md`](sync-coverage-matrix.md), evidence at [`sync-coverage-evidence.json`](sync-coverage-evidence.json) |

## Quality and delivery rules

| Rule | Automation status | Gate / evidence |
|---|---|---|
| No self-assumption; every claim needs evidence | Review / process | Human/process; reflected in self-check fact sheets. |
| Root cause over patch stacking | Review / process | Human architecture review. |
| Line-count / architecture gate escapes are real responsibility splits | dotnet test (C# port) | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` + `docs/architecture-debt.json`. |
| Red→green hard gate | Review / process | Process rule; deliverable evidence is the failing-test commit. |
| Core + edge/failure test coverage | dotnet test | Existing xUnit suite; coverage breadth is a review/size question. |
| Every key path observable | Review / process | Logging discipline; not a single source-shape gate. |
| Acceptance-readiness audit | Review / process | `docs/evidence/delivery-checklist.md`. |
| User-found issues are hard blockers | Review / process | Backlog/human process. |
| Independent adversarial self-check | Review / process | Human/process. |
| Delivery checklist | dotnet test (C# port) | `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`. |
| The protocol version number lives in ONE place: `ProtocolVersion.Current` and its doc comment are the value and the wire-change log, so a live governance document (`AGENTS.md`, `docs/api/**/*.md`, `docs/decisions/active.md`, `docs/architecture/current.md`) points at the constant instead of restating it — records of a past state (`docs/evidence/**`, backlog tickets, the evolution logs) keep their own historical number on purpose | dotnet test (C# port) | `ProtocolNumberGateTests.LiveGovernanceDocuments_DoNotRestateTheProtocolVersionNumber` (census floor + a check that the constant still exists) + `...TheMatcher_FlagsACurrentValueClaimAndIgnoresThePointerForm` |
| The game-update contract toolchain stays out of the plugin: the tool is metadata-only, no `src/` project references it, and the patch-target rows it recovers from the adapter's metadata equal `PatchInventory.BuildContracts` (the rows it cannot see are exactly the hand-declared dynamic ones); the snapshot is byte-reproducible and its own census is verified on read | dotnet test (C# port) | `PatchContractRowParityTests.ToolRows_EqualTheAdaptersOwnContractRows` + `...CoverEveryAttributedPatchClass` + `RowsTheToolCannotSee_AreExactlyTheHandDeclaredDynamicOnes`, `GameAssemblySnapshotTests.Snapshot_OfTheRealGameAssembly_IsByteReproducible` + `...MeetsTheCensusFloor`, `SnapshotJsonTests.Reader_RefusesACensusThatDisagreesWithItsRows`; runbook at [`game-update-runbook.md`](../development/game-update-runbook.md) |
| The backlog index is a table of POINTERS, not a second copy of its tickets: every ticket is listed exactly once under the section that matches its folder, each row fits a 160-character budget and carries the priority its ticket declares, each ticket's `- Status:` field agrees with its folder, every test anchor a live ticket cites still exists under `tests/`, and no document cites `AGENTS.md` by line number | dotnet test (C# port) | `BacklogIntegrityGateTests.EveryTicketIsIndexedExactlyOnceUnderItsOwnSectionAndEveryLinkResolves` + `BacklogIntegrityGateTests.EveryIndexRowIsAPointerWithinItsBudgetCarryingItsTicketsPriority` + `BacklogIntegrityGateTests.TheStatusFoldersAreTheOnlyOnesAndNoTicketFileSitsLoose` + `BacklogIntegrityGateTests.EveryTicketStatusFieldAgreesWithItsFolder` + `BacklogIntegrityGateTests.EveryDocumentedTestAnchorIsStillDeclared` + `BacklogIntegrityGateTests.NoDocumentCitesAgentsMdByLineNumber`, each with its negative-contract self-test |
| The Harmony patch bridge is FROZEN per domain: the aggregate declares exactly its pinned census and composes exactly the four earlier patch seams, every seam interface declares its pinned census, the one implementation's public surface is exactly the seams it serves, no member name is shared by two seams, and a migrated domain's members are reachable ONLY through its port — the compiler enforces that direction, because the aggregate neither declares nor composes them | dotnet test (C# port; Roslyn source scan, fast suite) | `PatchBridgePortShapeGateTests.Aggregate_DeclaresExactlyTheFrozenCensus` + `...Aggregate_ComposesExactlyThePinnedSeams` + `...Seam_DeclaresExactlyItsPinnedMembers` + `...ThePort_IsNotReachableThroughTheAggregate` + `...NoMemberName_IsSharedByTwoSeams` + `...Bridge_ImplementsExactlyTheAggregateAndThePorts` + `...Bridge_PublicSurface_IsExactlyTheSeamsItServes` + `...Bridge_DeclaresExactlyThePinnedImplementationMembers` + `...Seam_DeclaresExactlyThePinnedMembers` + `...TheCensus_ReadsDeclarationsAndIgnoresMentions`; the built adapter is covered by `PatchBridgePortContractTests` (Integration) |
| Deployment/artifact verification | PowerShell + process | `tools/deploy.ps1` and deployment hash/file check. |

## Former PowerShell checks

The former `tools/check-*.ps1` scripts were removed after their logic was ported
into this test project. The mapping below records what replaced each one.

| Former check | What it checked | C# replacement |
|---|---|---|
| `check-architecture.ps1` | One type/file, logical line count, bool-flag debt, plus the Phase E guard suite below. | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` |
| `check-gamestate-isolation.ps1` | GameState project isolation from runtime/game/network dependencies. | `SourceShapeGateTests.GameStateIsolation_NoForbiddenReferencesOrTokens` |
| `check-item-authority.ps1` | Legacy item projection tables only mutated by their owners. | `SourceShapeGateTests.ItemAuthority_NoDirectProjectionMutation` |
| `check-no-legacy.ps1` | No removed dual-architecture markers in production source. | `SourceShapeGateTests.NoLegacy_NoRemovedDualArchitectureMarkers` |
| `check-command-authority.ps1` | Every `GameCommand` carries an `AuthorityKind` policy. | `SourceShapeGateTests.CommandAuthority_EveryGameCommandDeclaresAuthority` |
| `check-kernel-shape.ps1` | No string-keyed dictionaries / `Hashtable` kernel state. | `SourceShapeGateTests.KernelShape_NoStringKeyedStateOrHashtable` |
| `check-event-replay.ps1` | Event-replay matrix completeness; also guarded by `ReplayMatrixDataTests`. | `RepositoryGateTests.EventReplayMatrix_Completeness` |
| `check-entity-event-dispatch.ps1` | Entity event dispatch matrix. | `RepositoryGateTests.EntityEventDispatch_AllKindsCoveredInEveryTable` |
| `check-no-absolute-paths.ps1` | Tracked-file absolute machine paths. | `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` |
| `check-delivery.ps1` | Delivery checklist/forbidden-box integrity. | `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` |

> `NoAbsolutePaths_NoTrackedMachinePaths` enumerates `git ls-files --cached --others --exclude-standard`:
> tracked files plus untracked-and-not-gitignored ones, so a brand-new file IS checked before it is committed.
> It was tracked-only until 2026-09-20, which twice let a new file pass the local run and turn the gate red on
> the commit that added it (the adapter capability reporter's log literal, and this gate suite's own
> public-surface baseline header).
> The same scan reads a C# regex escape such as `:\s` in a verbatim string as a drive-letter path, so a pattern
> like `- Status:[ \t]*(.+?)` is the form that both means what it says and stays clean.

## Why the FQ-name rule uses Roslyn

The rule is a pure C# source-shape convention and is best served by a Roslyn
syntax tree rather than textual scanning. The gate:

- parses every `src/` and `tests/` C# file with official Roslyn APIs
  (`Microsoft.CodeAnalysis.CSharp`);
- reports type names and static/type member accesses that are fully qualified
  by a known namespace root when a `using`/alias is the natural cleaner form;
- allows the documented exceptions: namespace declarations, using directives,
  string literals/HotRepl strings, and enclosing-type member-name collisions
  (for example `System.IO.Path` inside a class that declares a method named
  `Path`).

## Related

- `AGENTS.md` Engineering Conventions #3, #5, #10, #14
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/FullyQualifiedNameGate.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/FullyQualifiedNameGateTests.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ApiSurfaceGate.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ApiSurfaceGateTests.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ProtocolNumberGateTests.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ProjectDirectionGateTests.cs` and its table `ProjectDirectionPolicy.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/SourceShapeGateTests.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/RepositoryGateTests.cs`
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/TestIsolationGateTests.cs`
- [`test-parallelization.md`](test-parallelization.md)
- `docs/evidence/delivery-checklist.md`
