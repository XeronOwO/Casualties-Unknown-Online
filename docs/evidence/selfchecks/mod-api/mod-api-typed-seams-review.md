# Typed seams (the untyped invoke path and the moved supply seam) — independent review

Independent adversarial review of the uncommitted change set "typed seams: `IModNativeApi`'s untyped invoke
path deleted rather than retyped, the Runtime → Game Adapter seam narrowed to one typed projection,
`ModNativeApiPolicy` shrunk and `IStartingSupplyBehaviour` moved off the mod-visible surface", run in a
fresh context against the FROZEN working tree at `a0f1da76` (branch `master`; 21 modified paths, two
deletions and three untracked additions — the dirty tree is the artifact under review). Risk tier: **FULL** —
a mod-visible third-party contract, a framework seam between assemblies, four projects and the documentation
set move together. Read-only: the only file this review writes is this report, `dotnet format` was not run
(it rewrites files), no background job was started and every search stayed inside this repository. The
change's claims were attacked, never the evidence offered for them; every claim was re-opened in the tree and
every suite that runs without the game was run.

Ticket: `docs/backlog/review/mod-api-typed-seams.md`. Self-check:
`docs/evidence/selfchecks/mod-api/mod-api-typed-seams-selfcheck.md`. Decision: entry 250.

## 1. Verdict

**No blocker. Two majors, three minors, seven nits.**

The mechanism is sound and the deletion is the right call. The dynamic entry's whole reachable behaviour on
this tree was the typed projection that replaced it (the old Game Adapter dispatch refused every operation
except `local.player.state` and refused any non-zero argument count), the erased-type scan I ran myself over
all 102 `Abstractions` files finds `object` in exactly two signatures, both BCL overrides, and the moved seam
really is a framework seam: one production implementation, one production caller, both in the Game Adapter,
with the test host substituting its own. Every figure the change states reproduces (§2), including the one
claim I could have expected to fall — the dynamic path's argument machinery, the four caps and the
rank-and-element array rule have no caller anywhere in `src/`, so deleting them removed no reachable guard.

What did fall:

- **M1 (major)** — the deleted value surface is still documented as LIVE, in both blocks, on a security
  page: `docs/en/internals/permissions-and-security.md` and `docs/zh/internals/permissions-and-security.md`
  still state the four caps and the "rejected on both sides of the adapter seam" rejection this change
  deleted. Every number in that paragraph (16 arguments, 4096-character strings, 64 KiB values, 1024-element
  arrays) was true at HEAD and is false now; the change touches neither page, so the documentation set
  contradicts the tree in exactly the area the ticket claims lost nothing.
- **M2 (major)** — the supersession the change itself created ("all three sweep tickets have landed, so the
  contract carries none of the debt") was written into ONE of the four places that state it. `AGENTS.md`
  rule 15, `docs/evidence/normative-gates.md` row #15 and `docs/zh/reference/modification-policy.md` all
  still say the debt IS the three tickets — including the binding rule file and the rule-to-gate map — and
  the Chinese block of the very page that was amended now contradicts its English pair.
- The three minors are a recorded focused-run command that selects zero tests (and exits 0), a dangling
  `todo/` path to the moved ticket that survives in the evidence set the previous cycle had already swept
  for its own move, and one stale mod-visible XML summary that still promises invocation "by the same
  string".
- The seven nits are a test name that promises more than its assertion, three self-check/doc sentences that
  are looser than the tree, an omission in the new "adding an operation" instruction, two imprecise tombstone
  or doc phrases, and a status line that says the review already ran.

## 2. What was run (evidence)

- `dotnet build CasualtiesUnknownOnline.slnx --nologo` → **0 warnings, 0 errors**.
- `dotnet test tests\CasualtiesUnknownOnline.Tests --nologo -v q` → **4850 passed / 4850** (net48).
- `dotnet test tests\CasualtiesUnknownOnline.Tests --nologo -v q --no-build --filter
  "FullyQualifiedName~ModNativeApi|FullyQualifiedName~StartingSupply|FullyQualifiedName~GameAdapterNativeApi"`
  → **64 passed / 64**, and `--list-tests` decomposes it exactly: 53 `Session`, 7 `Mods`, 3 `Patching`,
  1 `Persistence`. The filter string the ticket and the self-check record
  (`FullyQualifiedName~ModNativeApi|~StartingSupply|~GameAdapterNativeApi`) selects **no test at all** and
  exits 0 — see m1.
- `dotnet test tests\CasualtiesUnknownOnline.NormativeGates.Tests --nologo -v q` → **573 total, 572 passed,
  1 failed** — the single red is `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`, which
  reports `Delivery gate failed (3 issue(s), 5 boxes checked)`. So `ApiSurfaceGateTests`,
  `DocumentationTreeGateTests`, `BacklogReferenceGateTests`, `BacklogIntegrityGateTests`,
  `SelfcheckManifestGateTests` and the shape gates are green on the frozen tree.
- `--filter "FullyQualifiedName~ModNativeApiTests"` → **7 passed / 7**, so the ticket's "7 cases, reworked
  rather than dropped" is a count that holds (7 before, 7 after).
- Baseline arithmetic, read with `File.ReadAllLines` (the shell's text pipeline mangles non-ASCII and must
  not be trusted for counts): `docs/contracts/abstractions-api-baseline.txt` is 940 lines — 12 comments,
  1 blank, **860 surface entries** (757 `member|` + 103 `type|`) and **67 tombstones** (64 `member`, 3
  `type`). Decomposition against HEAD: 860 + 5 = 865 and 67 − 5 = 62, so "was 865 / 62" is exact.
- Line counts and their diffs: `ModNativeApiPolicy.cs` **39** now, and `git diff --numstat` gives 6 added /
  100 removed, so HEAD was **133** — the claimed "133 → 39" is exact. `GameAdapter.cs` is **588** lines and
  the same diff gives 3 added / 8 removed, so HEAD was 593: the ticket's "made it smaller, not larger" is
  true (and 12 lines under the `SourceShapeGateTests` 600 aggregate limit, which is green).
- `object` over the whole contract: all 102 `.cs` files under `src/CasualtiesUnknownOnline.Abstractions`
  contain two occurrences in a signature — `ContentId.Equals(object? obj)` and
  `ModValue.Equals(object? obj)` — and every other hit is the English word in a doc comment. Rule 15's other
  spellings are clean too on the public surface: the only `byte[]` are `ModValue`'s private field and private
  constructor, and the only `Delegate` is `AttributeTargets.Delegate` in the attribute's own `ValidOn` list.
- `TryInvoke` across the repository: no hit in `src/`, `tests/`, `tools/` or `SampleMod` — the remaining hits
  are the records that must name the deletion (the five tombstones, decision 250, the ticket, the self-check,
  the MANIFEST row) plus one stale historical pointer (m2).
- Alignment: all **39** pairs in `docs/standard/alignment.txt` reproduce under `git hash-object`
  (0 mismatches), including the three this diff re-recorded (`reference/mod-api` in both blocks and the
  English `reference/modification-policy`); the Chinese `modification-policy` hash is unchanged because the
  file is unchanged — which is exactly the drift M2 names.
- Index budget: the moved ticket's row is **142** characters against `BacklogIntegrityGateTests`'
  `IndexLineBudget = 160`, and it sits under `### Review` with the ticket in `review/`.
- Reference integrity: `docs/backlog/todo/mod-api-typed-seams.md` is gone, `docs/backlog/review/…` exists,
  the README row moved Todo → Review, `docs/backlog/review/mod-content-attribute-declarations.md` re-points
  (that path moved from `todo/` when stage B of that ticket landed),
  and the dated done-ticket's location claim is updated. One stale `todo/` pointer survives (m2).
- The deleted value surface's callers: `ModNativeApiPolicy` is called from exactly one production file
  (`ModNativeApiAdapter.CanInvoke` → `IsValidOperation`), and no `MaxArguments` / `MaxStringLength` /
  `MaxValueBytes` / `MaxArrayLength` / `IsValidArguments` / `IsSafeResult` reference survives in `src/`,
  `tests/` or `tools/`.

## 3. Claim by claim

| # | Claim | Verdict | Where it was checked |
|---|---|---|---|
| 1 | `TryInvoke(string, object?[], out object?)` cannot survive rule 15, so it is DELETED rather than retyped; the surface is `CanAccess` + `CanInvoke(string)` + one typed projection per operation; no reference to the old path remains anywhere | **Holds** | `IModNativeApi.cs` declares exactly those three members; the baseline carries the removal plus a tombstone and `ApiSurfaceGateTests` is green; a repository-wide `TryInvoke` search finds no code site, no shim and no overload. `ModNativeApiOperations` keeps the id, and `CanInvoke` is its only consumer |
| 2 | Deleting is the RIGHT call rather than retyping the arguments as `ModValue`: a surviving generic entry would let the Game Adapter widen the contract by editing `IsRegistered` alone, bypassing the baseline review | **Holds — reproduced, see §4.3** | The old adapter dispatch refused every operation except `ModNativeApiOperations.LocalPlayerState` and refused `arguments.Length != 0`, so the dynamic path's entire reachable behaviour was already the typed projection — nothing a mod could do was removed. On the other side, the operation set has no other registration site than `GameAdapter.IsRegistered` and the new typed projections, and no gate covers `Runtime`/`GameAdapter`, so a `ModValue`-shaped generic entry really would make a new operation mod-reachable with no baseline line to review |
| 3 | `ModNativeApiPolicy` shrank 133 → 39 lines; its value surface, four caps and rank-and-element array rule are deleted rather than moved, and nothing of value was lost | **Holds on the code, FALSIFIED on the documentation** | 133 → 39 exact (§2); the deleted members have no caller left anywhere; the only production provider returns the DTO or `null` and the Runtime-only provider refuses everything, so no reachable path depended on the scan. But the surface it policed is still described as live on both permissions pages — M1 |
| 4 | `IStartingSupplyBehaviour` is a framework seam, not a mod contract, so its move to `CasualtiesUnknownOnline.Runtime.Session.World` is correct, its `object` handles are correct there, and decision 180's reason for the seam survives | **Holds** | Its whole reference face is three files: `GameStartingSupplyTarget` (implementation), `StartingSupplyCoordinator` (caller, both Game Adapter) and `StartingSupplyCoordinatorTests`' fake. Nothing in `Abstractions` referenced it, and nothing in `Runtime` does either. Its new home is the established one for adapter-implemented seams (`INativeWorldFacts`, `IModItemSpawner`, `IWorldFactSource` are all public in `Runtime`, implemented by the adapter and substituted by the test host) and `Runtime` carries no game reference, so `object` is the only available handle spelling there. Decision 180's clause — the seam is what makes the decision runnable in the test host — still reads true: the coordinator takes it by constructor and the suite substitutes it |
| 5 | The contract carries no erased type in any signature except the two BCL overrides, verified by a scan of the whole `Abstractions` tree | **Holds — reproduced with my own scan** | §2: two signature occurrences in 102 files, both `Equals(object?)` overrides; rule 15's other spellings are clean on the public surface. The self-check's phrasing ("a scan") is about the *evidence*, and the rule it honours is AGENTS.md rule 15's "NOT by a scan" — see §5 |
| 6 | The numbers: build 0/0; `dotnet format` clean; behaviour 4850/4850; focused 64/64; gates 572/573 with only the checklist red; baseline 860 / 67 tombstones | **Holds except format and the focused filter** | Every number reproduced in §2 except `dotnet format`, which this review is forbidden to run (the build's `EnforceCodeStyleInBuild` + warnings-as-errors covers the analyzer half but not whitespace), and except that the focused figure is only reproducible with the full filter syntax (m1). The 572/573 red is exactly the checklist, with 3 required boxes still open against this cycle's own reset |
| 7 | Every document the change touches agrees with the tree: decision 250, the amended sentence in 180, both `mod-api.md` blocks, the policy page's debt sentence, the index row, the moved ticket's path everywhere, one dated done-ticket and the re-recorded alignment hashes | **Partly falsified** | What holds: decision 250's statement matches the code line by line, decision 180's amended sentence is true, both `mod-api.md` blocks carry the same new sample and the same three amended bullets, the index row is a 142-character pointer under `### Review`, the dated done-ticket now names the new namespace, and the three re-recorded hashes reproduce. What fails: "the policy page's debt sentence" was amended on the English side only while three sibling statements of the same fact were left standing (M2), and "the moved ticket's path everywhere" misses the stage-B review (m2). The one page the change should have touched and did not is the permissions pair (M1) |

## 4. The apparatus, not the wording

### 4.1 Do the rewritten cases pin what their names claim?

- `WithPermission_ReturnsTheProvidersTypedProjection` — **discriminating.** It asserts `Assert.Same(expected,
  state)` (the provider's own instance comes back unchanged, so no wrapper is invented), then
  `Assert.Equal(1, fake.LocalPlayerStateCalls)` (reached exactly once — a double call would fail), then every
  projected field. Its predecessor asserted a generic call log; this one is strictly stronger.
- `NoLocalBodyToProject_IsRefused` — **discriminating, and it pins the distinction the change claims to
  keep:** `CanInvoke` is asserted TRUE (the operation is registered) while the projection returns false with a
  null out value. That is the one case where "registered" and "available" come apart, and it is the case that
  keeps `CanInvoke` from being a second name for the projection.
- `ProviderThatRefusesEveryOperation_IsRefused` — real: the fake's `Available = false` is checked inside
  `TryGetLocalPlayerState` as well as in `IsRegistered`, so the refusal comes from the provider, not from the
  fixture's `Result`.
- `MalformedOperationId_IsRefusedBeforeTheProviderSeesIt` / `UnregisteredOperation_IsNotInvokable` /
  `PolicyRails_AreExact` — they pin the rail that is left (`MaxOperationLength`, the character set, the empty
  id) through the production composition root and through the policy directly. See N1 for what the first
  name does not pin.
- **The deleted cases' replacements are genuinely equivalent in coverage, not a hole.** The old suite's three
  removed legs were unsafe arguments, an argument-count cap and an unsafe provider *result type*: no
  argument can be passed at all now (`TryGetLocalPlayerState` takes none), and a non-DTO result cannot be
  produced (`out IModNativeLocalPlayerState state`). The old suite's *code paths* for those three are gone
  with them, so nothing reachable is untested — the two that remain reachable (permission, no-body) have
  cases, and the provider-shaped refusal has one as well. What is genuinely no longer assertable is
  assertable by the compiler instead, which is the point of the change.

### 4.2 Is the typed path reachable in production, and does the adapter still log its refusals?

- The chain is the production one and single: `GameAdapterComposition` registers
  `services.Replace(ServiceDescriptor.Singleton<IModNativeApiProvider>(p => p.GetRequiredService<GameAdapter>()))`
  in the adapter composition, `ModComposition` registers `new DisabledModNativeApiProvider()` in the
  Runtime-only one; `ModService` → `ModLifecycle` → `new ModNativeApiAdapter(manifest, nativeApiProvider,
  log)` → `ModContext.NativeApi`. The tests drive that root (`TestNode.CreatePair` replaces only
  `IModNativeApiProvider`), so the suite exercises the production adapter, not a copy.
- Logging: the permission refusal is still logged in one place (`ModPermissionGate.Try` →
  `"[Mods] {ModId} does not declare {Permission} — the call is refused."`), the provider refusal logs
  `"native operation {Operation} is not available (no local body, or the Game Adapter does not provide it) —
  refused."`, and the success path logs `"invoked native operation {Operation}."`. The three log lines the
  change removed (malformed id / unsafe arguments / unsafe result) guarded states that can no longer be
  reached; no reachable branch lost its line.
- `CanInvoke` is still meaningful, but its meaning narrowed and the change says so: with a compile-time
  operation set it answers permission + id shape + provider registration, i.e. "does this adapter build
  provide the operation". The old adapter's dispatch refused an unknown operation *after* the policy check,
  so the probe and the act were consistent by construction; now they are two independent answers (N5).

### 4.3 Falsifying claim 2 myself

I tried to make the deletion lose something a mod could reach:

- **Was the dynamic path more capable than the projection?** No. The HEAD implementation read
  `if (operation != ModNativeApiOperations.LocalPlayerState || arguments.Length != 0) { return false; }`, so
  the only invocable operation was the one the projection now exposes and no argument ever crossed. There was
  no second operation in the registry (`ModNativeApiOperations` holds exactly `LocalPlayerState`), and no
  mod in the tree called it (`ModExample` has no `NativeApi` use at all).
- **Was there a value the old scan refused that the new signature admits?** No. The old admitted set was
  `null`, strings, numeric primitives, encodable `ModValue`, one-dimensional primitive arrays and the DTO;
  the new seam admits exactly the DTO or `null` for this operation, and a future projection declares its own
  type. The scan's refusals were already unreachable through the Game Adapter.
- **Could the Game Adapter widen the contract without a baseline line now?** Not for a *mod-visible* call:
  adding an operation means adding a method to `IModNativeApi` (baseline, reviewed, gate-enforced) and to the
  Runtime seam. The probe can be widened by `IsRegistered` alone, but a probe is not a capability — which is
  exactly the ticket's argument, and I could not break it.

### 4.4 The moved seam's placement, checked against the repository's own pattern

`Runtime.Session.World` already holds `INativeWorldFacts` (public, implemented by `NativeWorldFacts` in the
Game Adapter, substituted by `FakeNativeWorldFacts` in tests) and `Runtime.Session.Mods` holds
`IModItemSpawner` / `IModEntitySpawner` / `IModTilePlacer` (public, implemented by `GameAdapter`, with
`Disabled*` implementations in the Runtime-only composition and `Fake*` in tests). The moved interface has
no `Disabled*` implementation because its only consumer, `StartingSupplyCoordinator`, is constructed only in
`GameAdapterDomains` — so a Runtime-only composition never needs one. The placement matches the pattern; the
alternative the ticket's own Related line names (`internal` + `InternalsVisibleTo`) would have been a first
for that assembly (it has no `internal` type today and no `InternalsVisibleTo`), and the chosen home keeps
the type off the assembly a mod may reference either way.

## 5. Contract shape (the changed baseline lines, judged)

No scan enforces this, and the change says so in the right places: `AGENTS.md` rule 15, the amended policy
page and `docs/evidence/normative-gates.md` row #15 all state "review, deliberately not a gate"; I confirmed
that no gate in `CasualtiesUnknownOnline.NormativeGates.Tests` mentions erased types at all, so there is no
claim of a scan to falsify.

| Changed baseline line | Judgement |
|---|---|
| `member\|Advanced\|…IModNativeApi.TryInvoke(string operation, object?[] arguments, out object? result)\|method\|-> bool` — removed, tombstoned | Fails all three questions: the arguments are an erased envelope and the result is an erased handle. Its removal is the only shape that satisfies rule 15 short of a per-operation projection, and the tombstone's reason ("the mod-visible contract carries no erased type: each registered operation is reached through its own typed projection (TryGetLocalPlayerState), and CanInvoke stays as the availability probe") matches the diff exactly |
| `member\|Stable\|…IStartingSupplyBehaviour.Create(string itemId)\|method\|-> object?` — removed, tombstoned | The `object?` is an erased handle on the mod-visible surface, and the engine-typed spelling the user's ruling allows is unavailable to an assembly that may not reference the game — so leaving the surface is the fix, not a re-spelling. The tombstone's "moved to `CasualtiesUnknownOnline.Runtime.Session.World`" is accurate: I read the type at the new path and its three members are the same three |
| `…IStartingSupplyBehaviour.LocalBody\|property\|-> object?` and `…TryPlace(object body, object item, int slot)\|method\|-> bool` — removed, tombstoned | Same; both reasons name the new home and the fact that no mod path reaches it |
| `type\|Stable\|…IStartingSupplyBehaviour\|interface\|-` — removed, tombstoned | The reason ("the mod-visible surface promises a shape to mod authors and may not carry a type no mod can reach") is the visibility rule, and it matches what was done |
| The three surviving `IModNativeApi` lines and the two `IModNativeApiProvider` lines (`Runtime`, outside this baseline) | `CanAccess` is a `bool`; `CanInvoke(string)` takes a name, not a payload, and `string` is not an erased type; `TryGetLocalPlayerState(out IModNativeLocalPlayerState state)` is a CUO-owned typed definition. None fails a question |

The one shape question the change leaves open rather than answers is the effect surface, which decision 250
explicitly hands the handle rule to — recorded as a downstream dependency, not as debt here.

## 6. Findings

### M1 (major) — The value surface this change deleted is still documented as live, in both blocks, on a security page

`docs/en/internals/permissions-and-security.md`, section **The native API surface is bounded** (heading at
line 74, paragraph at line 76):

> "`AccessNativeApi` does not hand over the game. It reaches a curated registry whose value surface is
> deliberately narrow: operation ids are capped at 128 characters, calls at 16 arguments, strings at 4096
> characters, values of the framework's own data model at 64 KiB encoded and primitive arrays at 1024
> elements. Unity and game-assembly objects and arbitrary object graphs are rejected on both sides of the
> adapter seam, so a native operation cannot smuggle a live game object out to a mod."

and its Chinese pair, `docs/zh/internals/permissions-and-security.md`, section **原生接口面是有边界的**
(heading at line 45, paragraph at line 47):

> "`AccessNativeApi` 并不是把游戏交出去。它通向一张经过筛选的登记表，值面刻意很窄：操作 id 最多 128 字符、
> 一次调用最多 16 个参数、字符串最多 4096 字符、框架数据模型的值编码后最多 64 KiB、基元数组最多 1024 个元素。
> Unity 与游戏程序集的对象、以及任意对象图，在适配器接缝的两侧都会被拒，所以一次原生操作没法把活着的游戏对象
> 偷渡给模组。"

Every clause after the first is now false: the four caps (`MaxArguments`, `MaxStringLength`, `MaxValueBytes`,
`MaxArrayLength`) and the whole `IsValidArguments` / `IsSafeResult` pair were deleted by this change, no
argument can cross at all, and nothing is "rejected on both sides of the adapter seam" any more — the type
of the projection is what prevents an unusable value, which is a stronger fact and a different sentence.

How this was verified: at HEAD `ModNativeApiPolicy.cs` is 133 lines and declares exactly those four caps
(plus `MaxOperationLength`); at the frozen tree it is 39 lines and declares only the operation-id rail; a
search for the four constant names and the two method names over `src/`, `tests/` and `tools/` finds
nothing; and neither page is in the diff, with its alignment row still recording the pre-change hashes
(which reproduce, because the files did not move). The pages were accurate when written — this change made
them wrong.

Why it matters beyond tidiness: this is the one page a reader consults to learn what `AccessNativeApi` can
reach, it is the page the ticket's own "nothing of value was lost" argument is about, and the claim under
review — "Every document the change touches agrees with the tree" — is satisfied while the documentation set
as a whole is not. The delivery checklist's whole-family-audit box cites "the contract-wide `object` scan,
the zero-reference check for `TryInvoke`, the moved seam's three referencing sites" but not the pages that
describe the deleted surface, so the audit did not reach them.

What would prove it addressed: the paragraph rewritten on both sides to the new fact ("a native operation's
result is declared by the projection that returns it; a value outside that type cannot be produced"), the
alignment pair re-recorded afterwards (it is hash-checked, so an edit obliges it), and the checklist's audit
line naming the pair.

### M2 (major) — The "debt is paid" fact was written into one of the four places that state it, including the binding rule file

The change amended the English policy page to:

> "The three `Critical` tickets this rule produced (`review/mod-content-typed-registration.md`,
> `review/mod-api-no-opaque-envelopes.md`, `review/mod-api-typed-seams.md`) have all landed, so the contract
> carries none of the debt it was written for; what keeps it that way is this review and the baseline gate,
> not a scan."

Three siblings of that same sentence still say the opposite, and none of them changed:

- `AGENTS.md`, rule 15, its closing clause: "and the debt the current contract carries is the sweep's three
  tickets in `docs/backlog/README.md`."
- `docs/evidence/normative-gates.md`, row `#15`: "…the debt is the three `Critical` sweep tickets in
  [`backlog/README.md`](../backlog/README.md)."
- `docs/zh/reference/modification-policy.md`: "`docs/backlog/README.md` 里那三张 `Critical` 票，就是这条规则
  定下之前这份契约欠下的账，各修各的那一份。" — the Chinese pair of the sentence that WAS amended, so the two
  blocks of one page now state contradictory facts, which `docs/AGENTS.md` §3 ("same facts") and §6 ("editing
  one side obliges the other in the same change") both forbid.

How this was verified: all three site texts were read from the frozen tree; all three sweep tickets are in
`docs/backlog/README.md`'s `### Review` section with Status "Review — implementation landed" (lines 96-98),
so the present-tense debt claim is false at every one of the three sites; and no gate covers these sentences
(the alignment gate is hash-based, so it cannot see a one-sided edit's content drift, and the rule map is
prose).

Why it matters: one of the three is the rule file every agent loads, and the sentence is the rule's own
statement of its remaining exposure — the file now points a reader at paid debt. This is the same
single-source-of-truth drift the repository's own rule ("同一条规则只留一处") exists to avoid, created inside
one change.

What would prove it addressed: the same amended sentence (or its removal) applied to `AGENTS.md` rule 15,
to the `#15` row of `docs/evidence/normative-gates.md` and to the Chinese policy page, with the pair's hash
re-recorded.

### m1 (minor) — The recorded focused-run command selects zero tests and exits 0

The ticket's *What landed*/*Limits* area and the self-check §3 both record:

> "- Focused run (`FullyQualifiedName~ModNativeApi|~StartingSupply|~GameAdapterNativeApi`) — **64/64**."

Run verbatim, vstest matches nothing: the output is the "1 test file matches" line and nothing else, with
exit code **0** — no "0 tests" warning a reader would notice, just the absence of a result line. With the
property name in every clause the figure is exact: 64 passed, decomposed 53 `Session` + 7 `Mods` +
3 `Patching` + 1 `Persistence` = 64.

Why it matters: `AGENTS.local.md` requires a declared number to be reproducible from the state the sentence
names, and this one is not reproducible as written — worse, its failure mode is a silent green, so a
successor re-running it would believe the focused run passed. What would prove it addressed: the filter
recorded in full form (as in §2) in both the ticket and the self-check.

### m2 (minor) — A dangling `todo/` path to the moved ticket survives in the evidence set

`docs/evidence/selfchecks/mod-api/mod-value-model-stage-b-review.md`:

> "(`docs/backlog/todo/mod-api-typed-seams.md`, whose own table names that exact line)"

The file it points at was deleted by this change (the ticket is now `docs/backlog/review/mod-api-typed-seams.md`),
so the path no longer resolves. This is the same class of check the immediately preceding cycle ran on its own
move — that cycle's report records "no `todo/mod-api-no-opaque-envelopes` reference survives under `docs/`"
as one of its tier-1 grep checks — so the sweep this change performs was incomplete, and claim 7's "the moved
ticket's path everywhere it is referenced" is falsified by this site. (The sentence's *substance* is a
point-in-time claim about the pre-change contract and, by the repository's own convention for dated records,
does not need rewriting; a path that no longer resolves is a link, not history.) What would prove it
addressed: the path re-pointed to `review/`.

### m3 (minor) — A mod-visible XML summary still promises invocation "by the same string"

`src/CasualtiesUnknownOnline.Abstractions/ModNativeApiOperations.cs`:

> "The ids are part of the public Mod API contract: the Game Adapter registers them and a mod invokes them by
> the same string."

A mod can no longer invoke anything by string — the id is what it hands to `CanInvoke` — and the invoke path
is the typed projection. This is the type a mod uses to name the operation, so the summary sits directly in
IntelliSense, which is the same class of finding the stage-B review raised against `IModContext.State`'s
summary. Related, one step weaker: `IModNativeApi.CanAccess`'s summary still says "Every invoke method also
checks and logs this before acting", and there is exactly one projection now. What would prove it addressed:
both summaries reworded to the probe/projection split the interface's own summary and the `mod-api.md`
blocks already state.

### N1 (nit) — `MalformedOperationId_IsRefusedBeforeTheProviderSeesIt` cannot pin "before the provider sees it"

The case ends with `Assert.Equal(0, fake.LocalPlayerStateCalls);`, but `FakeModNativeApiProvider` counts only
`TryGetLocalPlayerState`; `IsRegistered` is not counted, and a malformed id would fail
`RegisteredOperations.Contains` anyway. So the assertion is satisfied equally by "the rail refused it" and by
"the provider was consulted and answered no" — the name promises a distinction the fixture does not record.
(The old case had the same shape, so no coverage was lost; the fix is one counter in the fake.)

### N2 (nit) — The self-check's scope sentence about `TryInvoke` is wider than the tree

Self-check §2: "A repository-wide search for `TryInvoke` returns nothing: the dynamic path is gone from
`src/` and `tests/` alike". The clause after the colon is true and is the substance; the clause before it is
false — a repository-wide search necessarily returns the five tombstones, decision 250, the ticket, the
self-check and the MANIFEST row, all of which have to name what was deleted.

### N3 (nit) — The new "adding an operation" instruction omits two of the four edit sites

`docs/en/reference/mod-api.md` and its Chinese pair say an operation "costs one typed method on
`IModNativeApi` and its counterpart on the Runtime → Game Adapter seam". It also costs a constant in
`ModNativeApiOperations` and — for `CanInvoke` to tell the truth — the id recognised in
`GameAdapter`'s `IsRegistered`, which is exactly the string-keyed half that stayed behind. A maintainer who
adds the two methods and forgets `IsRegistered` gets a working projection whose own availability probe
answers `false`, and no gate covers `Runtime`/`GameAdapter` to catch it. One clause in each block would close
it.

### N4 (nit) — Two imprecise phrases in the moved interface's own doc

`src/CasualtiesUnknownOnline.Runtime/Session/World/IStartingSupplyBehaviour.cs` says it lives "beside the
other seams the adapter implements and a test host substitutes (`INativeWorldFacts`, `IModItemSpawner`)":
`INativeWorldFacts` is indeed in this namespace, but `IModItemSpawner` lives in `Runtime.Session.Mods`. The
same doc says "its only implementation is the Game Adapter's own" and, two lines later, that "a composition
with no engine at all supplies its own implementation for tests or a dedicated server" — the test fake is a
second implementation today, and a dedicated server is not a consumer anywhere in the tree (the Runtime-only
composition never constructs the coordinator, and the MVP excludes dedicated servers).

### N5 (nit) — `CanInvoke` and the projection are now two independent provider answers

`ModNativeApiAdapter.TryGetLocalPlayerState` does not consult `IsRegistered`; the probe does not consult the
projection. The two production providers agree (`DisabledModNativeApiProvider` refuses both, `GameAdapter`
registers the id and answers the projection's own body question), and the "registered but no body" split is
deliberate and tested. But the contract now permits a provider that says `IsRegistered == true` and
`TryGetLocalPlayerState == false` (or the reverse) with no statement anywhere that the two must agree —
which is how a future provider can make `CanInvoke` lie. Worth one sentence on the seam's doc rather than a
mechanism.

### N6 (nit) — One tombstone's reason is looser than the other two, and than the self-check

The `Create` tombstone says "a framework seam, not a mod contract: its only implementation and its only
caller live in the Game Adapter", while the `LocalBody` and `TryPlace` tombstones say "a framework seam the
Game Adapter implements and a test host substitutes". The self-check counts three referencing sites including
the test fake, so the first wording drops the substitution that is half the seam's justification.

### N7 (nit) — The ticket's status line and its test list are ahead of the tree

The ticket says "an independent adversarial review ran in the same cycle" while the self-check §5 says the
review is "Pending at the time of writing" — one of the two is wrong at the frozen commit. And the *What
landed* test list names "the starting-supply suite's fake implementation" as part of the change, but
`tests/CasualtiesUnknownOnline.Tests/Session/StartingSupplyCoordinatorTests.cs` carries no diff: it already
had `using CasualtiesUnknownOnline.Runtime.Session.World;`, so its fake follows the move without an edit —
worth saying, because a reader of the list would look for a change that is not there.

## 7. What could NOT be falsified (checked, no gap)

- **A0, the premise, checked against the diff before anything else.** The change touches no gesture, no patch,
  no protocol message, no UI, no input and no save path; the only behavioural delta is on the mod-facing
  contract, where an entry that could only ever reach `local.player.state` with no arguments is replaced by
  the typed projection of the same operation. No mod in the tree calls the removed entry (`ModExample` has no
  `NativeApi` use), and no player-visible surface reads any of it. So no entry mapping and no native call
  site is owed, and the checklist's entry-mapping box states exactly that. The one honest consequence to name
  is mod-facing and breaking for a third-party mod compiled against `TryInvoke` — which the user's own
  2026-10-08 ruling ordered, which decision 250 records, and which the repository's pre-release stance
  ("compatibility is never a design input") permits; the ticket's non-goal "No compatibility shim" says so.
- **Claim 2, the delete-versus-retype argument** (§4.3): I could not construct a capability a mod had before
  and lost, and I could not break the "a `ModValue`-shaped generic entry would widen the contract without a
  baseline line" argument.
- **The "nothing reachable depended on the scan" half of claim 3** (§4.1, §2): the deleted policy had exactly
  one production caller, and the only value shapes that ever crossed were the DTO and `null`. Only its
  documentation survived as a live claim — that is M1.
- **Claim 4's layering** (§4.4), including the alternative home the ticket's own Related line names.
- **Claim 5's scan** (§2), reproduced with my own scan, including rule 15's other spellings.
- **The five tombstones** (§5): each removed key is genuinely absent, each reason matches what was done, and
  the "moved" ones really are moved rather than euphemisms — I read the type at the new path.
- **The rule-15 enforcement story**: no gate claims to scan for erased types, so the change's "this review and
  the baseline gate, not a scan" is consistent with `docs/evidence/normative-gates.md` and with the gate
  suite, and there is no scan claim to falsify.
- **Reference integrity, both directions**: the moved ticket's index row, its five other prose references, its
  Status versus its container, the two other sweep tickets' `review/` links, the MANIFEST row for the
  self-check, the re-recorded alignment hashes (39/39 reproduce), and the 160-character index budget — all
  hold, apart from m2's one stale pointer.

## 8. What could not be checked at all

- **No game process and no two-client session** were available, so a live mod reaching the projection against
  the real adapter and the mod-API acceptance rows against deployed DLLs remain unverified here — the same
  limit the ticket and the self-check record, and the acceptance batch is where those rows close.
- **`dotnet format` was not run** (the review instruction forbids it, because it rewrites the frozen tree),
  so the "exit 0 with no file it wanted to change" claim stands unverified. The build's
  `EnforceCodeStyleInBuild` with warnings-as-errors passing gives the analyzer half; the whitespace half is
  the part this review cannot see.
- **The author's intermediate states are not visible**: only the frozen end state exists, so the claim that
  the format run left no file changed, and any earlier measured state, can only be judged at the end.
- **A third-party mod's build** is out of reach, so whether a real mod is broken by the removal is judged
  from the source shape alone (nothing in this tree uses it).
- **The uncommitted cycle's own checklist boxes** (self-check table, build/format/gates, structure review)
  are open by design at review time; I verified the gate reports exactly that red and nothing else.

## 9. Measured state, and this file's own row

Frozen-tree state as measured: build clean (0 warnings, 0 errors); behaviour suite **4850/4850** (net48);
`ModNativeApiTests` **7/7**; focused run **64/64** (53 + 7 + 3 + 1) with the full filter syntax; normative
gates **573 total, 572 passed, 1 failed** — the single red being
`RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` with 3 required boxes open against this
cycle's own reset, which is the cycle's declared position. Baseline **860 entries** (757 `member|` +
103 `type|`) / **67 tombstones**, exactly 865 − 5 and 62 + 5. `ModNativeApiPolicy` 133 → 39 lines;
`GameAdapter.cs` 593 → 588. 39 alignment pairs reproduce, 0 mismatches. The moved ticket's index row is 142
of 160 characters.

This report's own row in `docs/evidence/selfchecks/MANIFEST.md` is added by the author after this review
returns, so `SelfcheckManifestGateTests` goes red the moment this file exists; measured after it was written,
the gate suite is **573 total, 571 passed, 2 failed** — `EverySelfcheckFile_HasExactlyOneManifestRow` naming
this report, plus the checklist red above. That is expected, and it is the only thing about the gate state
that this report changes. Nothing under the work tree was created, modified or deleted by this review except
this file; the scratch directory this review wrote outside the repository (a copy of the deleted ticket and
four `git show` blobs, read with byte-accurate APIs after the shell's text pipeline proved unreliable for
non-ASCII content) was deleted before this report was written.
