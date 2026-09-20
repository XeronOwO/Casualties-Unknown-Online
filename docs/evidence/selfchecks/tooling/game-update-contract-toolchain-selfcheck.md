# Game-update contract toolchain — Self-Check (2026-09-20)

Delivery fact sheet for `docs/backlog/review/game-update-contract-toolchain.md` (High, the first
ticket of the 2026-09-20 review batch). The answer to "what did the game change?" used to be
assembled by chasing compile errors and whichever of the patch classes failed verification first;
this cycle lands a read-only toolchain that snapshots a game build, CLASSIFIES the differences
between two builds against the adapter's patch contracts, and writes the update-day flow down.

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Current behaviour | This cycle's change | Evidence |
|---|---|---|---|---|
| 1 | Game-facing probe | `GameAdapter.ProbeGame()` is four `typeof(...)` reads tested with `is not null`, which cannot fail for a compile-time reference — effectively a constant true | untouched (that is `todo/adapter-capability-catalog.md`'s subject); the toolchain reads the assembly instead of trusting a probe | `src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs` (`public bool ProbeGame()`, the four `typeof` reads) |
| 2 | Patch-target facts | `PatchInventory.BuildContracts()` (attributes + nine hand-declared dynamic rows) is the single source the runtime guard and the contract tests consume | the tool recovers the SAME rows from the adapter's metadata; a gate proves the two agree row by row | `src/CasualtiesUnknownOnline.GameAdapter/Patches/PatchInventory.cs`; `tools/.../Snapshot/PatchContractReader.cs`; `tests/.../ContractTool/PatchContractRowParityTests.cs` |
| 3 | Contract verdict rules | `PatchContractChecker` (existence, exact argument types, patch parameter names) + `VerifyMissing` (unconstrained target with several overloads = ambiguous) | the tool's contract lens mirrors those rules on snapshot data, so a report and the runtime guard cannot disagree about what "broken" means | `src/CasualtiesUnknownOnline.Runtime/Patching/PatchContractChecker.cs`; `tools/.../Diff/SnapshotDiffer.cs` (`AppendContractVerdicts`, `CompareTarget`) |
| 4 | Snapshot read | — (new) | Mono.Cecil reads the assembly as METADATA: nothing is loaded, executed or resolved, so a build that has never been launched can be snapshotted and no BepInEx/Unity/game directory is needed | `tools/.../Snapshot/GameAssemblyReader.cs` (`AssemblyDefinition.ReadAssembly(..., new ReaderParameters { InMemory = true })`) |
| 5 | Snapshot artifact | — (new) | canonical JSON, byte-reproducible by construction (sorted rows, fixed property order, no timestamp/machine fact, culture-free numbers), with a self-declared census the reader verifies | `tools/.../Snapshot/SnapshotWriter.cs`, `SnapshotReader.cs`; `tests/.../ContractTool/SnapshotJsonTests.cs`, `GameAssemblySnapshotTests.cs` |
| 6 | Classification | — (new) | each contract row gets exactly one verdict (removed or renamed with same-shape rename candidates, signature changed, Harmony target ambiguous, unchanged-needs-a-semantic-look); everything else is tiered contract-adjacent / outside the lens and still reported | `tools/.../Diff/SnapshotDiffer.cs`, `MemberDiffer.cs`; `tests/.../ContractTool/ContractDiffClassificationTests.cs`, `ContractToolFixtureTests.cs` |
| 7 | Plugin dependency graph | the plugin builds from `src/` only | untouched: the tool references no `src/` project and no `src/` project references it; the test project references it to test it | `tools/.../CasualtiesUnknownOnline.ContractTool.csproj` (no `ProjectReference`); `CasualtiesUnknownOnline.slnx` |
| 8 | Production runtime | — | no production/wire/save change, no `ProtocolVersion` bump: every production file is bit-identical to HEAD | `git status` (no `src/` change) |

## Design

- **Metadata, not reflection.** A snapshot must work on a build you have not launched (an incoming
  update, a rollback copy), so the tool parses the file instead of loading it. That also keeps it out
  of the plugin's dependency graph by construction rather than by discipline.
- **One source of facts, pinned by a gate instead of by shared code.** The obvious way to avoid drift
  would be to call `PatchInventory.BuildContracts` from the tool — but that would put the Game
  Adapter (the only project allowed to bind the copyrighted game assemblies) into the tool's
  dependency set. The tool therefore recovers the rows itself and a test asserts equality row by row,
  plus that the only rows it cannot see are the nine hand-declared dynamic ones and that its row
  count equals `CountTargets()` — a stronger guarantee than sharing a helper, because it also catches
  a divergence the helper would have hidden.
- **Classify, do not list.** The verdict rules mirror the runtime guard's: a missing target, an
  argument-type mismatch that must never fall back to a name-only match, an unconstrained target that
  gained an overload, and a patch parameter name the target no longer carries (Harmony binds patch
  arguments by name — the silent detach no existence check can see). A contract that cannot be broken
  structurally is not dropped either: it lands under "unchanged, still needs a semantic look", which
  is exactly the part the structural half cannot clear.
- **Tier by distance from the lens.** Contract verdicts first, then changes to members of the types
  the contracts target, then everything else — counted and listed, never silently dropped, but not
  turned into hook work it is not.
- **Recorded boundaries.** The nine hand-declared dynamic contracts carry no attribute; a contract
  whose target type lives outside the snapshotted assembly (the `SceneManager.LoadScene` hook)
  reports unresolved; no method BODY is compared, so the semantic half stays in
  `future/adapter-shell-verification-harness.md`; enum member ORDER is not compared (values are).
  All four are stated in the report's own limits section. Where the tool is deliberately STRICTER
  than the runtime guard — a re-typed parameter on a name-resolved target installs fine but is
  reported as a signature change — the report says so, because the guard's silence there is exactly
  what makes the row worth reading.

## Verification design

What the machine proves, and how:

- the verdict rules, one case per classification, on explicit before/after snapshots
  (`ContractDiffClassificationTests`);
- the extraction half end to end on two REAL emitted revisions of one assembly plus its adapter —
  the acceptance's "two builds produce a report"
  (`ContractToolFixtureTests` over `ContractFixtureAssemblies`, written with Cecil at test time);
- the CLI the runbook calls, as a process: both commands and the four exit codes
  (`ContractToolCliTests`);
- artifact integrity: byte-reproducibility, round trip, escaping, and refusal of an
  unknown-schema or census-mismatched snapshot (`SnapshotJsonTests`);
- the real shipped build: byte-reproducibility and a census floor on `Assembly-CSharp`, plus a
  self-diff whose lens must resolve every contract inside the assembly
  (`GameAssemblySnapshotTests`);
- the one-source-of-facts claim (`PatchContractRowParityTests`).

What the machine CANNOT prove, stated so nothing is over-claimed: whether a member that is still
shaped the same MEANS the same thing. That is the live-game half
(`future/adapter-shell-verification-harness.md`), and this cycle does not claim it.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Patch-target row recovery | metadata reader reproduces `PatchInventory.BuildContracts` | `PatchContractRowParityTests.ToolRows_EqualTheAdaptersOwnContractRows` (205 rows), `...CoverEveryAttributedPatchClass`, `RowsTheToolCannotSee_AreExactlyTheHandDeclaredDynamicOnes` (nine), `AdapterAndToolAgreeOnTheContractCount` |
| Contract verdicts | one verdict per contract row, rules mirroring the runtime guard | `ContractDiffClassificationTests` (16 cases), `GameAssemblySnapshotTests.SelfDiff_ResolvesEveryContractInsideThisAssemblyAndBreaksNothing` |
| Classification vocabulary | removed/renamed (+candidates), signature changed, ambiguous, field shape, enum value, unchanged, added | `ContractToolFixtureTests` (12 cases) over a fixture pair built to carry each one; `ContractDiffClassificationTests` for the rules in isolation |
| Snapshot reproducibility | same input → byte-identical output | `GameAssemblySnapshotTests.Snapshot_OfTheRealGameAssembly_IsByteReproducible`, `SnapshotJsonTests.Write_IsByteIdenticalAcrossRuns` |
| Snapshot integrity | declared census verified on read; unknown schema refused | `SnapshotJsonTests.Reader_RefusesACensusThatDisagreesWithItsRows`, `Reader_RefusesAnUnknownSchema`, `Reader_RefusesAMissingFile` |
| Read-only tool | no `src/` reference in either direction; no game-directory write | `tools/.../*.csproj` (no ProjectReference); `git status` shows no `src/` change |
| Update-day runbook | snapshot → snapshot → diff → contract tests → replay → verdict | `docs/development/game-update-runbook.md`; linked from `references/README.md` and `docs/README.md`; the CLI exit codes it quotes are pinned by `ContractToolCliTests` |

## Delivery evidence (2026-09-20)

- Contract-tool family: **61/61 passing** (`dotnet test CasualtiesUnknownOnline.slnx --filter
  "FullyQualifiedName~Tests.ContractTool"`).
- Full suite with build: **3 678 passed / 0 failed** (main suite, 56 s) — 61 more than the 3 617
  baseline recorded before this cycle, i.e. exactly this cycle's cases.
- Snapshot of the shipped build: 488 types, 3 318 methods, 2 968 fields, 290 properties, 111 enum
  members, 1 617 serialized fields (2.3 MB of canonical JSON), plus 205 attribute-declared contract
  rows when the adapter assembly is embedded.
- Independent adversarial review (fresh context, frozen tree, 2026-09-20): two majors, five minors,
  two nits — all fixed in this commit. The substance: (1) an unconstrained target whose parameter
  TYPE moved was reported as structurally unchanged, because the contract comparison inherited the
  runtime guard's name-based view and the member-level pass was suppressed — now the parameter types
  are compared first, with `UnconstrainedTargetWithARetypedParameter_IsClassifiedAsSignatureChanged`
  as the regression; (2) a malformed or null-bearing snapshot crashed the process instead of exiting
  3 — `SnapshotReader` now refuses what it cannot trust and the CLI catches `JsonException`, with
  `MalformedSnapshot_ExitsThree` and `Reader_RefusesRowsThatAreNull` as the regressions. Also fixed:
  a removed contract target reported twice, a contract that becomes resolvable only in the new build
  hiding the member change underneath, ambiguous report identities for patch classes sharing a simple
  name (the report now uses the namespaced name, the parity gate still compares the simple one), an
  unnecessary fully qualified name, the ticket's stale "113 patch classes", and a runbook step whose
  output directory did not exist.
