# Agent acceptance workflow — foundation

- Status: Done
- Priority: High
- Category: Process / verification tooling
- Source: User instruction (2026-09-27): build the agent's own acceptance workflow so the tickets in `review/` are accepted automatically; the technical documents live in a dedicated area under `docs/`; local machine information stays out of git and uses `AGENTS.local.md`; a missing acceptance dependency (Steam, the game, Sandboxie, HotRepl, and anything else the run needs) is raised with the user instead of silently weakening the run. Same day, two further instructions: the run must **capture its own lessons** (pitfalls, experience) so the acceptance area keeps improving, and acceptance must **not be bound to the single-ticket workflow** — it is expensive, so one run accepts a **batch** of tickets instead of starting a develop/accept loop per ticket.
- Related: `future/adapter-shell-verification-harness.md` (its live-game half is promoted by this work), `docs/acceptance/AGENTS.md`, `docs/acceptance/workflow.md`, `docs/acceptance/dependencies.md`, `tools/acceptance/preflight.ps1`
- Acceptance record: `docs/evidence/acceptance/agent-acceptance-workflow-foundation-20260927.md` (batch `20260927-a`: every Stage 1 criterion `pass`, no residuals)

## What landed

Stage 1 of the roadmap below: the agent-only acceptance area (`docs/acceptance/`, indexed by
`AGENTS.md` with the machine facts in the gitignored `AGENTS.local.md` beside it), the read-only
dependency preflight (`tools/acceptance/preflight.ps1`, first green run on this machine: 9 `present`,
1 `pending`, exit `0`), the batched run procedure with per-ticket records, the lessons pass, and the
rule-layer rewire that makes acceptance the agent's job — with the staged boundary stated everywhere,
so a row that needs the two-client session stays `blocked` instead of being passed. Verified by the
preflight runs, the gate suite (288/288), the full suite (4,475/4,475 with build) and an independent
adversarial review whose 3 major and 7 minor findings were fixed in the same change.

## Problem

`review/` was defined as "waiting for the final unified acceptance pass", and that pass was the user's
manual work: deploy, launch two clients, watch the reported behaviour, judge it. The repository
therefore carries ~140 code-complete tickets that only a person could close, and the recurring class of
adapter-shell behaviour stayed "code-reviewed only" until a person ran it.

The user's 2026-09-27 decision moves that step to the agent. The agent judges every acceptance row from
the evidence its run collects, writes an acceptance record and moves the ticket; the user is asked only
when the machine is missing a dependency, or when a row is genuinely subjective. The capability lands in
stages (below): today the dependency preflight and the procedure are executable, and a row that needs a
staged step stays `blocked` rather than being passed — the rule layer says so explicitly, so no ticket
is closed on a capability the tree does not have yet.

## Stage 1 — foundation (this ticket)

- `docs/acceptance/` — an agent-only area that declares itself with `AGENTS.md` (there is no
  `README.md`): `workflow.md` (the batched end-to-end run and the record format), `dependencies.md`
  (dependency table, detection, degradation ladder, the ask protocol), `lessons.md` (the run's own
  feedback record) and the binding rules — batching and the lessons pass are binding, not advice.
- `docs/acceptance/AGENTS.local.md` — the machine values the run reads: gitignored, auto-loaded only
  while working in that area, so the root instruction file keeps its budget.
- `tools/acceptance/preflight.ps1` — the read-only dependency preflight; `FACT-MISSING` for an absent
  machine fact, exit code 2 when a required capability is absent.
- The rule layer follows: `AGENTS.md` (development-period verification, Definition of Done),
  `docs/AGENTS.md` (§1 area list, §5 truth), `docs/backlog/README.md` (review semantics),
  `docs/evidence/delivery-checklist.md` (release-cycle line), `tools/AGENTS.md`, and the human pages
  that still said the acceptance run is the user's.
- `AgentInstructionBudgetGateTests` now measures what the loader charges — the always-loaded root pair
  against the 65,536-byte budget, and every other instruction file (tracked or area-local) against its
  own 5,120-byte ceiling — and asserts that the new area files are discovered. An area-local
  `AGENTS.local.md` relieves the root file without hiding the cost.

## Stage 2 — run harness

Launch the host client through Steam, start the sandboxed guest after the deploy, apply the shadow rule,
capture frames and recordings into the artifact directory, collect the three log channels, and stop both
clients cleanly.

## Stage 3 — acceptance engine

Turn a ticket's acceptance matrix into a run plan (row classification, scenario setup), drive the
scenario through the native UI and HotRepl probes, collect per-row evidence, and write the record that
moves the ticket.

## Stage 4 — backlog sweep

Run acceptance over the tickets in `review/` in priority order, moving what passes to `done/` and
returning what fails to `todo/` with `- Status: Rejected`.

## Stage 1 acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | `tools/acceptance/preflight.ps1` runs on this machine | One row per dependency with a state, a summary line, and an exit code that matches the missing set |
| 2 | A required machine fact is absent (point `-FactsPath` at a file without the section) | Every key reports `FACT-MISSING`, exit code 2, and the run asks per `dependencies.md` instead of guessing the value |
| 3 | The preflight runs twice | Same report; nothing on the machine written, started or stopped |
| 4 | The changed files are scanned for machine paths | No absolute path in `docs/acceptance/`, `tools/acceptance/` or the rewired rule pages |
| 5 | The instruction budget is measured | `AgentInstructionBudgetGateTests` green: the always-loaded root pair inside 65,536 bytes, every nested instruction file inside 5,120, both new area files asserted as discovered |
| 6 | The dependency table is compared with the script | Every id in `dependencies.md` has a check in `preflight.ps1`, and every check has an id — including what each check really decides |
| 7 | The area's rules are read end to end | Batching and the lessons pass are binding in `AGENTS.md` and carried by `workflow.md` §1/§10; `lessons.md` exists and already holds this cycle's lessons |
| 8 | `sandboxie-exe` is unset while `SbieSvc` runs | The program is derived from the service image path and the row is `present` — the false `missing` the review found is gone |

## Evidence

- Preflight on this machine (2026-09-27): `present` for dotnet, game, deploy, steam, hotrepl, capture,
  logs, artifacts; `pending` for the staged input helper; `missing` rows only for facts the local file
  did not carry — which is what drove the derivation fix above.
- Gate suite: `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests/...csproj` green.
- Independent adversarial review (fresh context, frozen tree, read-only; full report in `%TEMP%`):
  0 blockers, 3 major, 7 minor — all fixed in this same cycle, including the false `missing` above and
  the detection claims in `dependencies.md`.

## Limits

- Stage 1 does not run the game yet: the session, capture and probe steps are staged, and
  `workflow.md`'s capability status table says so.
- The preflight can prove a dependency is absent, not that a present one works: Steam installed but
  logged out is settled by the first launch attempt.
- Instruction-budget measurement (2026-09-27, `os.path.getsize` on the working tree): the always-loaded
  pair is 58 973 bytes (`AGENTS.md` 18 746 + `AGENTS.local.md` 40 227) of the 65 536-byte budget, leaving
  6 563 bytes — less than the 19 456-byte user-level instruction file, so the loader still drops that
  file; restoring it needs 12 893 bytes of cuts from the root pair. A separate decision, recorded in
  `docs/acceptance/lessons.md`.
