# Architecture Evolution Session Workflow

This file defines how an independent phase session starts, works, and ends so any
later session can pick up with minimal context loss.

## Why sessions must be continuable

The user will run each architecture phase in its own fresh session. A fresh session
does not inherit the previous session's working memory. The repository documentation
is therefore the only durable channel. Every phase must end with enough written state
for the next session to:

- know exactly which phase is active and what was already done;
- know which phase exit criteria are already satisfied and which are not;
- avoid redoing lost work or starting from stale assumptions;
- have a concrete next-action list.

## Session start checklist

1. Read this file.
2. Read [status.md](status.md). Identify `Current phase` and its status.
3. Read the active phase doc under `docs/architecture-evolution/`.
4. Read [current-architecture.md](current-architecture.md) for the current model.
5. Read the active phase doc's `Session log` and `Next actions` sections.
6. Check the repository status: `git status`, `git log --oneline -5`.
7. Read any relevant existing docs:
   - `docs/tech-decisions.md` for landed binding decisions;
   - `docs/selfchecks/` for recent delivery evidence;
   - `docs/backlog.md` for open work;
   - domain feature docs (`item-features.md`, `entity-features.md`) when the phase touches them.
8. If the phase is marked `In progress`, continue from the open checklist; do not restart it.
9. If the phase is marked `Blocked`, read the blocker and either resolve it or update the blocker note.

## During the phase

- Follow the repo delivery gate in `AGENTS.md`: understand -> mechanism inventory ->
  adversarial self-check -> plan + self-check table -> user approval (if required) ->
  implement -> build/format/gates -> deploy/verify -> structure review -> commit.
- Record every binding decision in `docs/tech-decisions.md` with commit evidence and
  `file:line` where possible.
- For every delivery cycle, write or update a fact sheet under `docs/selfchecks/`.
  One phase may contain multiple delivery cycles; each cycle gets its own fact sheet.
- Keep this area's phase doc synchronized as work progresses:
  - tick completed checkboxes;
  - update `Session log`;
  - note unresolved risks/open questions;
  - never mark a phase complete before its exit criteria are verifiable.
- Do not quietly add compatibility layers. If a phase needs to keep legacy code alive,
  record the reason, the owner, and the planned deletion phase in the phase doc.

## Session end / phase completion checklist

When a session is ending, perform these updates in the repository:

### Required for every working session

1. Update the phase doc:
   - append a `Session log` row with date, scope, commits, and verification results;
   - update `Next actions` with the exact next step;
   - mark any completed work items.
2. Update [status.md](status.md):
   - set `Last status update`;
   - if a phase transition happens, update `Current phase` and the phase table;
   - append a row to `Phase completion log` only when the phase is actually complete.

### Required when a phase is complete

1. Move the phase's status in [status.md](status.md) to `Completed`.
2. Create/update the phase fact sheet under `docs/selfchecks/` with:
   - mechanism inventory;
   - evidence table;
   - verification results;
   - structure review.
3. Add all binding decisions to `docs/tech-decisions.md`.
4. Update `docs/backlog.md`:
   - remove completed items;
   - add any new architectural debt discovered;
   - link the evolution area if the open-work view changed.
5. Update `docs/architecture-evolution/README.md` if the directory map or phase table changed.
6. If the implemented architecture supersedes an existing blueprint section, update
   `docs/architecture.md` in the same phase so there is never a stale design
   reference.
7. Update `AGENTS.md` only when the phase changes engineering conventions, current phase,
   or mandatory gates. Do not update it for minor progress.

### Required before starting the next phase

- Read the next phase's `Prerequisites` section.
- Confirm the previous phase's exit criteria have evidence in the repo.
- If the previous phase left an unresolved exception, list it in the next phase's
  `Preconditions`/`Risks` before starting.

## Handoff message template

A completed working session should leave a short handoff notice in the phase doc's
`Session log` containing:

```text
Date:
Phase:
Scope:
Commits / artifacts:
Verification:
Open items:
Next actions:
```

## Normalization rules

- All committed docs in this area are English.
- Never commit machine-local paths or personal environment details.
- Keep docs in the active voice and evidence-first.
- A phase is not "done" because its code compiles; it is done when the phase's exit
  criteria are demonstrated by tests, gates, and written evidence.
