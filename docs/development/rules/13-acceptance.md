# Step 13 — run an acceptance batch on the real game

Procedure: [docs/acceptance/workflow.md](../../acceptance/workflow.md). Why: [why.md](why.md).

## One session, many rows

- Start the clients once and drive every row the batch set out to judge; a batch that judged no row failed - build the
  capability, then run the next batch.
- At most two batches per row, then the next ticket; two batches without a reading means the code, not the fixture.

## The clients stay up

- A client restarts only for a source change or a deploy; a declaration or recipe change is re-declared and the
  session continues.
- A stuck fixture is not a third: use the game's state paths (reopen the view, re-enter the world) or record the stuck
  state as the reading; a restart throws the session away.

## Every row on its own evidence

- Machine rows from a reading; visual rows from an opened, enlarged crop plus a measured number for position, width or
  alignment.
- Name the instrument in the batch scope before it runs; a row is never judged by a weaker check afterwards.
- A failed row is a finding; a row nobody can judge is a named residual, never a weaker check.

## A fixture is derived, never discovered

- Before staging a mechanic, read `docs/design/mechanics/`, `surfaces/`, `items/` and the game code they cite; write the
  gesture's input contract into the ticket before the first staged input.
- Name the row's entry before the fixture: read the family table (`docs/design/items/README.md`) and the family's page,
  write the entry into the ticket; a row driven from a ruled-out entry is `not driven` and stays open.
- A repeat of a caught mistake is a process failure: re-read the owning page before the next staged input and land the
  rule here the same turn.

## A row never invents behaviour

- A row asks whether existing behaviour is correct. If seeing it needs something a player would notice - a bystander's
  copy, a prompt, an object - that is a product question: park the row and ask.
- "A third party observes" means the bystander's client does not break or desync, not that it shows a copy.

## The two ways to drive

- In-process is the default; extending that channel is ordinary tooling work.
- OS-level input (`tools/acceptance/send-input.ps1` and the like) needs the user's authorisation, only after in-process
  provably cannot reach the row.

## Close the clients before touching `src/`

- Every project under `src/` counts, the batch-only assembly included: a running client holds the file the next deploy
  must overwrite and the deploy refuses.

## Reading a job is the work

- `run_in_background` is for a command that outlives one call; read it back with `job_output(wait: true)` in the same
  step. A turn that ends with the job unread threw it away.

## Closing

- Quit the clients through the game, then stop what the run started. A reusable pitfall goes to
  `docs/acceptance/lessons.md`, a machine value to the local facts file, and a wrong rule to the page that owns it.
