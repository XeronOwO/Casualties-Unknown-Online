# docs/acceptance/ — agent-run acceptance

Agent-only area. It declares itself with this `AGENTS.md` (there is no `README.md` here), it is never
translated and it is not part of the human navigation in `docs/en/` or `docs/zh/`. Acceptance is a
duty of the agent, not a step handed to the user: a ticket in `review/` is code-complete and waiting
for the agent's acceptance run, and that run is what moves it.

Local machine facts — game directory, sandbox roots, endpoints, artifact directory — live in
[AGENTS.local.md](AGENTS.local.md) in this directory: gitignored, and auto-loaded only while working
here, so they cost nothing in the root instruction budget. Read that file before running anything and
never copy a machine value into a committed page: committed pages use placeholders such as
`<game-dir>`.

## Index

- [workflow.md](workflow.md) — the end-to-end run: preflight, batch plan, build and deploy, the
  two-client session, per-row verdicts, the records, the ticket transitions, and the capability status
  table that says which steps are executable today and which are still staged.
- [dependencies.md](dependencies.md) — every dependency, its detection, the capability it unlocks and
  the degradation ladder when it is missing.
- [lessons.md](lessons.md) — what the runs keep teaching; every run folds its reusable lessons back
  here, so each batch is cheaper than the last.
- [`tools/acceptance/preflight.ps1`](../../tools/acceptance/preflight.ps1) — the executable half of
  the dependency table.

## Binding rules

1. `[CRITICAL]` **Preflight first, and a missing dependency is never skipped.** Run the preflight at
   the start of every acceptance run — and check for a game or Steam process the run did not start
   before touching anything. When something is absent: say so to the user in plain language (what is
   missing, why acceptance needs it, what the options are), record it on the affected tickets as an
   external blocker, and leave every row that depends on it `blocked`. Do not substitute a weaker
   check for a missing capability, and do not call a row judged when it was not.
2. `[CRITICAL]` **Acceptance is batched, not per ticket.** One run covers every ticket waiting in
   `review/` that the available dependencies can serve: build and deploy once, launch the clients
   once, work through every ticket's scenarios in that session, and write one record per ticket
   naming its batch. Starting a whole session for a single ticket is the loop this saves.
3. `[CRITICAL]` **A row passes only on evidence from this run against the deployed artifact.** The
   evidence pointer — a log excerpt, a probe result, a captured frame — is part of the verdict. A
   test that passed earlier is not acceptance evidence; `unproven` is not `pass`.
4. `[RULE]` **Judge by reading the evidence, frames included.** A visual row is judged from a frame
   the run captured and the agent inspected, and the frame is named in the record. A judgement that
   is genuinely subjective becomes a residual for the user, never a self-issued pass.
5. `[RULE]` **The run is reproducible from the record**: the commit, the deployed artifact identity,
   the ticket's rows, one verdict and one evidence pointer per row, the dependencies used, and the
   residual list.
6. `[RULE]` **The user's machine is shared property.** Never kill a game or Steam process the run did
   not start, never delete sandbox files outside the shadow-clearing rule in `AGENTS.local.md`, and
   stop the run cleanly.
7. `[RULE]` **No machine path, no personal data in git.** Records, index rows and commits carry
   placeholders only; frames, recordings and probe dumps stay in the local artifact directory named
   by `AGENTS.local.md` and are cited by artifact id.
8. `[RULE]` **A failed row is a finding, not a stopper.** Move the ticket back to `todo/` with
   `- Status: Rejected`, name the failing row and its evidence, and fix it through the normal
   development cycle. Tickets whose rows all pass, or whose only remaining rows are residuals for the
   user, move to `done/`; the index rows move in the same change.
9. `[RULE]` **Every run leaves this area better.** Before it closes, fold what it taught: a reusable
   lesson into [lessons.md](lessons.md), a machine value or local gotcha into `AGENTS.local.md`, a
   rule the run showed to be wrong or missing into this file — and into `docs/AGENTS.md` or
   `AGENTS.md` when it binds the whole repository. A lesson left in the transcript is lost.

## Related

- Status meaning and ticket transition: [`../backlog/README.md`](../backlog/README.md)
- The development cycle that precedes acceptance: [`../../AGENTS.md`](../../AGENTS.md)
- Delivery gate: [`../evidence/delivery-checklist.md`](../evidence/delivery-checklist.md)
