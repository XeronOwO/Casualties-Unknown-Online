# Step 13 — run an acceptance batch on the real game

Procedure: [docs/acceptance/workflow.md](../../acceptance/workflow.md). Why: [why.md](why.md).

## One session, many rows

- Start the clients once and drive every row the batch set out to judge. A batch that judged no row failed: build the
  missing capability, then run the next batch.
- A blocked row never becomes the whole cycle: at most two batches per row, then the next ticket. Two batches without a
  reading means the problem is in the code, not in the fixture.

## The clients stay up

- A client restarts for exactly two reasons: a source change or a deploy. A declaration or recipe change is re-declared
  under a new name and the session continues.
- A stuck fixture is not a third: use the game's own state paths (reopen the view, re-enter the world, drive a clean
  client), or record the stuck state as the reading. Killing the client throws away the session the reading was about.

## Every row on its own evidence

- Machine rows from a reading; visual rows from an opened, enlarged crop, and for position, width or alignment from a
  measured number beside it.
- The instrument is named in the batch scope before it runs, so no row can be judged by a weaker check afterwards.
- A failed row is a finding, not a stopper; a row nobody can judge is a named residual, never a weaker check.

## A fixture is derived, never discovered

- Before staging a mechanic, read `docs/design/mechanics/`, `surfaces/`, `items/` and the game code they cite; write the
  gesture's input contract into the ticket before the first staged input.

## A row never invents behaviour

- A row asks whether behaviour that already exists is correct. If the only way to see it is to add something a player
  would notice — a bystander's copy of a screen, a prompt, an object — that addition is a product question for the user:
  park the row and ask before building it.
- "A third party observes" means the bystander's client does not break or desync, not that it shows a copy. A test row
  is a question about the game, never a feature request.

## The two ways to drive

- In-process is the default: a recipe runs and the game's own logic decides what the input means; extending that channel
  is ordinary tooling work.
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
