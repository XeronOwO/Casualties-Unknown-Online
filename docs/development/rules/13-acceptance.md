# Step 13 — run an acceptance batch on the real game

The rules of a batch. The procedure — launch, probes, capture, the record's shape — is in
[docs/acceptance/workflow.md](../../acceptance/workflow.md); what measured these rules is in [why.md](why.md).

## One session, many rows

- Start the clients once and drive every row the batch set out to judge. A batch that judged no row is a failed batch:
  build the missing capability, then run the next batch.
- A blocked row never becomes the whole cycle: at most two batches per row, then the next ticket. Two batches on the
  same row without a reading means the problem is in the code, not in the fixture.

## The clients stay up

- A client is restarted for exactly two reasons: a source change, or a deploy. A declaration or recipe change is
  re-declared under a new name and the same session continues.
- A stuck fixture is not a third: use the game's own state paths (close and re-open the view, re-enter the world, drive a
  client that is still clean), or record the stuck state as the reading. Killing the client throws away the session the
  reading was about and hides the defect behind a fresh process.

## Every row on its own evidence

- Machine rows from a reading; visual rows from a crop that was opened and enlarged, and for a position, a width or an
  alignment also from a measured number beside it.
- The instrument for each row is named in the batch scope before it runs, so a row cannot be judged by a weaker check
  afterwards.
- A failed row is a finding, not a stopper; a row nobody can judge is a named residual, never a weaker check.

## A fixture is derived, never discovered

- Before staging a mechanic, read `docs/design/mechanics/`, `surfaces/` and `items/`, and the game code they cite; write
  the gesture's input contract into the ticket before the first staged input.

## The two ways to drive

- In-process is the default: the client runs a recipe and the game's own logic still decides what the input means.
  Extending that channel is ordinary tooling work and needs no permission.
- OS-level input (`tools/acceptance/send-input.ps1` and anything like it) needs the user's authorisation for the batch,
  and only after in-process provably cannot reach the row; the batch scope says why.

## Close the clients before touching `src/`

- Every project under `src/` counts, the batch-only assembly included: a running client holds the file the next deploy
  must overwrite, the deploy then refuses, and the batch is dead.

## Reading a job is the work

- `run_in_background` is for a command that outlives one call; the step that started it finishes by reading it with
  `job_output(wait: true)`. A turn that ends with the job unread threw its result away.

## Closing

- Quit the clients through the game, then stop what the run started. Fold a reusable pitfall into
  `docs/acceptance/lessons.md`, a machine value into the local facts file, and a wrong or missing rule into the page that
  owns it.
