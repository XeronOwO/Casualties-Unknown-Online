# The gates and the rules a change must satisfy

[Documentation](../../README.md) > [Contributing](README.md) > The gates and the rules a change must satisfy

**After this page** you can say which rule a change has to satisfy, which gate refuses it when it does
not, and what a new gate has to look like to be accepted. Nothing has to be read first; the commands
themselves are in [Build, test and deploy](build-and-test.md).

## How a rule gets enforced

Every binding rule falls into one of three mechanisms, and the
[rule-to-gate map](../../evidence/normative-gates.md) records which one applies to which rule:

1. **Build and format** — `.editorconfig` raises the style rules to error severity and
   `EnforceCodeStyleInBuild` in each project file makes the compiler enforce them, on top of
   warnings-as-errors.
2. **The gate project** — `tests/CasualtiesUnknownOnline.NormativeGates.Tests`, running as part of
   `dotnet test`. These are C# xUnit tests; the former `tools/check-*.ps1` scripts were ported into
   them and removed.
3. **Review** — rules a machine cannot judge without false positives: whether a page teaches,
   whether an argument rests on evidence, whether a self-check was independent.

A gate proves presence and shape. It cannot prove quality, and a green run is not a review.

## The gate inventory

| Refuses | Gate |
|---|---|
| A top-level type over 600 aggregate lines, more than five boolean state fields in one type, or two top-level types in one file | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` |
| A kernel that reaches runtime, game or network types; a projection table mutated outside its owner; removed dual-architecture markers coming back; a `GameCommand` without an authority policy; string-keyed kernel state | `SourceShapeGateTests.GameStateIsolation_…`, `…ItemAuthority_NoDirectProjectionMutation`, `…NoLegacy_…`, `…CommandAuthority_…`, `…KernelShape_…` |
| An upward project reference, a Runtime that skips the Application layer, an undeclared project, a consumer reaching down | `ProjectDirectionGateTests.Tree_FollowsTheDeclaredProjectDirection` (table: `ProjectDirectionPolicy.AllowedReferences`) |
| A fully qualified type name where a `using` or alias is the cleaner form | `FullyQualifiedNameGateTests` (Roslyn) |
| An added, changed or quietly removed `Abstractions` public member while the reviewed baseline still says otherwise | `ApiSurfaceGateTests.AbstractionsPublicSurface_MatchesTheReviewedBaseline` (+ census floor and matcher contracts) |
| An absolute machine path in any file git would carry | `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` |
| An unchecked required box, or a checked FORBIDDEN box, in the delivery checklist | `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` |
| A wire discriminator missing from the sync coverage matrix, a row without a verdict or evidence, an evidence quote that no longer matches its source | `SyncCoverageGateTests.SyncCoverageMatrix_…`, `…SyncCoverageEvidence_EveryQuoteMatchesItsSourceLine` |
| A test class that mutates a process-global static outside the `GameAssembly` collection | `TestIsolationGateTests.StaticGameStateMutations_JoinTheGameAssemblyCollection` |
| A test class over 40 real cases or data rows | `TestClassSizeGateTests.NoTestClass_ExceedsTheCaseLimit` |
| A live governance document that restates the protocol version instead of pointing at `ProtocolVersion.Current` | `ProtocolNumberGateTests.LiveGovernanceDocuments_DoNotRestateTheProtocolVersionNumber` |
| A patch seam whose census, composition or port reachability drifted from the frozen declaration | `PatchBridgePortShapeGateTests.…` |
| A Runtime session service that does not react to session end through `ISessionReset`, or a lifecycle subscription without its unbind half | `SessionLifecycleGateTests.…` |
| A backlog index row that is not a pointer, a ticket whose status disagrees with its folder, a document citing `AGENTS.md` by line number | `BacklogIntegrityGateTests.…` |
| A contract tool row that no longer equals the adapter's own contract rows | `PatchContractRowParityTests.ToolRows_EqualTheAdaptersOwnContractRows` |

Two rows of that table — `TestClassSizeGateTests` and `PatchContractRowParityTests` — are declared in
the behavioural suite (`tests/CasualtiesUnknownOnline.Tests`) rather than in the gate project. `dotnet
test` runs both projects, so the split changes nothing about how you run them; it matters only when you
go looking for the declaration.

## Engineering discipline

Standards here are "done well, not just done". Delivering means passing three reviews before
hand-off: **architecture** (single responsibility, clean dependencies), **tests** (runtime
verification of behaviour) and **maintainability** (readable and changeable by the next person).
"It runs" is the floor, not the goal.

- No shortcuts: take the proper path when one exists. Manual workarounds, hard coding, copy-paste and
  skipped tests borrow against the future.
- Pragmatism is not a shield: a suboptimal choice needs an architectural reason, not "not enough
  time". Do the right thing once.
- Root cause over patch stacking: first ask whether an architecture change can eliminate the cause;
  do not pile on features or patches, and do not avoid a needed refactor out of fear of churn — the
  cost moves, it does not disappear.
- Pragmatic future-proofing: leave room for foreseeable evolution, do not pre-build for imagined
  futures. "We'll deal with it later" is not a default excuse.
- Tests cover the core scenarios plus edge, exception and failure paths. Happy-path-only coverage
  never proves "done well".
- Every key path and logical branch is observable. Log level follows trigger frequency
  (high-frequency → Verbose/Debug, low-frequency or exceptional → Warn/Error), and the log carries
  what locating the failure needs: branch, state, ids, input, result. An unobservable key path is
  unfinished; the standard is "one pass of logging is enough", not "change code → deploy → reproduce
  → add logs".

## The fourteen binding conventions

1. **English by default** in all code, comments and committed docs. The paired Chinese pages are the
   deliberate exception; see [Writing documentation](documentation-standard.md).
2. **Modern idiomatic C#**: `var`, nullable, `is null` / `is not null`, collection expressions, and
   using aliases for name collisions. **Unity objects are the exception**: use `== null` / `!= null`,
   because the overload detects objects destroyed by a scene reload. Prefer switch expressions over
   switch statements and long `if`/`else` chains.
3. **Evidence-based changes**: cite the decompiled sources (`reversing/`, file plus line — that tree
   is never edited, so its line numbers stay stable) before touching code. For our own `src/` and
   `tests/`, cite the path plus the quoted text and never a line number: a line number drifts with
   every edit above it, while the quoted text is what the claim rests on. Fix root causes, not
   symptoms.
4. **Absolute-machine-path red line**: no absolute machine path may ever enter git. No drive-letter
   path, UNC path or Unix-style path rooted at home, user, temp, var or opt belongs in a tracked
   file; local paths live in the gitignored `AGENTS.local.md` or in placeholders such as
   `<game-dir>`. Existing tracked absolute paths are removed, not left as historical debt.
5. **Self-learning**: record reusable, generalizable knowledge in `AGENTS.md`, under `docs/`, or in
   memory; be selective.
6. **Patch hooks report only verified writes**: a Prefix that swallows a write must not let the same
   Postfix report it. Re-read the written state or pass the verdict explicitly.
7. **Prefer `using` directives and aliases** over fully qualified type names; use a fully qualified
   name only when unavoidable (a HotRepl eval has no `using`).
8. **Reuse the game's existing native UI** whenever a player-facing feature already has one (backpack,
   medical panel). Do not build a parallel CUO UI to replace it; prefer a thin adapter plus patches.
   If a native UI cannot be reused, record the concrete blocker with evidence and get user direction
   before adding custom UI.
9. **Ask on functional design, execute on implementation.** When the functional design is ambiguous,
   has several reasonable options or has user-visible trade-offs, research reference implementations
   first and still confirm the direction with the user before coding. Once design and requirements are
   clear, follow the specifications without asking about routine implementation detail. Which work
   item to pick next is not such a decision — take it from the ticket's priority, its dependencies and
   the handoff's suggested order.
10. **Future backlog items are not work items.** `future/` means "deferred by decision", not "pending
    implementation": those items are not proactively implemented unless the user promotes them.
11. **Large or multi-stage work is split into stages**, each producing a verifiable result. Do not
    hide multi-stage work behind one vague ticket; once split, the umbrella ticket may be dropped.
12. **Empty directories** that must stay in the repository carry a 0-byte `.gitkeep`; do not
    substitute a placeholder document and do not leave an untracked empty directory.
13. **Extension methods use the C# 14 `extension` syntax.** It is a strict superset of the classic
    `this X` form (methods plus properties, static members and operators, receiver named once) and
    compiles to the same call sites, so there is no case for the classic form. Migrate one on sight:
    `SourceShapeGateTests.ExtensionMethods_UseTheCsharp14ExtensionSyntax` fails on a new classic
    declaration.
14. **Minimum visibility, declared stability.** A type or member defaults to the narrowest visibility
    its implementation needs; only a capability that is designed, documented and reviewed becomes a
    public third-party contract. `Runtime` and `GameAdapter` are implementations a mod may patch but
    is never promised. The `Abstractions` public surface is a recorded, gate-enforced baseline
    ([abstractions-api-baseline.txt](../../api/abstractions-api-baseline.txt)): an addition or a
    removal fails until the baseline is reviewed and updated, a removal names its reason, and a
    surface that is not `Stable` declares its level with `[ApiStability]`
    ([advanced-modification-policy.md](../../api/advanced-modification-policy.md)).

## The compatibility boundary

Compatibility is never a design input. The boundary is the protocol-version check at handshake: the
host refuses a peer whose `HandshakeMsg.Protocol` differs and the guest ends the session on a
mismatched `HandshakeAckMsg.Protocol`. A change therefore never has to keep an old wire or save shape
alive, retain a legacy field, or pick a weaker mechanism to avoid a version bump: change the wire the
mechanism needs and bump `ProtocolVersion.Current` in the same change (the numbering policy is in
[active.md](../../decisions/active.md)). "No wire change" and "host untouched" are facts worth
recording, never merits or constraints in a design argument — before and after release alike.

The version number lives in exactly one place, `ProtocolVersion.Current`, and its doc comment is the
wire-change log; live governance documents point at the constant instead of restating it, which is
what `ProtocolNumberGateTests` checks.

## Writing a new gate

A gate's declaration must equal what it can reach:

- Derive the scan surface from the existing source of truth (the solution file, the attribute, the
  registry) instead of a hand-kept list that will drift.
- Keep a census floor, so a scan that silently stops seeing the tree fails instead of passing.
- Pin the matcher with positive and negative samples: a synthetic case that must be flagged and one
  that must not.
- Do not scan the gate's own test data, and prefer a regex that matches the fact rather than the
  shape of the text around it.
- After changing a matcher or a scan surface, re-run the negative sample, then check the three states:
  the previous commit should fail it, the fixed tree should pass, and the whole repository should
  report no false positive.

## How you know it worked

- `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` exits `0`.
- A rule with a gate has a row in the [rule-to-gate map](../../evidence/normative-gates.md); a rule
  without one is listed there as review/process, which is a statement about the rule, not a gap.
- The change passes the [delivery checklist](../../evidence/delivery-checklist.md) before its last
  commit.

## Related reading

- [Build, test and deploy](build-and-test.md) — the commands and the suite's own contract
- [Repository map and pitfalls](repository-map-and-pitfalls.md) — the dependency direction these gates enforce
- [Review and delivery](review-and-delivery.md) — the process that catches what no gate can
- [Rule-to-gate map](../../evidence/normative-gates.md) — every rule with the gate or process that enforces it
- [Active decisions](../../decisions/active.md) — the recorded decisions the rules rest on

[Documentation](../../README.md) > [Contributing](README.md) > The gates and the rules a change must satisfy
