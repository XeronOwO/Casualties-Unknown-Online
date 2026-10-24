# AGENTS.md

Instructions for AI coding agents and contributors working in this repository.

## Document Scope & Classification

- `AGENTS.md` (this file) is the shared, portable rule set and the authority for the rules. The
  machine- and person-specific companion is the gitignored `AGENTS.local.md` beside it — local paths,
  personal preference, execution detail, past mistakes. The same rule may exist in both: this file is
  authoritative for the rule, the local file for execution.
- This file binds and routes; the pages it links carry the detail, the examples and the long lists. Read
  the linked page before working in that area.
- Committed content is English. The human documentation is the one paired exception: English + Chinese
  under `docs/en/` and `docs/zh/`; `docs/AGENTS.md` is binding for every page under `docs/`.
- Requirement triage: personal or machine-specific → `AGENTS.local.md`; shared or generally beneficial →
  this file or `docs/`; ambiguous → ask the user.
- This repository is the long-term reference implementation for these standards; new projects adopt its
  rules and gates rather than duplicating them in a global agent file.

[REF] Document standard: `docs/AGENTS.md` · Contributor pages: `docs/en/contributing/README.md` ·
Architecture: `docs/architecture/current.md` · Decisions: `docs/decisions/active.md` · Evidence:
`docs/evidence/verification.md` · Backlog: `docs/backlog/README.md` · Acceptance:
`docs/acceptance/AGENTS.md` · Agent detail: `docs/development/agent-reference.md`.

## Project Overview

**Casualties Unknown: Online (CUO)** is a BepInEx multiplayer mod framework for *Casualties Unknown*
(Demo). The game has no multiplayer; CUO adds Steam-based Host + Guests co-op by reorganizing local-only
game state into a host-authoritative simulation with guest input/state sync. The stable **CUO Runtime**
(protocol, host/guest state machine, mod loading, serialization, tick/snapshot, logging, version
negotiation) is separate from the replaceable **Game Adapter** — the only layer that knows the game's
private types and absorbs game-update churn. Why it works this way: `docs/en/internals/` (the same paths
under `docs/zh/`).

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
- `[RULE]` Test parallelism, the feedback tiers (`[Trait("Category", "Integration")]`) and the measured
  timings are contract, not accident: `docs/en/contributing/build-and-test.md`.
- Target `net48`, `LangVersion = preview`, nullable enabled, warnings as errors.
- Game assemblies are copyrighted: only the Game Adapter may reference them.
- `[RULE]` The packaged plugin deploys via `tools/deploy.ps1 -GameDir "<game-dir>"` and the deployment is
  verified against this tree's build output by `tools/verify-deploy.ps1`; machine paths live in
  `AGENTS.local.md`.

## Engineering Discipline

Standards are "done well, not just done". Delivering means passing three reviews before hand-off:
**architecture** (single responsibility, clean dependencies), **tests** (runtime verification of
behaviour) and **maintainability** (readable and changeable by the next person). "It runs" is the floor,
not the goal.

- `[CRITICAL]` **Gameplay first — never "the code connects"**: CUO is a game, so a player-facing command,
  UI or mechanic is designed from the player's need and normal gameplay expectations FIRST — who uses it,
  what it does, what it costs, what it refuses, and what the player sees — and only then implemented. A
  compatible service, a matching signature or an unused mechanism is never a design reason, and "it wires
  up", "it compiles" and "the mechanism would otherwise have no consumer" are forbidden justifications.
  Every code change names the player-facing or system problem it solves and the design it implements; a
  change that names only the code it touches is the "code for code's sake" this rule refuses. A design
  with user-visible trade-offs goes to the user (Development Workflow rule 9); the implementation's own
  help text or doc comment is never the design. **The same rule governs a "where does this go" question**:
  the answer is where the thing SHOULD end up in the fiction (the world, the player's hands), never the
  destinations the code already has to hand (a free slot, a discard, a refused row) — an item a legitimate
  rule cannot place still belongs somewhere a player would accept, and a menu of the code's own options is
  not a design question but the defect restated (the user's 2026-10-23 ruling on a refused restore child:
  it falls to the ground).
- `[CRITICAL]` **The interaction is designed from the player's INPUT, never from the code's
  opportunity.** Before a player-facing gesture exists, write down the exact input sequence the player
  performs — which mouse button is held, which window is open, and what can still be clicked or pressed
  while it is held — and check it against the game's own control flow (`PlayerCamera`'s drag bracket and
  its UI hit tests are the reference for a drag). A gesture whose input sequence cannot be performed is a
  design defect found at design time, not an implementation detail. **An impossibility claim is a claim
  about every entrance**: enumerate the surfaces that open the window the gesture needs and the button each
  needs before declaring the sequence unperformable — a conflict on one entrance is not a property of the
  control scheme (the player panel opens a backpack with a LEFT click, so a drag holding the right button
  can reach it). **An entry behind a hotkey the player has no reason to know is not the primary entry**: the
  gesture must open what it needs from the act itself (dragging an item onto a player opens that player's
  backpack). A design that needs a new input, a new window or a changed control scheme is user-visible and
  goes to the user before it is built. A call seam that a probe can drive is never evidence that a player
  can reach it.
- `[CRITICAL]` No shortcuts: take the proper path when one exists.
- `[CRITICAL]` Pragmatism is not a shield: a suboptimal choice needs an architectural reason. Do the
  right thing once.
- `[CRITICAL]` Root cause over patch stacking: ask first whether an architecture change can eliminate the
  cause; do not avoid a needed refactor out of fear of churn — the cost moves, it does not disappear.
- `[RULE]` Pragmatic future-proofing: leave room for foreseeable evolution, do not pre-build for imagined
  futures.
- `[RULE]` **A mechanism needs a named consumer**: before adding one, name the caller that reads what it
  produces — a form nothing transports, compares or interprets is a guess, not a design. Deferring the
  question (an envelope that "stays opaque", a field "somebody across a boundary will read") is how a
  mechanism acquires cost with no beneficiary, so the mechanism inventory answers it for everything the
  change ADDS.
- `[CRITICAL]` Compatibility is never a design input, before and after release. The boundary is the
  protocol-version check at handshake — the host refuses a peer whose `HandshakeMsg.Protocol` differs and
  the guest ends the session on a mismatched `HandshakeAckMsg.Protocol` — so a change never keeps an old
  wire or save shape alive, retains a legacy field, or picks a weaker mechanism to avoid a version bump:
  change the wire the mechanism needs, and let the number follow decision 241 — frozen at the pre-release
  baseline, bumped per wire change from the first official release on
  (`docs/decisions/active.md` holds the numbering policy). "No wire change" and "host untouched" are
  facts worth recording, never merits or constraints in a design argument.
- `[CRITICAL]` **A recommendation never trades correctness for a quiet wire.** When the right behaviour needs a
  message, a field or a new verb, that IS the recommendation — offering "record the hole and change no protocol" as
  the conservative option is the defect wearing caution's clothes, and the user has rejected exactly that
  recommendation (2026-10-24, a host-only death whose drops would have stayed unsynchronised). The cost of a wire
  change here is a number that follows the change; the cost of a named hole is shipped wrong behaviour.
- `[CRITICAL]` **…and the mirror error is recommending a message that already exists.** Before proposing a new
  message or channel, trace where the fact ALREADY travels: the host-authoritative kernel carries every fact the
  host itself decides to the members (a host-side item spawn is a committed kernel row, so the members materialise it
  without any new message), and "nothing reports it" is a claim about the code, not an impression. The user's
  2026-10-24 ruling on a host-only blast death asked for a host report channel that turned out to already exist
  (`IItemControl.SendItemSpawned` on the host applies the row and the kernel hands it to the members); the
  recommendation was made without that trace, which is the same defect as dodging a wire change — a design answer
  written before the mechanism was read.
- `[RULE]` Self-review happens before hand-off, not after review.
- `[CRITICAL]` **A normal gameplay flow is never shipped as a console command.** The console is a diagnostic and
  administration surface; when the player's own screen is where the action belongs, it lives there — a rule the
  player can see, a gate on the UI, a piece of world state — because a player does not know the command exists.
  The user has said this more than once (2026-10-24, on a host-dead save: it is a host RULE plus a screen gate,
  never `/save`), so "it is easier as a command" is the defect speaking. A command may exist as a developer or
  recovery path, never as the player-facing entry.
- `[CRITICAL]` Tests cover core scenarios plus edge, exception and failure paths.
- `[CRITICAL]` Every key path and logical branch must be observable; choose the log level by trigger
  frequency (high-frequency → Verbose/Debug, low-frequency or exceptional → Warn/Error) and carry the
  context that locates a failure: branch, state, ids, input, result. An unobservable key path is
  unfinished; the standard is "one pass of logging is enough".
- Rationale, the gate behind each item and the full statements: `docs/en/contributing/gates-and-rules.md`.

## Engineering Conventions (binding)

1. `[RULE]` English by default in all code, comments and committed docs (see *Document Scope &
   Classification*).
2. `[RULE]` Modern idiomatic C#: `var`, nullable, `is null`/`is not null`, collection expressions, using
   aliases for name collisions. **Unity objects are the exception**: `== null` / `!= null`, because the
   overload detects scene-reload-destroyed objects.
3. `[RULE]` Evidence-based changes: cite decompiled sources (`reversing/`, file:line — that tree is never
   edited, so its line numbers are stable) before touching code; for our own `src/` and `tests/` cite the
   path plus the quoted text and never a line number. **`reversing/` is where EVERY decompiled assembly
   lives — the game's (`Assembly-CSharp`) and the third-party co-op mods (`KrokMP`, `CasualtiesMP`) — so a
   question about how the game or a reference mod behaves is answered by reading a file there, never by
   searching the web or a mod's release page.** The tree is gitignored, so a directory-wide search returns
   nothing: name the file. A reference implementation named in a ticket is a fact to READ, not to recall.
4. `[CRITICAL]` **Absolute-machine-path red line**: no absolute machine path may ever enter git — no
   drive-letter path, UNC path or Unix-style path rooted at home, user, temp, var or opt. Local paths
   belong only in gitignored local files — `AGENTS.local.md` or the `.agent-local/` area — or in
   placeholders such as `<game-dir>`, `<sandbox-root>`. Enforced by
   `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths`. The same rule covers the classes that are
   not paths — host names, network addresses, ports and other run readings, sandbox instance and account
   names, real account ids, credentials — and the two positions a file-level gate cannot see, the commit
   message and the history behind it. What a matcher cannot reach is a pre-commit check rather than a
   gate (`docs/en/contributing/review-and-delivery.md`), by the user's ruling of 2026-10-12
   (decision 261).
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
   implementation detail once design and requirements are clear. **A rejection is a question, not a
   mandate**: a user saying a design or a behaviour is wrong asks what it SHOULD be — inferring a removal, a
   rename, a new surface or a different mechanism from the rejection is the violation, and a user-visible
   command's semantics (who may use it, what it does, whether it exists) are design, not implementation
   detail. **Which backlog item to work on next, and in what order, is not such a decision** — take it from
   the ticket's priority, its dependencies and the handoff's order.
10. `[RULE]` **Future backlog items are not work items**: `future/` means "deferred by decision", not
    "pending implementation", and those items are implemented only when the user promotes them.
11. `[RULE]` **Large or multi-stage work is split into stages** that each produce a verifiable result; the
    original umbrella ticket may then be dropped.
12. `[RULE]` **Empty directories** that must stay in the repository carry a 0-byte `.gitkeep`.
13. `[RULE]` **Extension methods** use the C# 14 `extension` syntax, a strict superset of the classic
    `this X` form that compiles to the same call sites; migrate one on sight.
    `SourceShapeGateTests.ExtensionMethods_UseTheCsharp14ExtensionSyntax` fails on a new classic
    declaration.
14. `[RULE]` **Minimum visibility, declared stability**: only a capability that is designed, documented
    and reviewed becomes a public third-party contract, and `Runtime`/`GameAdapter` are implementations a
    mod may patch but is never promised. The `Abstractions` public surface is a recorded, gate-enforced
    baseline (`docs/contracts/abstractions-api-baseline.txt`, `ApiSurfaceGateTests`): an addition or removal
    fails until the baseline is reviewed and updated, a removal names its reason, and a surface that is
    not `Stable` declares its level with `[ApiStability]`
    (`docs/en/reference/modification-policy.md`).
15. `[RULE]` **No erased type in the mod-visible contract**: a public `Abstractions` member never takes or
    returns `byte[]`, `object`, `Delegate`, `dynamic` or `IntPtr`. A payload is a typed definition or the
    framework's own data model, a handle is a CUO-defined type or an engine type, and a value that really is
    binary is an explicit leaf rather than the envelope. This one is enforced where the decision is made and
    NOT by a scan: a spelling check would flag a legitimate binary leaf while an envelope spelled as JSON
    walked past it, so its declaration could never equal its reach. The review is the contract-shape
    questions every baseline addition already goes through
    (`docs/en/reference/modification-policy.md`; the adversarial pass asks them too,
    `docs/development/review-prompt.md`), and the sweep's three tickets have all landed, so the
    contract carries none of that debt.
16. `[RULE]` **Member reports are bounded by DATA, or the gap is named** (decision 265): the check compares the claim with a fact the receiver already holds — an identity, a value it computed, a table it owns — and never models what a player is ABLE to do. Reach, aim, timing, cooldown and input-rate checks are anti-cheat, which the MVP excludes, so a report with no data anchor stays unbounded and is named as such rather than given a behavioural bound. Concretely: a state a member REPORTS (rather than
    requests) is a claim, so the receiving side checks it against what it can establish on its own, refuses
    what contradicts that BY NAME, and answers the refusal so both sides end up agreeing. Never re-derive
    what the receiver cannot reproduce (an unseeded roll, another client's frame) and never invent an
    identity the message cannot carry. A surface with no anchor declares the anchor it would need and files
    the ticket. The census that makes this answerable per wire member is
    `docs/architecture/member-report-validation.md`, enforced by `ReportValidationCensusGateTests`, and the
    adversarial review asks it (`docs/development/review-prompt.md`).
17. `[RULE]` **A console command's shape follows Minecraft's CURRENT template, fetched not remembered.** The
    console's command family is modelled on Minecraft's (decision 264 chose its `/data` verbs, and the user's
    2026-10-18 ruling asks for its `/summon` template), so before designing or renaming one, read the live Java
    Edition syntax on `minecraft.wiki` and cross-check it with a web search — the argument list, their order and
    which are optional. A verb may deviate only by recording the reason at the point of design (a game fact, an
    earlier user ruling, or the family's own convention), because a shape written from memory is unreviewable: a
    reader cannot tell a typo from a deliberate difference, and the wiki's version moves under the memory. The
    citation is the fetched page, never a recollection.

## Development Workflow (binding)

Applies to all normal work — features, bug fixes, user-facing changes and internal improvements — not
only to previously rejected items or user-reported problems.

**Hard order:** understand → **entry mapping (whenever behaviour crosses players or mirrors a native
action)** → mechanism inventory (**and, for a mechanism the change ADDS, the consumer that reads it**) →
plan + self-check table → user approval (large changes only) → red
test (defects only) → implement → build/gates → deploy → runtime verification → independent adversarial
self-check → structure review → commit.

1. `[RULE]` **Frame the task from the user's perspective**: reproduction steps, expected behaviour and an
   acceptance matrix (roles, directions, views, related families) for a user-reported issue; functional
   intent, user-visible behaviour and scope for feature work.
2. `[RULE]` **Resolve functional design before implementation**: research reference implementations and
   ask the user when the design is ambiguous or has user-visible trade-offs; once it is clear, proceed
   autonomously on implementation detail.
3. `[RULE]` **Check for a reusable native game UI or mechanism first.**
4. `[CRITICAL]` **Map CUO's entry points to the game's own, one line each, before designing anything that
   crosses players or mirrors a native action** — and treat a missing mapping as a design defect, not an
   implementation detail:
   - Write the INPUT SEQUENCE of every gesture — which mouse button is held, which window is open, and
     what can still be clicked or pressed while it is held — and walk it through the game's own control
     flow BEFORE the entry is accepted. A gesture the player cannot perform has no entry: it is a design
     defect, and the adversarial review's A0 asks for the sequence.
   - Name the native CALL SITE each CUO gesture is the counterpart of (a gesture is a call site, not a
     family name; `PlayerCamera.TryPerformSpecialUIAction`'s `WoundViewLimb` branch is the counterpart of
     the medical panel, the radial's `UseItem`/`WearWearable` of the inventory use). An entry with no
     native counterpart is CUO's own invention: its scope is argued with the user, never inherited from
     an older CUO table or list.
   - Ask of every action whether it needs information its gesture cannot carry — a limb, an identity, an
     intent. A fallback that supplies it ("the most injured limb", "the first match") is the defect
     itself: refuse the request or ask the user; never ship the fallback and record it as a limit.
     "Ask the user" means a case NO recorded decision covers: when the rule or a ruling already answers it
     — the item's own data separating two entries, a family the user has already placed on one entry —
     refusing IS that ruling's execution, and re-asking it is itself a process violation.
   - Keep the entries ISOLATED: when one item could be claimed by two of them, the item's own data says
     which action each entry runs. Deciding by order (a family chain, an if-else ladder) means two
     scenarios were merged, and every rule built on top of it inherits the error.
   - The `User approval` step above covers THIS scope, including every user-visible behaviour change it
     introduces: a parent ticket's frozen direction, a handoff's work order or an existing
     implementation are never that approval, and a behaviour change noticed mid-cycle goes back to the
     user as an open question instead of into a limits section.
5. `[GATE]` **Defects only: make the expected failure visible before fixing.** Add a regression test or
   runtime probe that fails on the current code and record the red before implementing; a compile error
   caused by a missing type is not a red, and "it passes now" is no substitute for having observed it fail.
6. `[GATE]` **Implement, then verify against the full matrix**: build → deploy the latest artifacts →
   verify the deployed artifact identity → runtime and log checks → every acceptance row passes. The
   rows of a ticket in `review/` are judged by the agent-run acceptance in `docs/acceptance/`.
7. `[GATE]` **Run an independent adversarial self-check BEFORE the commit**, in a fresh context and
   against the FROZEN working tree; fix its findings in the SAME commit. Template:
   `docs/development/review-prompt.md`.
8. `[RULE]` **A rejected delivery or a failed verification runs a root-cause loop**: analyse why it was
   missed, record the process lesson IN THE RULE AND WORKFLOW FILES THAT LOAD NEXT TIME — a lesson that
   lives in a gitignored, non-auto-loaded notes file has not been recorded — fix the leak. Moving the
   ticket back is not enough.
9. `[CRITICAL]` **Keep incomplete or unverified work open**: no completion claim, no reclassifying a
   known gap as "future", no moving on before the exact scenario and the full acceptance matrix are
   verified.
10. `[CRITICAL]` **A work item runs to its written end — the run sheet decides, not the feeling.**
    Before touching a ticket, write its RUN SHEET into the ticket: the numbered steps, the done
    definition, and the failure branch of each step. Then execute it in one continuous cycle.
    **Between steps only tool calls are allowed**: a message that ends the cycle is reserved for exactly
    three cases — the sheet is complete, a step needs a decision only the user can make, or the user
    asked a question. "The item is too big" is a reason to SPLIT the sheet, never to end the cycle.
    **"The context is spent" does not exist as a fact about this harness** (user's ruling, 2026-10-25: the
    context handed to the agent is the full, complete one and is never insufficient): the phrase, or any
    paraphrase of it — "out of room", "budget blown", "too much to hold" — may not be written, offered as a
    reason to stop, or used to justify a smaller step. Work continues until the sheet is complete; the only
    things that end a cycle are the three above. **A commit is a middle step, never a settlement**: the
    sheet's next step runs in the same cycle. A cycle that ends without its sheet complete is a FAILED
    cycle — record on the ticket where it stopped and why, and let the next cycle resume from that line.
    The cost this rule prices is asymmetric: a stop costs the user a whole round and costs the agent
    nothing, which is exactly why the agent may not spend the user's rounds on its own peace of mind.

Detail: `docs/en/contributing/review-and-delivery.md`.

### Quality & Delivery

- `[CRITICAL]` No self-assumption: every claim needs source evidence (the path plus the quoted text,
  never a line number) or runtime evidence. The runtime is the judge, not the plan: a paper review is not
  a review, so ask "how does this chain get proven at runtime?" before hand-off.
- `[CRITICAL]` **Fix the family, not just the reported case**: inspect sibling mechanisms and other
  modules for the same defect pattern and align the whole family in the same cycle, or record a backlog
  item when the rest is genuinely out of scope.
- `[CRITICAL]` **"Not introduced by this change" is never a reason to leave a defect alone**: when work or
  a run turns one up, check this repository's own history (`git log`, `git blame`) for whether it
  introduced the behaviour; if it did, fix it in the same cycle, or file a ticket that carries the
  evidence and the attribution when it genuinely cannot ride that cycle. Recording "pre-existing" and
  walking on is the failure mode.
- `[CRITICAL]` **User-found issues are hard release blockers**: fix until the exact reproduction is
  resolved and verified, not merely until tests and gates pass; state an external blocker explicitly
  rather than declaring the issue done.
- `[CRITICAL]` **Acceptance is agent-run** (`docs/acceptance/AGENTS.md`): the agent judges every
  acceptance row from the evidence its own run collects and moves the ticket; a row it cannot judge is
  named as a residual for the user, a missing dependency is asked about rather than replaced by a weaker
  check, and a row that needs a capability not yet executable stays `blocked` — no ticket is closed on it.
- `[CRITICAL]` **A blocked row never becomes the whole cycle.** When a row cannot be read with the
  capabilities at hand, the ticket stays open with the row and the missing capability named, and the agent
  moves to the NEXT ticket in the same pass — grinding on one row across cycles is the failure the user has
  reported twice (a cycle that stops after every step, and time spent on one row while the backlog waits).
  At most two batches per row, and the second must be preceded by the code that was missing; when no code
  is missing, the row is blocked and the ticket moves on.
- `[CRITICAL]` **The run sheet is the execution contract, and it is written before the first action.** One
  ticket row, one sheet: numbered steps in order, the evidence each step must produce, the done definition,
  and what each step does when it fails. The sheet lives in the ticket, so a resumed cycle reads it instead
  of re-deriving the plan. A step that fails is retried with a CHANGED approach (a different instrument, a
  different state, a smaller question) — repeating the same call is not a retry, it is churn, and it is
  recorded as a failed cycle. This rule exists because the agent has repeatedly turned one ticket into a
  dozen near-identical cycles, each ending in a status message.
- `[CRITICAL]` **When the user points out a repeat of a process failure, the fix is the RULE FILE, in that
  turn, before any work resumes** — a rewritten paragraph, not an apology, and with the exact trigger
  named so the next occurrence is checkable. The user's standing instruction (2026-10-25) is to keep
  tightening the rule each time they catch it, so a rule that has been caught twice must become mechanical
  (a checklist entry, a gate, a script) rather than a reminder.
- `[CRITICAL]` **Re-entering is not relaunching.** A new layer or a fresh world comes from the RUNNING
  client (leave-world then continue-run, or the in-session layer advance), never by closing and starting
  the clients again; a client restart belongs to a deploy and to nothing else. A batch that ends without a
  judged row is a failed batch: record it and change the capability before the next attempt.
- `[RULE]` **Ticket progress is judged rows and moved tickets, not tooling.** A cycle that only adds
  acceptance recipes or records a finding has not advanced the backlog; tooling counts when it unblocks a
  row in the same pass, and the review block advances by judging rows and moving tickets to `done/`.
- `[CRITICAL]` **Acceptance-readiness audit before review**, answered with evidence: does the game
  already have a native UI for this surface (reuse it); was the whole family audited across roles,
  directions, participants and third-party views; was the exact reproduction covered by a test or runtime
  trace against the latest deployed DLLs; can `docs/acceptance/` judge every remaining row, with the
  residuals named. Passing tests and gates is necessary, not sufficient.
- `[GATE]` Follow `docs/evidence/delivery-checklist.md`: its integrity is checked by
  `DeliveryChecklistGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`, the census of the required
  boxes it declares by `DeliveryChecklistGateTests.DeliveryChecklist_DeclaresEveryRequiredBox`. Check its
  boxes one line at a time with evidence on the same line; bulk checking is forbidden.

**Definition of Done:** the Quality & Delivery bullets above plus the executable checklist in `docs/evidence/delivery-checklist.md`.

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
