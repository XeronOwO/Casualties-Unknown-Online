# AGENTS.md

**The first tool call of every round prints step 0's rules** (`rules/00-work-mode.md` + its `.local.md`, in full) before
any other call touches the repository, and **every step the round then enters is read in full at the moment it is entered**
(design before the first edit, implement before the first change, verify before the first reading, commit before the commit,
acceptance before the batch). No exception for a short round, a continued round, or "I already know this". If a round's first
call was something else, the read is not done — stop and do it. Measured 2026-11-02: a long session read only step 0, and
every other step's rules were guessed at for hours.

Working rules for this repository. Each step below has one committed rule file and one local sibling; **read both in
full at the start of the step, every round, even if the previous round read them**. The rules live there, not here —
this page is the map.

Machine facts and personal working notes live in the gitignored `AGENTS.local.md` and in the `*.local.md` sibling of
each step. Never let the instruction set outgrow the session's load budget.

[REF] Docs standard `docs/AGENTS.md` · Architecture `docs/architecture/current.md` · Decisions `docs/decisions/active.md` ·
Evidence `docs/evidence/verification.md` · Backlog `docs/backlog/README.md` · Acceptance `docs/acceptance/AGENTS.md` ·
Agent detail `docs/development/agent-reference.md`

## Project

**Casualties Unknown: Online (CUO)** is a BepInEx multiplayer framework for *Casualties Unknown* (Demo): host-authoritative
co-op over Steam. A stable **Runtime** (protocol, session state machine, serialization, tick/snapshot, logging, versioning)
is separate from the replaceable **Game Adapter** — the only layer that knows the game's private types.

## Build & commit commands

```bash
dotnet build CasualtiesUnknownOnline.slnx
dotnet test CasualtiesUnknownOnline.slnx     # before every commit; also runs the normative gates
dotnet format CasualtiesUnknownOnline.slnx   # mandatory before every commit
```

Deploy with `tools/deploy.ps1 -GameDir "<game-dir>"`, verify with `tools/verify-deploy.ps1`. A pure documentation change
skips build, test and format. Target `net48`, `LangVersion=preview`, nullable on, warnings as errors.

## The steps

| # | step | rule files (committed + local) |
|---|---|---|
| 0 | working mode — one continuous item, four end states, unlimited budget | `rules/00-work-mode.md` + `.local.md` |
| 1 | frame the task from the player's side | `rules/01-task-frame.md` + `.local.md` |
| 2 | design from the player's action and the game's own chain | `rules/02-design.md` + `.local.md` |
| 3 | map every entrance | `rules/03-entry-mapping.md` + `.local.md` |
| 4 | count the family, fix the family | `rules/04-family.md` + `.local.md` |
| 5 | plan and self-check the plan | `rules/05-plan.md` + `.local.md` |
| 6 | make the failure visible first | `rules/06-red-first.md` + `.local.md` |
| 7 | implement | `rules/07-implement.md` + `.local.md` |
| 8 | verify on the real thing | `rules/08-verify.md` + `.local.md` |
| 9 | adversarial self-check | `rules/09-self-review.md` + `.local.md` |
| 10 | structure review | `rules/10-structure.md` + `.local.md` |
| 11 | commit and push | `rules/11-commit.md` + `.local.md` |
| 12 | close the item and talk to the person | `rules/12-report.md` + `.local.md` |
| 13 | acceptance batch on the real game | `rules/13-acceptance.md` + `.local.md` |

All under `docs/development/rules/`. The index there explains what belongs in which half.

## Phases

The typed deterministic kernel is the only supported architecture (`docs/architecture/README.md`). MVP excludes host
migration, dedicated servers, mod auto-install, generic physics sync, client prediction and anti-cheat.
