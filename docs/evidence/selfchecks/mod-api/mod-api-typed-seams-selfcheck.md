# Self-check — typed seams (the last two `object` shapes on the mod-visible contract)

Cycle: 2026-10-09. Ticket: `docs/backlog/review/mod-api-typed-seams.md`. Decision: 250.

Scope landed: `IModNativeApi`'s untyped invoke path (deleted rather than retyped), the Runtime → Game
Adapter native-API seam, `ModNativeApiPolicy`, and `IStartingSupplyBehaviour`'s move off the mod-visible
surface. The sibling tickets of the same sweep (`mod-content-typed-registration`,
`mod-api-no-opaque-envelopes`) landed earlier; this is the one that closes it.

## 1. What landed, mechanism by mechanism

| Mechanism | Change | Evidence |
|---|---|---|
| `IModNativeApi` | `TryInvoke(string, object?[], out object?)` deleted; the surface is `CanAccess`, `CanInvoke(string)` as the availability probe, and one typed projection per operation | `ApiSurfaceGateTests.AbstractionsPublicSurface_MatchesTheReviewedBaseline` (two tombstones), `ModNativeApiTests` (7 cases) |
| `IModNativeApiProvider` (the Runtime → Game Adapter seam) | `TryInvoke(string, object?[], out object?)` → `TryGetLocalPlayerState(out IModNativeLocalPlayerState state)`; `IsRegistered(string)` stays | `GameAdapterNativeApiContractTests.GameAdapter_ImplementsModNativeApiProvider` (reflective — the test project does not compile-reference GameAdapter), `ModNativeApiTests.WithPermission_ReturnsTheProvidersTypedProjection` |
| `DisabledModNativeApiProvider` | follows the seam; every operation is refused, which is the Runtime-only composition's default | `ModNativeApiTests.ProviderThatRefusesEveryOperation_IsRefused` |
| `ModNativeApiPolicy` | 133 → 39 lines: the admitted value surface, its four caps and the rank-and-element-type array rule are deleted, not moved; the operation-id shape rail stays | `ModNativeApiTests.PolicyRails_AreExact`, `ModNativeApiTests.MalformedOperationId_IsRefusedBeforeTheProviderSeesIt` |
| `GameAdapter`'s provider half | the operation/argument dispatch disappears; the body projection *is* the method body | `GameAdapterNativeApiContractTests` (3 cases), build 0/0 |
| `IStartingSupplyBehaviour` | `Abstractions` → `CasualtiesUnknownOnline.Runtime.Session.World`; the `object` body/item handles stay, with the reason in its own doc | baseline (four tombstones), `StartingSupplyCoordinatorTests` (its fake implements the interface from the new namespace and needed no edit — it already imported it) |
| `GameStartingSupplyTarget`, `StartingSupplyCoordinator` | their usings follow the interface | build 0/0; focused 64/64 |
| Docs | the native-operations section of both blocks (the sample no longer calls `TryInvoke`), decision 250 — which also records the supersession of decision 249's native-value clause and 180's moved seam — the policy page's debt sentence, the backlog index, one dated done-ticket's location claim | `DocumentationTreeGateTests.EveryPair_IsRecordedAtItsCurrentContents` (both hashes re-recorded), `BacklogIntegrityGateTests`, `BacklogReferenceGateTests` |
| Tests | `ModNativeApiTests` reworked rather than dropped (7 cases); `FakeModNativeApiProvider` records the projection that was reached instead of a generic call log | behaviour 4850/4850, focused 64/64 |

## 2. The family audit (no piecemeal fix)

- **The contract-wide claim, measured rather than asserted.** A scan of the whole `Abstractions` tree for
  `object` leaves exactly two signature occurrences, both BCL overrides: `ContentId.Equals(object?)` and
  `ModValue.Equals(object?)`. Every other match is the English word in a doc comment ("a plain data
  object"). So "no untyped hole on the mod-visible contract" is a whole statement after this cycle, not
  half of one.
- **No leftover of the deleted entry.** A search for `TryInvoke` finds no code site anywhere — the dynamic
  path is gone from `src/`, `tests/` and `tools/`, with no shim beside the typed projection. The only hits
  left are the records that have to name what was deleted: the baseline's five tombstones, decision 250, the
  ticket, this self-check and the MANIFEST row.
- **The moved seam's whole reference face.** `IStartingSupplyBehaviour` has exactly three referencing
  sites — its implementation (`GameStartingSupplyTarget`), its consumer (`StartingSupplyCoordinator`) and
  the test fake — and all three follow the move. Nothing in `Abstractions` referenced it; that was the
  ticket's own test for whether it is a mod contract at all.
- **Same shape, same layer.** The interface's own doc names `IModItemSpawner` as its shape; that seam lives
  in the Runtime, beside `IModEntitySpawner`, `IModTilePlacer` and friends. The new home
  (`Runtime.Session.World`) puts it with the other adapter-implemented seams (`INativeWorldFacts`,
  `IWorldFactSource`), and `Abstractions` has no `internal` type at all — checking that settled the
  alternative of keeping it there behind `internal` + `InternalsVisibleTo`.
- **The ticket's third row, re-checked.** `IModCommands.TryExecute(string, IReadOnlyList<string>, …)` is
  unchanged and stays: a console command is the mod's own, its name and text arguments are its natural
  shape, and a typed call per command would have to be declared by the mod itself.
- **The knowledge the deletion would have taken with it.** Decision 249's net48 measurement (a signed and
  an unsigned array of the same width are indistinguishable to `isinst`/type patterns while their IL
  tokens differ) has no `src/` consumer any more, so decision 250 records both the supersession and the
  fact, and the ticket's limits repeat it. Deleting the scan did not delete the lesson.
- **Deleted rather than moved, twice.** The value-surface scan and the generic invoke path both leave
  nothing behind: no "safety" fallback validator, no deprecated overload.

## 3. Tests

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors.
- `dotnet format CasualtiesUnknownOnline.slnx` — exit 0, and `git status` showed no file it wanted to
  change.
- Behaviour suite (`CasualtiesUnknownOnline.Tests`, net48) — **4850/4850**.
- Focused run (`--filter "FullyQualifiedName~ModNativeApi|FullyQualifiedName~StartingSupply|FullyQualifiedName~GameAdapterNativeApi"`)
  — **64/64** (53 `Session` + 7 `Mods` + 3 `Patching` + 1 `Persistence`; the `~Value`-only spelling selects
  nothing at all and still exits 0, which is why the property name is written out in every clause).
- Normative gates — **572/573**, the single red being
  `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` against this cycle's own reset
  checklist; it goes green when the boxes are filled (`573/573`).
- Contract baseline after the change — **860 entries / 67 tombstones** (was 865 / 62: five removals, five
  tombstones).

## 4. Verification (how the runtime proves it)

- The contract's shape is proven by the baseline gate and the review that updates it, deliberately not by a
  scan — which is what rule 15 says and what this cycle is the first full test of: both removals carry
  tombstones naming their reason, and the replacement signatures are recorded one line at a time.
- The typed projection's failure paths are three separate cases against the real composition root
  (`TestNode` → `ModService` → `ModContext` → adapter → provider): no permission, no body to project, and a
  provider that refuses everything. The happy path asserts the provider's own instance comes back unchanged.
- The Game Adapter's implementation is covered reflectively (`GameAdapterNativeApiContractTests`), because
  the test project references GameAdapter with `ExcludeAssets="compile"` — it binds Unity/game assemblies
  and cannot be loaded into the test host.
- Nothing here needs a game process: this cycle changes a contract's shape and one seam's layer, and adds no
  player-visible behaviour. The acceptance batch still re-runs the mod-API rows against deployed DLLs as
  usual.

## 5. The independent review (2026-10-09)

`docs/evidence/selfchecks/mod-api/mod-api-typed-seams-review.md` is the report, run in a fresh context
against the frozen tree at FULL tier. Verdict: **no blocker**; two majors, three minors, seven nits, each
dispositioned here in the same commit.

- **M1 (major)** — the deleted value surface was still documented as LIVE on both
  `docs/{en,zh}/internals/permissions-and-security.md`: the four caps and the "rejected on both sides of the
  adapter seam" sentence this cycle deleted. Fixed: both paragraphs rewritten to the fact the projection's
  type now states, and the pair's hashes re-recorded.
- **M2 (major)** — the rule's own "the debt is the three sweep tickets" sentence survived in three of its
  four sites after the English policy page was amended: `AGENTS.md` rule 15, `docs/evidence/normative-gates.md`
  row #15, and the Chinese policy page (the pair of the sentence that WAS amended). Fixed: all four now say
  the debt is paid, with the Chinese pair re-recorded.
- **m1 (minor)** — the recorded focused-run filter silently selected zero tests and exited 0. Fixed: the full
  `FullyQualifiedName~…` syntax is recorded here and in the ticket, with the decomposition.
- **m2 (minor)** — a dangling `todo/` pointer to the moved ticket survived in the stage-B review. Fixed:
  re-pointed to `review/`.
- **m3 (minor)** — `ModNativeApiOperations`' summary still promised invocation "by the same string", and
  `IModNativeApi.CanAccess`' summary said "every invoke method". Fixed: both reworded to the probe/projection
  split.
- **N1** — `MalformedOperationId_IsRefusedBeforeTheProviderSeesIt` could not pin "before the provider sees
  it". Fixed: the fake counts registration probes, the malformed case asserts zero and the unregistered case
  asserts one.
- **N2** — §2's "a repository-wide search returns nothing" was wider than the tree. Fixed: the sentence now
  names the code surface and the records that have to mention the deletion.
- **N3** — "adding an operation" omitted two of its four edit sites. Fixed: both blocks name the constant in
  `ModNativeApiOperations` and the id in the Game Adapter's `IsRegistered`.
- **N4** — the moved seam's own doc misplaced `IModItemSpawner` (it is in `Runtime.Session.Mods`, not this
  namespace) and claimed a dedicated-server consumer that exists nowhere. Fixed: both sentences rewritten.
- **N5** — nothing stated that `IsRegistered` and the projections must agree. Fixed: the seam's summary now
  says so, naming the lying probe as the failure.
- **N6** — the `Create` tombstone was looser than its three siblings. Fixed: it names the test host's
  substitution too.
- **N7** — the ticket's status line ran ahead of the tree and its test list claimed an edit that does not
  exist. Fixed: the status is now true, and the list says the starting-supply suite's fake needed no edit.

The review reproduced every figure the cycle states (build 0/0, behaviour 4850/4850, `ModNativeApiTests`
7/7, focused 64/64 decomposed 53 + 7 + 3 + 1, gates 572/573 with only the checklist red, baseline 860
entries / 67 tombstones, `ModNativeApiPolicy` 133 → 39, `GameAdapter.cs` 593 → 588, 39/39 alignment pairs)
and could not break the delete-rather-than-retype argument: the deleted dynamic path's whole reachable
behaviour was already the typed projection. What it could not check: `dotnet format` (forbidden to it,
because it rewrites the frozen tree) and anything needing the game.

## 6. Limits (recorded, not hidden)

- `ContentId.Equals(object?)` and `ModValue.Equals(object?)` remain: a BCL override is not an untyped hole,
  and they are the only two left.
- `GameAdapter.cs` is 588 lines — 12 from the architecture gate's 600. This cycle made it smaller, not
  larger, but the margin is thin and worth naming before the next change lands in it.
- `mod-authored-effects.md` inherits the handle rule from decision 250; it is not implemented here.
- The rank-and-element-type array rule has no executable site left, so nothing fails if a future change
  re-introduces a `T[]` pattern test somewhere else. The rule survives as a recorded measurement, not as a
  gate.
