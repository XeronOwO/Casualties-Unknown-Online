# AGENTS.md

Instructions for AI coding agents and contributors working in this repository.

## Document Scope & Classification

- `AGENTS.md` (this file) is the shared, portable rule set. `AGENTS.local.md` is the machine- and
  person-specific companion — local paths, personal preference, execution detail, past mistakes — and is
  gitignored and never committed. The same rule may exist in both: this file is authoritative for the
  rule, the local file for execution.
- Committed content is English, the shared working language. Another language is appropriate only where
  the deliverable targets that language's audience; the human documentation is that case, paired
  English + Chinese under `docs/en/` and `docs/zh/`.
- This file binds and routes; the pages it links carry the detail, the examples and the long lists. Read
  the linked page before working in that area.
- Requirement triage: personal or machine-specific → `AGENTS.local.md`; shared or generally beneficial →
  this file or `docs/`; ambiguous → ask the user.
- This repository is the long-term reference implementation for these standards; new projects adopt its
  rules and gates rather than duplicating them in a global agent file.

[REF] Document system: `docs/AGENTS.md` (binding, auto-loaded under `docs/`) ·
Contributor pages: `docs/en/contributing/README.md` ·
Architecture: `docs/architecture/current.md` · Decisions: `docs/decisions/active.md` ·
Evidence: `docs/evidence/verification.md` · Backlog: `docs/backlog/README.md`.
Binding architecture and sync rules — host-authoritative ownership, judgment ownership, latency never a
judgment input, accept-first arbitration, dedicated events over snapshots — are explained in
`docs/en/internals/` (the same paths under `docs/zh/`); the agent-side detail stays in
`docs/development/agent-reference.md`.

## Project Overview

**Casualties Unknown: Online (CUO)** is a BepInEx multiplayer mod framework for *Casualties Unknown*
(Demo). The game has no multiplayer; CUO adds Steam-based Host + Guests co-op by reorganizing local-only
game state into a host-authoritative simulation with guest input/state sync.

- Stable **CUO Runtime**: protocol, host/guest state machine, mod loading, serialization, tick/snapshot,
  logging, version negotiation.
- Replaceable **Game Adapter**: the only layer that knows the game's private types and absorbs
  game-update churn.

## Build & Commit Gates

```bash
dotnet build CasualtiesUnknownOnline.slnx
dotnet test CasualtiesUnknownOnline.slnx          # must pass before every commit
dotnet format CasualtiesUnknownOnline.slnx        # mandatory before every commit
```

- `[GATE]` All three pass before a commit; `dotnet format` is build-enforced. `dotnet test` also runs
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests`, the Roslyn fully-qualified-name gate plus the C#
  ports of the former `tools/check-*.ps1` gates; the rule-to-gate map is
  `docs/evidence/normative-gates.md`.
- `[RULE]` A pure documentation change — no `src/`, `tests/` or `tools/` modification — skips build, test
  and format: review the diff and commit directly. Documentation that describes a code change is
  committed with that code and the gates run in the same commit.
- `[RULE]` Test parallelism is contract, not accident: the runner configuration, the `GameAssembly`
  collection for a class that writes a process-global static, no per-node rolling log file, the 40-case
  ceiling per test class, the rule that behaviour-family splits never duplicate `MemberData` rows, and
  the `1x` thread cap. Measured numbers and the three-run median method:
  `docs/evidence/test-parallelization.md`.
- `[RULE]` Test feedback tiers: a class that constructs the production composition root, a full
  simulation world/replay harness, a shared full-stack fixture, the game-assembly reflection host or real
  loopback sockets carries `[Trait("Category", "Integration")]`; untagged classes are the fast inner
  loop (`--filter "Category!=Integration"`).
- Target `net48`, `LangVersion = preview`, nullable enabled, warnings as errors.
- NuGet sources: nuget.org + nuget.bepinex.dev + nuget.samboy.dev.
- Game assemblies are copyrighted: only the Game Adapter may reference them.
- `[RULE]` The packaged plugin deploys via `tools/deploy.ps1 -GameDir "<game-dir>"` and the deployment is
  verified against this tree's build output by `tools/verify-deploy.ps1`; machine paths live in
  `AGENTS.local.md`.
- Detail: `docs/en/contributing/build-and-test.md`.

## Engineering Discipline

Standards are "done well, not just done". Delivering means passing three reviews before hand-off:
**architecture** (single responsibility, clean dependencies), **tests** (runtime verification of
behaviour) and **maintainability** (readable and changeable by the next person). "It runs" is the floor,
not the goal.

- `[CRITICAL]` No shortcuts: take the proper path when one exists. Manual workarounds, hard coding,
  copy-paste and skipped tests borrow against the future.
- `[CRITICAL]` Pragmatism is not a shield: a suboptimal choice needs an architectural reason, not "not
  enough time". Do the right thing once.
- `[CRITICAL]` Root cause over patch stacking: ask first whether an architecture change can eliminate the
  cause. Do not pile on features or patches, and do not avoid a needed refactor out of fear of churn —
  the cost moves, it does not disappear.
- `[RULE]` Pragmatic future-proofing: leave room for foreseeable evolution, do not pre-build for imagined
  futures. "We'll deal with it later" is not a default excuse.
- `[CRITICAL]` Compatibility is never a design input, before and after release. The boundary is the
  protocol-version check at handshake — the host refuses a peer whose `HandshakeMsg.Protocol` differs and
  the guest ends the session on a mismatched `HandshakeAckMsg.Protocol` — so a change never has to keep
  an old wire or save shape alive, retain a legacy field, or pick a weaker mechanism to avoid a version
  bump: change the wire the mechanism needs and bump `ProtocolVersion.Current` in the same change
  (`docs/decisions/active.md` holds the numbering policy). "No wire change" and "host untouched" are
  facts worth recording, never merits or constraints in a design argument.
- `[RULE]` Self-review happens before hand-off, not after review.
- `[CRITICAL]` Tests cover core scenarios plus edge, exception and failure paths. Happy-path-only
  coverage never proves "done well".
- `[CRITICAL]` Every key path and logical branch must be observable; choose the log level by trigger
  frequency (high-frequency → Verbose/Debug, low-frequency or exceptional → Warn/Error) and carry the
  context that locates a failure: branch, state, ids, input, result. An unobservable key path is
  unfinished; the standard is "one pass of logging is enough".
- Detail: `docs/en/contributing/gates-and-rules.md`.

## Engineering Conventions (binding)

1. `[RULE]` English by default in all code, comments and committed docs (see *Document Scope &
   Classification* for the language boundary).
2. `[RULE]` Modern idiomatic C#: `var`, nullable, `is null`/`is not null`, collection expressions, using
   aliases for name collisions. **Unity objects are the exception**: `== null` / `!= null`, because the
   overload detects scene-reload-destroyed objects.
3. `[RULE]` Evidence-based changes: cite decompiled sources (`reversing/`, file:line — that tree is never
   edited, so its line numbers are stable) before touching code; for our own `src/` and `tests/` cite the
   path plus the quoted text and never a line number. Fix root causes, not symptoms.
4. `[CRITICAL]` **Absolute-machine-path red line**: no absolute machine path may ever enter git — no
   drive-letter path, UNC path or Unix-style path rooted at home, user, temp, var or opt. Existing
   tracked absolute paths are removed, not left as historical debt. Local paths belong only in gitignored
   `AGENTS.local.md` or in placeholders such as `<game-dir>`, `<sandbox-root>`. Enforced by
   `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths`.
5. `[RULE]` Self-learning: record reusable, generalizable knowledge in this file, `docs/`, or memory; be
   selective.
6. `[RULE]` Patch hooks report only verified writes: a Prefix that swallows a write must not let the same
   Postfix report it. Re-read the written state or pass the verdict explicitly.
7. `[RULE]` Prefer `using` directives and aliases over fully qualified type names; use a fully qualified
   name only when unavoidable (a HotRepl eval has no `using`). Enforced by `FullyQualifiedNameGateTests`.
8. `[RULE]` Reuse the game's existing native UI whenever a player-facing feature already has one; do not
   build a parallel CUO UI to replace it, and prefer a thin adapter plus patches. If a native UI cannot
   be reused, record the concrete blocker with evidence and get user direction before adding custom UI.
9. `[RULE]` **Ask on functional design, execute on implementation**: research and confirm a design that is
   ambiguous, has several reasonable options or has user-visible trade-offs; do not ask about routine
   implementation detail once design and requirements are clear. **Which backlog item to work on next,
   and in what order, is not such a decision** — take it from the ticket's priority, its dependencies and
   the handoff's order.
10. `[RULE]` **Future backlog items are not work items**: `future/` means "deferred by decision", not
    "pending implementation", and those items are implemented only when the user promotes them.
11. `[RULE]` **Large or multi-stage work is split into stages** that each produce a verifiable result; the
    original umbrella ticket may then be dropped.
12. `[RULE]` **Empty directories** that must stay in the repository carry a 0-byte `.gitkeep`.
13. `[RULE]` **Extension methods** use the C# 14 `extension` syntax, a strict superset of the classic
    `this X` form that compiles to the same call sites; migrate one on sight.
    `SourceShapeGateTests.ExtensionMethods_UseTheCsharp14ExtensionSyntax` fails on a new classic
    declaration. This is an implementation detail and needs no user round trip.
14. `[RULE]` **Minimum visibility, declared stability**: only a capability that is designed, documented
    and reviewed becomes a public third-party contract, and `Runtime`/`GameAdapter` are implementations a
    mod may patch but is never promised. The `Abstractions` public surface is a recorded, gate-enforced
    baseline (`docs/contracts/abstractions-api-baseline.txt`, `ApiSurfaceGateTests`): an addition or removal
    fails until the baseline is reviewed and updated, a removal names its reason, and a surface that is
    not `Stable` declares its level with `[ApiStability]`
    (`docs/api/advanced-modification-policy.md`).

Rationale, the gate behind each item and the full statements: `docs/en/contributing/gates-and-rules.md`.

## Development Workflow (binding)

Applies to all normal work — features, bug fixes, user-facing changes and internal improvements — not
only to previously rejected items or user-reported problems.

**Hard order:** understand → mechanism inventory → plan + self-check table → user approval (large
changes only) → red test (defects only) → implement → build/gates → deploy → runtime verification →
independent adversarial self-check → structure review → commit.

1. `[RULE]` **Frame the task from the user's perspective**: reproduction steps, expected behaviour and an
   acceptance matrix (roles, directions, views, related families) for a user-reported issue; functional
   intent, user-visible behaviour and scope for feature work.
2. `[RULE]` **Resolve functional design before implementation**: research reference implementations and
   ask the user when the design is ambiguous or has user-visible trade-offs; once it is clear, proceed
   autonomously on implementation detail.
3. `[RULE]` **Check for a reusable native game UI or mechanism first.**
4. `[GATE]` **Defects only: make the expected failure visible before fixing.** Add a regression test or
   runtime probe that fails on the current code, covering the reported scenario and its neighbours, and
   record the red before implementing. A compile error caused by a missing type is not a red, and "the
   test passes now" is no substitute for having observed it fail.
5. `[GATE]` **Implement, then verify against the full matrix**: build → deploy the latest artifacts →
   verify the deployed artifact identity (hash/timestamp) → runtime and log checks where applicable →
   every acceptance row passes. A build that passes without the latest DLLs running is not completion.
6. `[GATE]` **Run an independent adversarial self-check BEFORE the commit**, in a fresh context (an
   independent subagent, not the reasoning path that produced the change) and against the FROZEN working
   tree; cover reverse directions, third-party views, edge cases and adjacent regressions. Fix its
   findings in the SAME commit. Request an interim report at the reviewer's first milestone; do not edit
   until the final report lands. Template, with the FULL and NARROWED tiers:
   `docs/development/review-prompt.md`.
7. `[RULE]` **A rejected delivery or a failed verification runs a root-cause loop**: analyse why it was
   missed, record the process lesson, fix the leak. Moving the ticket back is not enough.
8. `[CRITICAL]` **Keep incomplete or unverified work open**: no completion claim, no reclassifying a
   known gap as "future", no moving on before the exact scenario and the full acceptance matrix are
   verified.
9. `[RULE]` **Budget the working context; hand off at phase boundaries**: after a task, or a phase of a
   multi-stage task, judge how much context is spent and stop at the boundary with a handoff that states
   what landed, what is verified and what comes next — an exhausted context produces shallow work.

Detail: `docs/en/contributing/review-and-delivery.md`.

### Quality & Delivery

- `[CRITICAL]` No self-assumption: every claim needs source evidence (the path plus the quoted text,
  never a line number) or runtime evidence. The runtime is the judge, not the plan: a paper review is not
  a review, so ask "how does this chain get proven at runtime?" before hand-off.
- `[CRITICAL]` **Fix the family, not just the reported case**: inspect sibling mechanisms and other
  modules for the same defect pattern and align the whole family in the same cycle, or record a backlog
  item when the rest is genuinely out of scope.
- `[CRITICAL]` **User-found issues are hard release blockers**: fix until the exact reproduction is
  resolved and verified, not merely until tests and gates pass; state an external blocker explicitly
  rather than declaring the issue done.
- `[CRITICAL]` **Deployment and artifact verification are part of completion** after any runtime
  behaviour change.
- `[CRITICAL]` **Development-period verification is simulation/static-evidence based**: no manual
  dual-client acceptance during development — deployment to the physical machine and dual-client
  acceptance are the user's release-cycle actions.
- `[CRITICAL]` **Acceptance-readiness audit before review**, answered with evidence: does the game
  already have a native UI for this surface (reuse it); was the whole family audited across roles,
  directions, participants and third-party views; was the exact reproduction covered by a test or runtime
  trace against the latest deployed DLLs; were unsupported operations left as future only with explicit
  user acceptance. Passing tests and gates is necessary, not sufficient.
- `[CRITICAL]` **The adversarial self-check must be independent** — a fresh context, never the reasoning
  path that produced the fix.
- `[GATE]` Follow `docs/evidence/delivery-checklist.md` (integrity checked by
  `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`): check its boxes one line at a time
  with evidence on the same line; bulk checking is forbidden.

### Definition of Done for user-facing changes

A user-facing change is not complete just because tests and gates pass. Before moving on or reporting
completion: the exact user reproduction no longer reproduces; all roles, directions, participant views
and third-party views are verified; the game's existing native UI is reused where one exists; the latest
build is deployed and its artifact identity is verified; an independent adversarial self-check has
passed; no known failing scenario is left as "future" without explicit user acceptance; and the root
cause is addressed rather than patch-stacked. The executable gate is
`docs/evidence/delivery-checklist.md`.

## Commit Message Convention

- `[RULE]` Conventional Commits for every commit: `type(scope): summary`, with the allowed types `feat`,
  `fix`, `docs`, `test`, `chore`, `refactor`, `perf`, `revert`, `build`, `ci`, `style`.
- Scope is a short lowercase domain or path name (`protocol`, `carry`, `backlog`, `projection`); omit it
  when there is no clear domain. The summary starts with an imperative verb, stays lowercase after the
  prefix (proper nouns and acronyms keep their case), has no trailing period and is one line; an optional
  body starts after a blank line. Pure documentation or backlog changes use `docs(scope): …`.
- `[GATE]` The commit gate runs the build, test and format commands above (documentation-only changes
  excepted) and the agent commits autonomously once they pass. GPG signing is enabled globally: commit
  directly without locating gpg or disabling signing.

## Development Phases

- The typed deterministic kernel is the only supported architecture; see `docs/architecture/README.md`
  for the active architecture and `docs/backlog/README.md` for remaining/future work.
- MVP explicitly excludes: host migration, dedicated server, auto mod install, generic physics sync,
  client prediction, full anti-cheat. The generic Prediction Runtime is a separate future architecture
  item.
