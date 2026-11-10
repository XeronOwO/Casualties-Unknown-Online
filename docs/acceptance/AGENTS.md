# docs/acceptance/ — agent-run acceptance

Agent-only area (no `README.md`, never translated, outside the human navigation). Acceptance is the agent's duty, not a step handed
to the user: a ticket in `review/` is code-complete and waits for this run, and the run moves it. Machine facts live in the
gitignored [AGENTS.local.md](AGENTS.local.md): read it first, and never copy a machine value into a committed page.

## Index

- [workflow.md](workflow.md) — the end-to-end run and the capability table.
- [dependencies.md](dependencies.md) — every dependency, its detection, and the degradation ladder.
- [lessons.md](lessons.md) — what the runs keep teaching.
- [`tools/acceptance/preflight.ps1`](../../tools/acceptance/preflight.ps1) — the executable half of the dependency table.
- [`tools/acceptance/drive-in-process.ps1`](../../tools/acceptance/drive-in-process.ps1) — the in-process driver of a running
  client (`-ListActions` names its vocabulary) with its recipes under [`tools/acceptance/recipes/`](../../tools/acceptance/recipes/).
- [`tools/acceptance/session-environment.ps1`](../../tools/acceptance/session-environment.ps1) — the BepInEx-tree gate.

## Binding rules

1. `[CRITICAL]` **Preflight first; a missing dependency is never skipped.** Check for a game/Steam process the run did not start.
   When something is absent: say so, record it on the affected tickets as an external blocker, leave dependent rows `blocked`.
   Never substitute a weaker check, never call an unjudged row judged.
2. `[CRITICAL]` **Acceptance is batched, not per ticket**: one run covers every `review/` ticket the dependencies can serve —
   build and deploy once, launch once, work every ticket's scenarios, write one record per ticket naming its batch.
3. `[CRITICAL]` **A row passes only on evidence from THIS run against the deployed artifact**: the evidence pointer (log excerpt,
   probe result, captured frame) is part of the verdict. An earlier green test is not evidence; `unproven` is not `pass`.
4. `[RULE]` **Judge by reading the evidence, frames included**: a visual row is judged from a captured frame the agent inspected
   and named in the record, captured per client window (never the desktop). A genuinely subjective judgement is a residual for the
   user, never a self-issued pass.
5. `[RULE]` **The run is reproducible from the record**: commit, deployed artifact identity, rows, one verdict and one evidence
   pointer per row, dependencies used, residuals.
6. `[RULE]` **The user's machine is shared property**: never kill a process the run did not start, never delete sandbox files
   outside the shadow rule in `AGENTS.local.md`, stop cleanly.
7. `[RULE]` **No machine path, no personal data in git**: placeholders only; frames and dumps stay in the local artifact directory
   and are cited by artifact id.
8. `[RULE]` **A failed row is a finding, not a stopper**: move the ticket back to `todo/` with `- Status: Todo — Rejected (…)`,
   naming the failing row and its evidence; tickets whose rows all pass move to `done/`. Index rows move in the same change.
9. `[RULE]` **Every run leaves this area better**: fold a reusable lesson into [lessons.md](lessons.md), a machine value into
   `AGENTS.local.md`, a wrong or missing rule into this file — and into `docs/AGENTS.md` or the root `AGENTS.md` when it binds the
   repository. Closing an acceptance conversation runs this harvest unprompted.

## Related

[`../backlog/README.md`](../backlog/README.md) — status meaning and transitions · [`../../AGENTS.md`](../../AGENTS.md) — the cycle
before acceptance · [`../evidence/delivery-checklist.md`](../evidence/delivery-checklist.md) — the delivery gate
