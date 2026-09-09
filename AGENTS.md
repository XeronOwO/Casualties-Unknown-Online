# AGENTS.md

Instructions for AI coding agents and contributors working in this repository.

## Document Scope & Classification

- `AGENTS.md` is the shared, portable rule set for every contributor.
- `AGENTS.local.md` is the machine/person-specific companion: local paths, personal
  preferences, execution detail, and past mistakes. It is gitignored and never committed.
- The same rule may exist in both files. `AGENTS.md` states the objective/general principle;
  `AGENTS.local.md` carries the local detail, examples, and rationale. Where both state the
  same rule, this file is authoritative for the rule and the local file for execution.
- Language: this repository is global — contributors and readers may have any native
  language — so committed content is written in English, the shared working language. Another
  language is appropriate only where the deliverable explicitly targets that language's
  audience (localized resources, a community-specific project). Personal, uncommitted
  communication follows the owner's own preference, recorded in `AGENTS.local.md`.
- Requirement triage: personal/specific → `AGENTS.local.md`; shared/beneficial → this file or
  `docs/`; ambiguous → ask the user.
- This repository is the long-term reference implementation for these standards. New projects
  should adopt rules and gates from here rather than duplicating them in a global agent file.

## Project Overview

**Casualties Unknown: Online (CUO)** is a BepInEx multiplayer mod framework for
*Casualties Unknown* (Demo). The game has no multiplayer; CUO adds Steam-based
Host + Guests co-op by reorganizing local-only game state into host-authoritative
simulation with guest input/state sync.

- Stable **CUO Runtime**: protocol, host/guest state machine, mod loading, serialization,
  tick/snapshot, logging, version negotiation.
- Replaceable **Game Adapter**: the only layer that knows the game's private types and
  absorbs game-update churn.

[REF] Active architecture: `docs/architecture/README.md` ·
Current design: `docs/architecture/current.md` ·
Decisions: `docs/decisions/active.md` · Evidence: `docs/evidence/verification.md`.

## Repository Layout

```text
src/CasualtiesUnknownOnline.Abstractions/  # public API; the ONLY package mods may reference
src/CasualtiesUnknownOnline.Runtime/       # DI/Logging/BepInEx/Steam/session; never game assemblies
src/CasualtiesUnknownOnline.GameAdapter/   # the ONLY project referencing game assemblies; HarmonyX
src/CasualtiesUnknownOnline.Plugin/        # BepInEx 5 entry; thin lifecycle driver
CasualtiesUnknownOnline.slnx               # solution
references/                                # game assemblies, gitignored, copied on demand
reversing/                                 # reverse-engineering workspace, gitignored
docs/                                      # architecture, decisions, backlog, feature matrices, selfchecks
AGENTS.local.md                            # gitignored local notes; never commit
```

See `docs/README.md` for the documentation index.

## Build & Commit Gates

```bash
dotnet build CasualtiesUnknownOnline.slnx
dotnet test CasualtiesUnknownOnline.slnx          # must pass before every commit
dotnet format CasualtiesUnknownOnline.slnx        # mandatory before every commit
```

- `[GATE]` All of the above must pass before commit; `dotnet format` is build-enforced.
- `[RULE]` `dotnet test` also runs `tests/CasualtiesUnknownOnline.NormativeGates.Tests`: the
  Roslyn fully-qualified-name gate plus C# unit-test ports of the former `tools/check-*.ps1`
  source/repo/process gates. The rule-to-gate map is `docs/evidence/normative-gates.md`.
- `[RULE]` Pure documentation-only changes (no `src/`, `tests/`, or `tools/` modifications)
  skip build/test/gates; review the diff and commit directly. If docs describe a code change,
  commit them with the code change and run the gates in that same commit.
- `[RULE]` Test parallelism is contract, not accident. The xUnit v2 runner configuration is
  `tests/CasualtiesUnknownOnline.Tests/xunit.runner.json`; a test class that writes a
  process-global static field must join the `GameAssembly` collection
  (`tests/CasualtiesUnknownOnline.Tests/Patching/GameAssemblyCollection.cs`, enforced by
  `TestIsolationGateTests`); the test composition must not write a per-node rolling log file
  (`tests/CasualtiesUnknownOnline.Tests/Fakes/TestLogging.cs`). Keep test classes small enough
  that no single class dominates the run, and record runtime changes with the three-run median
  method in `docs/evidence/test-parallelization.md`. Behavior-family splits share stateless
  helpers and must never duplicate `MemberData` rows (kind shards must stay a partition,
  asserted by a guard test); a class may share a read-only query facade as an `IClassFixture`
  (e.g. `DirectionProbe`, which exposes only pure queries and keeps its nodes private), but
  never a mutable session/world fixture.
- `[RULE]` Test feedback tiers: a class that constructs the production composition root, a full
  simulation world/replay harness, a shared full-stack fixture, the game-assembly reflection
  host (`GameAssemblyHost`), or real loopback sockets (`IpDirectTransport`) carries
  `[Trait("Category", "Integration")]`; untagged classes form the fast inner loop. Use
  `dotnet test ... --filter "Category!=Integration"` for that loop and
  `--filter "FullyQualifiedName~X"` to target one class or family. The classification and
  measured subset size are recorded in `docs/evidence/test-parallelization.md` §9.
- `[RULE]` Anti-rot: no test class may exceed 40 real xUnit cases/data rows (including
  `MemberData` expansion). `TestClassSizeGateTests.NoTestClass_ExceedsTheCaseLimit` enforces
  the cap at runtime and its counting/limit contract is asserted by
  `CaseCounting_SeesMemberDataRowsAndFlagsTheLimit`. Split an oversized class by behaviour
  family; raising the cap requires the same measured evidence as a stage change.
- `[RULE]` The runner thread cap stays `1x` (logical processor count). The `2x` and fixed
  `10`/`12`/`14` alternatives were measured; summed test time inflates with concurrency and
  must never be compared across thread settings. See
  `docs/evidence/test-parallelization.md` §7.5 and §9.
- Target: `net48`, `LangVersion = preview`, nullable enabled, warnings-as-errors.
- NuGet sources: nuget.org + nuget.bepinex.dev + nuget.samboy.dev.
- Game assemblies are copyrighted and only the Game Adapter may reference them.
- Packaged plugin deploys via `tools/deploy.ps1` (machine path in `AGENTS.local.md`).

## Engineering Discipline

Standards are "done well, not just done". Delivering means passing three reviews before
hand-off: **architecture** (single responsibility, clean dependencies), **tests** (runtime
verification of behavior), and **maintainability** (readable and changeable by the next
person). "It runs" is the floor, not the goal.

- `[CRITICAL]` No shortcuts: take the proper path when one exists. Manual workarounds, hard
  coding, copy-paste, and skipped tests borrow against the future.
- `[CRITICAL]` Pragmatism is not a shield: a suboptimal choice needs an architectural reason,
  not "not enough time". Do the right thing once.
- `[CRITICAL]` Root cause over patch stacking: first ask whether an architecture change can
  eliminate the cause; do not default to piling on features or patches. Do not avoid a needed
  refactor out of fear of churn — the cost moves, it does not disappear.
- `[RULE]` Pragmatic future-proofing: leave room for foreseeable evolution, do not pre-build
  for imagined futures. "We'll deal with it later" is not a default excuse.
- `[RULE]` Self-review happens before hand-off, not after review: structure the change the
  moment a feature or fix is finished.
- `[CRITICAL]` Tests must cover core scenarios plus edge, exception, and failure paths.
  Happy-path-only coverage never proves "done well".
- `[CRITICAL]` Every key path and logical branch must be observable; choose log level by
  trigger frequency (high-frequency → Verbose/Debug, low-frequency/exceptional → Warn/Error).
  Logs carry the context needed to locate a failure: branch, state, ids, input, result. An
  unobservable key path is unfinished; the standard is "one pass of logging is enough", not
  "change code → deploy → reproduce → add logs".

## Architecture & Sync Rules

**Sync model (non-negotiable):**

- `[CRITICAL]` **Local compute, remote verify/sync**: each player simulates its own actions
  with single-player feel; the host never simulates a guest's per-frame behavior. Host
  authority is limited to global world-state ownership (seed, saves, rulings).
- `[CRITICAL]` **Accept-first sync arbitration — only for state the host can represent**:
  adopt and relay a guest's report first; correct only on an obvious conflict; a correction
  never blocks the player. Strict validation/anti-cheat are low priority until the feature set
  is stable. **Precondition: the host can actually adopt the reported state.** The rule exists
  to stop hard validation and player-blocking corrections, never to accept state the host
  cannot own. A report the host cannot represent — its own content set lacks the
  prefab/template, the id cannot be mapped, the domain object cannot be owned — is REJECTED,
  not accepted: it is neither recorded nor relayed. An accepted-but-unowned record has no owner
  whose death can ever retract it, so it leaks into every later snapshot and resurrects state a
  peer has already destroyed. A rejection must be VISIBLE: answer the reporter so its re-report
  fallback stops, and log/surface the concrete mismatch. Silent drops and unowned accepts are
  both forbidden.
- `[CRITICAL]` **Sync semantics, not Transforms**: synchronize game-semantic state, never raw
  Transform/GameObject state. (Transform sync fails on physics, parenting, animation, nav,
  rigidbodies, and scene loads.)
- `[CRITICAL]` **Dedicated events, not snapshots**: discrete triggers travel as dedicated event
  messages; periodic streams are only fallback/replay.
- `[CRITICAL]` **Deep sync chains**: one operation = one owner; Harmony patches are thin
  adapters; no cross-call business state in patches; reports happen only after a verified
  commit; every operation is recoverable as a complete trace.
- `[CRITICAL]` **Injected state must be authority-safe**: mutable state belongs to its owner;
  DI services are behavior/mechanism, not global mutable state.
- `[CRITICAL]` **No host migration in MVP**: host exit → session ends → guests return to lobby.
- `[CRITICAL]` **Identity, not handles**: use `NetworkEntityId` (epoch + host allocation
  counter + generation), never Unity instance IDs.
- `[CRITICAL]` Network/Steam callbacks never touch Unity objects; main-thread marshaling is
  mandatory.
- `[RULE]` Steam Lobby is discovery/roster only; game data goes through `INetworkTransport` /
  `ISession` / `IPeer` / `INetworkChannel`. Never expose Steam APIs to mods.
- `[RULE]` Host is the only save authority; guests keep local settings only.

**Structure and boundaries:**

- `[RULE]` **Strict single responsibility**: separate control plane from data plane; a class
  that both holds state and does wire I/O is a smell; split before it grows.
- `[RULE]` **One top-level type per file**; file name matches type name. Nested helper types
  stay inside their container.
- `[RULE]` **Handler pattern**: `[Handler(Key)]` + generic base class + reflection registration
  into DI, with a read-only route table built at startup. No giant `switch`. For large families
  of similar registration code (commands, handlers, providers, packets), prefer discoverable
  attribute + reflection registration over hard-coded linear registration. Keep the explicit
  list when it is genuinely clearer/more auditable; if such refactor space is found during
  development, refactor it or add a backlog item in the same cycle.
- `[RULE]` **Break construction cycles by extracting abstractions**: shrink the dependency
  surface into a standalone object so the dependency graph has no cycle. `Lazy<T>` is a second
  choice; late wiring such as `AttachXxx(other)` is forbidden. Reason about resolution chains
  with "who constructs whom".
- `[RULE]` **State belongs to its owner**: mutable state is held inside the owning object and
  exposed through narrow interfaces; DI services are not a global mutable-state repository.
- `[RULE]` Prefer switch expressions (IDE0066) over switch statements and long if/else chains.
- `[RULE]` Prefer HarmonyX; Mono.Cecil only for assembly structure changes. Feature-scan game
  APIs instead of hardcoding offsets/private fields (hardcoded offsets/private fields break on
  every game update).
- `[RULE]` Safe degradation at startup: `Compatible` / `CompatibleWithWarnings` / `Unsupported`
  / `CriticalFailure`; never let a failed patch silently run.

**Architecture gates:**

- `[CRITICAL]` **Architecture over feature/patch stacking**: when a sound architecture change
  can fully satisfy the requirement and remove the root cause, use it. Do not satisfy a need by
  piling on feature switches, narrow patches, per-case workarounds, or local hacks. A patch is
  acceptable only when it is the clearer/less-costly choice and the architecture alternative is
  explicitly documented as not justified in the same cycle. When a better, cleaner architecture
  exists, proactively overhaul instead of continuing to patch an inferior structure — local
  patches on a fundamentally wrong design either fail to fix the problem or create new ones.
- `[CRITICAL]` **Gate escapes must be real responsibility splits**: never delete
  comments/blank lines, shrink formatting, or move code between files just to pass a
  line-count or architecture gate. Extract a single-responsibility type, preserve behavior, and
  keep tests/gates green.
- `[CRITICAL]` **Hard thresholds are enforced at build time**, not by self-discipline: a
  top-level type exceeding 600 aggregate lines, more than 5 boolean state fields in one type,
  or multiple top-level types in one file must be resolved before the change, not after.
  Enforced by `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits`
  (C# port of the former `tools/check-architecture.ps1`, which no longer exists); over-limit
  exceptions are recorded in `docs/architecture-debt.json`. Run the three structure questions
  before every change: which domain does this belong to? who owns the state? will the target
  type exceed a limit?
- `[RULE]` **Architecture-first, ask before risky work**: for risky or architecture-affecting
  changes, propose a complete plan (current responsibilities, target domain model, dependency
  direction) and get consent before acting; split by domain in one pass. Test-only hardening
  and behavior-preserving extraction may proceed without prior approval. **When the existing
  mechanism has a structural problem or a clearly better architecture direction appears, the
  current backlog ticket is not the task boundary**: stop the small patching, present the full
  architecture proposal with impact and trade-offs, get confirmation, then implement it in
  stages through the full workflow. "Fix this ticket first", "no time", and "too risky" are not
  reasons to keep patching.

## Engineering Conventions (binding)

1. `[RULE]` English by default in all code, comments, and committed docs (see
   *Document Scope & Classification* for the language boundary).
2. `[RULE]` Modern idiomatic C#: `var`, nullable, `is null`/`is not null`, using aliases for
   name collisions. **Unity objects are the exception**: use `== null` / `!= null` because the
   overload detects scene-reload-destroyed objects.
3. `[RULE]` Evidence-based changes: cite decompiled sources (`reversing/`, file:line) before
   touching code; fix root causes, not symptoms.
4. `[CRITICAL]` **Absolute-machine-path red line**: no absolute machine path may ever enter
   git. Existing tracked absolute paths must be removed, not merely left as historical debt.
   Local machine paths belong only in gitignored `AGENTS.local.md` or in placeholders such as
   `<game-dir>`, `<sandbox-root>`. No drive-letter path, UNC path, or any Unix-style absolute
   path rooted at home, user, temp, var, opt, etc may appear in tracked files. Enforced by
   `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths`.
5. `[RULE]` Self-learning: record reusable, generalizable knowledge in `AGENTS.md`, `docs/`,
   or memory; be selective.
6. `[RULE]` Patch hooks report only verified writes; a Prefix that swallows a write must not let
   the same Postfix report it. Re-read the written state or pass the verdict explicitly.
7. `[RULE]` Prefer `using` directives / `using` aliases over fully qualified type names; use
   fully qualified names only when unavoidable (e.g., HotRepl eval, where `using` is
   unavailable). Enforced by the Roslyn gate in `FullyQualifiedNameGateTests`.
8. `[RULE]` Reuse the game's existing native UI whenever a player-facing feature already has
   one (e.g., backpack, medical panel); do not build a parallel CUO UI to replace it. Prefer a
   thin adapter focus + patches. If a native UI cannot be reused, record the concrete blocker
   with evidence and get user direction before adding custom UI.
9. `[RULE]` **Ask on functional design; execute on implementation**: when the functional design
   is ambiguous, has multiple reasonable options, or has user-visible tradeoffs, research
   reference implementations first (e.g. KrokMP, native mechanisms, user-provided references)
   and still ask the user and confirm the direction before coding. Being allowed to work
   autonomously is not permission to decide unilaterally, and "build it first, discuss later"
   is not design confirmation. Once the design and requirements are clear, follow the project
   specifications and this file without asking about routine implementation details; ask only
   when a decision is architecture-affecting or not covered by the conventions.
10. `[RULE]` **Future backlog items are not work items.** `future/` means "deferred by
    decision", not "pending implementation". Future items are not included in handoff prompts
    and are not proactively implemented unless the user explicitly promotes them to `todo/`
    because a clear need has appeared.
11. `[RULE]` **Large or multi-stage work is split into stages, not one umbrella ticket.** A
    ticket that needs deep analysis must say so and be split into stages/steps that each
    produce verifiable results; do not hide multi-stage work behind one vague todo and do not
    discover the wrong direction after a big-bang change. Once split, the original umbrella
    ticket may be dropped — do not keep a parent ticket with no independent incremental value.
12. `[RULE]` **Empty directories** that must stay in the repository use a 0-byte `.gitkeep`
    file; do not substitute a placeholder document, and do not leave an untracked empty
    directory.
13. `[RULE]` **Extension methods**: stop and ask the user whether to use the C# 14 `extension`
    syntax; do not default to the classic `this X` form.

## Development Workflow (binding)

Applies to all normal work — features, bug fixes, user-facing changes, and internal
improvements. It is not reserved for previously rejected items or user-reported problems.

**Hard order:** understand → mechanism inventory → plan + self-check table → user approval
(large changes only) → red test (defects only) → implement → build/gates → deploy → runtime
verification → independent adversarial self-check → structure review → commit.

1. **Frame the task from the user's perspective.**
   - For user-reported issues: write the exact reproduction steps, expected behavior, and an
     acceptance matrix covering roles, directions, views, and related families.
   - For feature work: state the functional intent, user-visible behavior, and scope before
     coding.
2. **Resolve functional design before implementation.** If the functional design is ambiguous
   or has user-visible tradeoffs, research references and ask the user; confirm the direction
   before implementing. Once design and requirements are clear, proceed autonomously on
   implementation detail and do not over-ask.
3. **Check for reusable game UI/mechanisms first.** If the game already has a native surface
   for the feature, reuse it. If not, document the evidence and get user direction before
   building custom UI.
4. **Make the expected failure visible before fixing (defects only).** For user-reported issues
   and bug fixes, add a regression test or runtime probe that fails on current code, covering
   the reported scenario and adjacent scenarios, and record the red before implementing. New
   feature development writes behavior tests directly and does not need a pre-implementation
   red: with no pre-existing defect there is nothing meaningful to see fail. Red→green is a
   hard gate for defects: if implementation was made without first seeing the regression test
   fail, stop and go back to the pre-fix code to record the red before presenting. "The test
   passes now" is not a substitute for having observed it fail, and a compile error caused by a
   missing type is not a red. The red step only needs the focused failing test to run; the full
   suite runs after the fix is in place.
5. **Implement, then verify against the full matrix.** Build → deploy the latest artifacts →
   verify deployed artifact identity (hash/timestamp) → run runtime/log/dual-client checks
   where applicable → confirm every row of the acceptance matrix passes before moving to
   `review/`. A build that passes without the latest DLLs running is not completion.
6. **Run an independent adversarial self-check.** Use a fresh/independent context (an
   independent subagent, not the same reasoning path that produced the change). Cover reverse
   directions, third-party views, edge cases, and regressions of adjacent features.
7. **When a delivery is rejected or verification fails, run a root-cause loop.** Analyze "why
   was it missed", record the process lesson, and fix the leak before moving on. Moving the
   ticket back is not enough.
8. **Keep incomplete or unverified work open.** Do not claim completion, do not reclassify
   known gaps as future, and do not move to `review/` until the exact scenario and full
   acceptance matrix are verified.
9. **Budget the working context; hand off at phase boundaries.** Batching is for small,
   strongly related work only — never for several large tasks in one sitting. After each task,
   or each phase of a multi-stage task, evaluate how much context is already consumed (long
   documents read, large source files, independent review rounds, full-suite runs). When it is
   close to full, stop at the phase boundary: do not start the next large task, and instead emit
   a handoff prompt for a fresh session that states what landed, what is verified, and what
   comes next. An exhausted context produces shallow work, which is a quality failure, not a
   time problem.

### Quality & Delivery

- `[CRITICAL]` No self-assumption: every claim needs source evidence (`file:line`) or runtime
  evidence. The runtime is the judge, not the plan. A paper review is not a review: key paths
  need instrumentation or a runtime tool to prove them; ask "how does this chain get proven at
  runtime?" before hand-off.
- `[CRITICAL]` **Fix the family, not just the reported case**: when fixing a problem, do not stop
  at the single symptom or function. Actively inspect similar functions, sibling mechanisms,
  and other modules for the same defect pattern; align the whole family in the same cycle, or
  record a backlog item if the remaining cases are genuinely out of scope.
- `[CRITICAL]` **User-found issues are hard release blockers**: every issue reported by the user
  must be fixed until the exact reproduction is resolved and verified, not merely until
  tests/gates pass. Do not stop at "unit tests are green", do not leave a known failing
  scenario as future work, and do not move it to `review/` while any part of the user-reported
  behavior is still unverified. Only stop when the fix is demonstrably complete against the
  user's scenario and no known regression remains; if an external blocker prevents completion,
  state it explicitly and keep the issue open rather than declaring it done.
- `[CRITICAL]` **Deployment/artifact verification is part of completion**: after any runtime
  behavior change, build and deploy the latest artifacts, then verify the deployed artifact
  identity (hash/timestamp) before reporting the change as complete. A build passing without
  the latest DLLs running is not completion.
- `[CRITICAL]` **Development-period verification is simulation/static-evidence based**: no
  manual dual-client acceptance during feature development. Deployment to the physical machine
  and dual-client acceptance are the user's release-cycle actions; the user performs final
  acceptance later.
- `[CRITICAL]` **Acceptance-readiness audit before review**: for every user-facing feature,
  before moving it to `review/`, answer explicitly with evidence:
  1. Does the game already have a native UI for this surface? If yes, reuse it; no parallel
     custom UI without a documented blocker and user direction.
  2. Was the whole family audited across all roles, directions, participants, and third-party
     views, not only the reported side?
  3. Was the exact user reproduction covered by a test or runtime trace against the latest
     deployed DLLs, not by generic unit coverage or static reasoning?
  4. Were unsupported features/operations left as future only with explicit user acceptance? A
     self-imposed "future" is not a completed parity claim.
  Passing tests and repo gates is necessary, not sufficient.
- `[CRITICAL]` **Adversarial self-check must be independent**: after a fix, run an adversarial
  review in a fresh/independent context (an independent subagent), never from the same reasoning
  path that produced the fix. Cover reverse directions, third-party views, edge cases, and
  regressions of adjacent features.
- `[CRITICAL]` **Rejection root-cause loop**: when the user rejects a delivered item, perform a
  "why was it missed" analysis and record the process lesson. Moving the ticket back is not
  enough; the leak must be understood.
- `[GATE]` Follow `docs/evidence/delivery-checklist.md`; the checklist integrity is verified by
  `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`. Check off its boxes one
  line at a time with the Edit tool; bulk checking is forbidden.

### Definition of Done for user-facing changes

A user-facing change is not complete just because tests and gates pass. Before moving to
`review/` or reporting completion:

- the exact user reproduction no longer reproduces;
- all roles, directions, participant views, and third-party views are verified;
- the game's existing native UI is reused where one exists;
- the latest build is deployed and artifact identity is verified;
- an independent adversarial self-check has passed;
- no known failing scenario is left as "future" without explicit user acceptance;
- the root cause is addressed, not patch-stacked.

See `docs/evidence/delivery-checklist.md` for the executable gate.

## Commit Message Convention

- `[RULE]` Use Conventional Commits for every commit: `type(scope): summary`.
- Allowed types: `feat`, `fix`, `docs`, `test`, `chore`, `refactor`, `perf`, `revert`, `build`,
  `ci`, `style`.
- Scope is a short lowercase domain/path name (for example `protocol`, `carry`, `backlog`,
  `projection`, `character-sound`, `adaptive-sync`); omit the scope when there is no clear
  domain.
- Summary starts with an imperative verb, uses lowercase after the type/scope prefix (proper
  nouns and acronyms may keep their case), and has no trailing period.
- Use one line for the summary; optional body text starts after a blank line and is wrapped for
  readability.
- Pure documentation/backlog-only changes use `docs(scope): ...`, with `backlog` as the common
  scope.
- The history from `2c3b6af9278637546331f301f45b9b24d3f10423` onward has been normalized to
  this convention.
- `[GATE]` The commit gate runs the build, test, and format commands above (documentation-only
  changes excepted) and the agent commits autonomously once they pass; GPG signing is enabled
  globally, so commit directly without locating gpg or disabling signing.

## Development Phases

Current: **Architecture evolution complete (Phases A–E).** Native game-content sync, the
Phase 4 Mod API, and the typed-deterministic-kernel migration are complete. The typed kernel is
the only supported architecture; see `docs/architecture/README.md` for the active architecture
and `docs/backlog/README.md` for remaining/future work.

MVP explicitly excludes: host migration, dedicated server, auto mod install, generic physics
sync, client prediction, full anti-cheat. The generic Prediction Runtime is a separate future
architecture item, not part of the completed evolution.

## Known Pitfalls

`[REF]` Detailed pitfalls list (historical blueprint, still applicable):
`docs/history/architecture-blueprint.md` §10. Keep these in mind:

- After `dotnet format` (or any external tool) modifies a file, re-`read` that file before using
  Edit; the Edit tool tracks the last-read buffer and refuses stale edits as "file changed since
  it was read".
- Steam P2P is not plain LAN UDP; don't mix the two modes.
- Syncing Transforms fails on physics, parenting, animation, nav, rigidbodies, scene loads.
- Over-reliance on hardcoded offsets/private fields breaks on every game update.
- Harmony patch state leakage: a Prefix that clears an instance field must have its Postfix
  restore it.
- A Steam receive batch is all-or-nothing: catch per message and release in `finally`.
- Lobby identity must follow the actual lobby, not process history.
- Late Steam init must refresh downstream snapshots (SteamId captured as 0).
- Undefined failure modes are not acceptable: define disconnect/dropout/version-mismatch
  behavior.
- `System.Memory` hijacks `.Reverse()` on arrays; use reverse-index loops or
  `Enumerable.Reverse`.
