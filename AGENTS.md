# AGENTS.md

Working rules for this repository. **This file carries rules, one line each; the reasoning lives in the linked pages.** Read
the linked page before working in that area. Full statements: `docs/en/contributing/gates-and-rules.md`.
Machine/person-specific facts — local paths, ports, sandbox and account names — live in the gitignored `AGENTS.local.md`
beside this file. Never let one grow past the load budget: see *Instruction Files*.

[REF] Docs standard `docs/AGENTS.md` · Architecture `docs/architecture/current.md` · Decisions `docs/decisions/active.md` ·
Evidence `docs/evidence/verification.md` · Backlog `docs/backlog/README.md` · Acceptance `docs/acceptance/AGENTS.md` ·
Agent detail `docs/development/agent-reference.md`.

## Project

**Casualties Unknown: Online (CUO)** is a BepInEx multiplayer framework for *Casualties Unknown* (Demo): host-authoritative
co-op over Steam. A stable **Runtime** (protocol, session state machine, serialization, tick/snapshot, logging, versioning)
is separate from the replaceable **Game Adapter** — the only layer that knows the game's private types.

## Build & commit gates

```bash
dotnet build CasualtiesUnknownOnline.slnx
dotnet test CasualtiesUnknownOnline.slnx     # before every commit; also runs the normative gates
dotnet format CasualtiesUnknownOnline.slnx   # mandatory before every commit
```

- A pure documentation change (no `src/`, `tests/`, `tools/`) skips build/test/format; docs describing a code change ride
  that code's commit.
- Target `net48`, `LangVersion=preview`, nullable on, warnings as errors.
- Only `GameAdapter` references game assemblies.
- Deploy with `tools/deploy.ps1 -GameDir "<game-dir>"`, verify with `tools/verify-deploy.ps1`; commit BEFORE deploying.
- Gates green in the same commit as the change; `tools/check-delivery.ps1` runs before the commit (see *Delivery*).
- **A red gate is evidence the work is not finished — it licenses continuing, never stopping and never reverting.** A gate, matcher, fixture or
  sample that the change made stale is part of that change and is fixed in the same commit.
- Fast loop: run the focused test/gate first; the full suite ONCE before the commit (`--no-build` after a build).

## Engineering discipline

- **Gameplay first.** Design from the player: who uses it, what it does, what it costs, what it refuses, what the player
  sees. "It compiles", "it wires up", "the code has no consumer" are never reasons. A "where does it go" question is
  answered by the fiction, not by the destinations the code happens to have.
- **Design from the player's INPUT, not the code's opportunity.** Write the exact input sequence (button held, window open,
  what is still clickable) and walk it through the game's own control flow before accepting a gesture. **An impossibility
  claim is a claim about EVERY entrance**: enumerate them and quote the code, or do not make it.
- **Native parity is the standard** for anything mirroring a native action or crossing players
  (`docs/development/native-parity.md`): the player's action and expectation, the native call site the game itself runs, and
  how the remote case reaches the same chain — stated before the first code edit, verified by running the same action both
  ways in one session. A stand-in object, a "give it first" ordering or a replayed effect is the defect, not the fix.
- **Read a surface's design record before writing its design**: the decision (`docs/decisions/active.md`) and the accepted
  ticket (`docs/backlog/done/`). Judge against that design, never against what the game happens to do.
- **A design page is read before a behaviour change and written back after it**, with `docs/design/audit/code.md` updated in
  the same commit. A corpus neither read nor written back is dead weight.
- **CUO's own code is never evidence of what the GAME does.** Native behaviour comes from `reversing/` alone, quoted with
  file:line. Reading a patch and concluding "the game cannot X" is the defect this rule names.
- **A cause is established by READING**, never by adding a log and re-running: a run costs a build, a deploy, a restart and a
  human at the keyboard. Instrument through the running client's eval/patch channel; spend a run on verifying a FIX only.
- **No shortcuts, no patch stacking.** Prefer the change that removes the cause; a suboptimal choice needs an architectural
  reason, not pragmatism.
- **A mechanism needs a named consumer**: name the caller that reads what it produces, or do not add it.
- **Compatibility is never a design input.** The boundary is the handshake's protocol check; change the wire the mechanism
  needs and let the number follow (`docs/decisions/active.md`).
- **Never trade correctness for a quiet wire**, and never propose a message that already exists: trace where the fact
  travels before adding a channel.
- **Normal gameplay is never shipped as a console command.** The console is diagnostic/administrative; the player-facing
  entry lives on the player's own screen.
- **Fix the family, not the reported case.** "Not introduced by this change" is never a reason to leave a defect alone.
- **Tests cover core, edge, exception and failure paths**; every key path and branch must be observable from the log, with
  context that locates a failure (branch, state, ids, input, result).
- **Committed content is English — every language**, not only the one that last slipped. Only `docs/zh/` and `zh.json` carry
  another language (plus a fixture that must). Quote a report in translation, marked as such. `tools/check-language.ps1`
  checks the staged diff.
- Rationale and the gate behind each rule: `docs/en/contributing/gates-and-rules.md`.

## Conventions

1. English in code, comments and committed docs.
2. Modern idiomatic C# (`var`, nullable, collection expressions, `is null`); **Unity objects use `== null`**.
3. Evidence-based changes: `reversing/` by file:line (that tree is never edited); our own `src/`/`tests/` by path and quoted
   text, never line numbers. `reversing/` holds every decompiled assembly (the game's and the reference mods) — answer a
   question about the game or a reference mod by reading there, never the web.
4. **No absolute machine path in git** — no drive letters, UNC, home/temp/var paths; placeholders only. Same for host
   names, ports, sandbox/account names and credentials: `AGENTS.local.md` or `.agent-local/`. Enforced by
   `RepositoryGateTests`; the commit message and history are a pre-commit check (decision 261).
5. **A tracked file is written by the edit tools, never by a shell round trip** — `git show > file`, `Set-Content`,
   `Out-File` and `[System.IO.File]::WriteAllText` over existing content are the three shapes that emptied and UTF-16'd two
   pages in one session. Enforced by `RepositoryGateTests.EveryTrackedTextFile_IsUtf8WithoutNulBytesAndNotEmpty` (NUL byte,
   BOM, invalid UTF-8, or an unexpectedly empty tracked file).
6. Self-learning: record reusable, generalizable knowledge in this file, `docs/` or memory; be selective.
7. A patch hook reports only verified writes: re-read the state or pass the verdict explicitly.
8. Prefer `using` directives and aliases; a fully qualified name only when unavoidable (`FullyQualifiedNameGateTests`).
9. Reuse the game's own UI when a player-facing feature already has one; a parallel CUO UI needs a recorded blocker and the
   user's direction.
10. **Ask on functional design, execute on implementation.** Ambiguity with user-visible trade-offs goes to the user; routine
    detail does not. **A rejection is a question, not a mandate** — inferring a removal, rename or new mechanism from "this is
    wrong" is the violation. Which ticket comes next is not such a decision: take it from priority and dependencies.
11. `future/` means deferred by decision, not pending work; implement only when the user promotes it.
12. Large work is split into stages that each produce a verifiable result.
13. Empty directories that must stay carry a 0-byte `.gitkeep`.
14. Extension methods use the C# 14 `extension` syntax (`SourceShapeGateTests`).
15. Minimum visibility, declared stability: only designed, documented, reviewed capability becomes public; the
    `Abstractions` baseline is gate-enforced and a removal names its reason.
16. No erased type in the mod-visible contract (`byte[]`, `object`, `Delegate`, `dynamic`, `IntPtr`); a binary payload is an
    explicit leaf.
17. **Member reports are bounded by DATA, or the gap is named** (decision 265): check a claim against a fact the receiver
    holds; never model what a player is able to do (that is anti-cheat, out of MVP scope). Census:
    `docs/architecture/member-report-validation.md`.
18. **A console command's shape follows Minecraft's CURRENT template, fetched not rewritten from memory** (argument list,
    order, which are optional); a deviation records its reason at the point of design.
19. **A wire field whose 0 is a legal value is encoded as `value + 1` (0 = absent).** protobuf-net omits 0-valued ints, so a
    raw slot index, count or id cannot carry a real 0 — the omitted zero arrives as the class default and silently changes
    meaning (a give answered for slot 0 became "no destination", 2026-10-26). Encode on send, decode on receive, and pin the
    zero case with a test.

## Development workflow

**Hard order:** understand → entry mapping (whenever behaviour crosses players or mirrors a native action) → mechanism
inventory (and the consumer) → plan + self-check table → user approval (large changes only) → red test (defects only) →
implement → build/gates → deploy → runtime verification → adversarial self-check → structure review → commit.

1. Frame the task from the user's perspective: reproduction, expectation, acceptance matrix (roles, directions, views).
2. Resolve functional design before implementing; then proceed autonomously.
3. Check for a reusable native UI or mechanism first.
4. **Map CUO's entries to the game's own, one line each**, before designing anything that crosses players: the input
   sequence, the native call site each gesture mirrors, and whether the action needs information the gesture cannot carry
   (then refuse or ask — never a silent fallback). Keep entries isolated: the item's own data decides which entry runs.
5. **Defects only: make the failure visible first** (a red test or a runtime probe); a compile error is not a red.
6. Implement, then verify against the full matrix: build → deploy → verify the artifact identity → runtime/log checks →
   every acceptance row.
7. **Adversarial self-check before the commit**, against the frozen tree; fix its findings in the same commit
   (`docs/development/review-prompt.md`).
8. A rejected delivery runs a root-cause loop and records the lesson in the rule file that loads next time.
9. **Keep incomplete or unverified work open**: no completion claim, no "future", no moving on.
10. **A work item runs to its written end**: the RUN SHEET in the ticket decides, not the feeling. Between steps only tool
    calls; a message ends the cycle only when the sheet is complete, a decision needs the user, or the user asked. "The
    context is spent" does not exist. A cycle that ends incomplete is recorded as failed, with where it stopped.

## Quality & delivery

- No self-assumption: every claim needs source or runtime evidence; the runtime is the judge.
- **User-found issues are hard release blockers**: fix until the exact reproduction is resolved, not until tests pass.
- **Acceptance is agent-run** (`docs/acceptance/AGENTS.md`): judge every row from your own evidence; an unjudgeable row is a
  named residual, never a weaker check.
- A blocked row never becomes the whole cycle: at most two batches per row, then move to the next ticket.
- **The delivery checklist is a SCRIPT, not a test** (`tools/check-delivery.ps1`): fill the boxes as steps complete, run the
  script before the commit — it validates, archives the cycle's record, and RESETS every box so the next cycle starts clean.
- A cycle that only adds tooling or records a finding has not advanced the backlog: progress is judged rows and moved
  tickets.
- Acceptance-readiness before review: native UI reused, family audited across roles/directions/views, the exact
  reproduction covered on the latest deployed build, every remaining row judgeable, residuals named.

## Commit messages

Conventional Commits: `type(scope): summary` (types `feat`, `fix`, `docs`, `test`, `chore`, `refactor`, `perf`, `revert`,
`build`, `ci`, `style`). Scope is a short lowercase domain. Summary is imperative, lowercase, no trailing period; the body
carries the evidence. Docs/backlog changes use `docs(scope): …`.

## Instruction files

`AGENTS.md` (rules), `AGENTS.local.md` (machine facts, gitignored), and one pair per area (`docs/`, `src/`, `tests/`,
`tools/`, `docs/design/`, `docs/acceptance/`, `docs/en/`, `docs/zh/`). How to write them:

- **Rules only, one line each.** The reasoning, examples and long lists belong in a linked page; a file that explains itself
  is a file that will be truncated.
- Each area file ≤ 5 KB (`AgentInstructionBudgetGateTests`); root `AGENTS.md` + `AGENTS.local.md` ≤ 64 KB, and the whole set
  — including the user-level file — must fit the session's 65,536-byte load budget, because what does not fit is dropped
  silently and the WIDEST file goes first.
- A rule that needs a paragraph is a page: move it and leave the pointer.
- Prune: duplicated rules, history, and anything a gate already enforces mechanically are deleted, not kept "for context".

## Phases

The typed deterministic kernel is the only supported architecture (`docs/architecture/README.md`). MVP excludes host
migration, dedicated servers, mod auto-install, generic physics sync, client prediction and anti-cheat.
