# Step 13 — run an acceptance batch on the real game

Read this file and `13-acceptance.local.md` completely before starting a batch.

## One session, many rows

Start the clients once and drive every row the batch set out to judge. A batch that judged no row is a failed batch: the
missing capability is built first, then a second batch runs. Two batches on the same row without a reading means the
problem is in the code, not in the batch.

## Do not restart for convenience

The clients stay up for the whole work item. They are stopped for exactly two reasons: a source change, or a deploy. A
declaration or recipe change is re-declared under a new name and the same session continues.

## Every row judged from its own evidence

Machine rows from a reading, visual rows from a frame that was actually opened. The instrument is named in the batch
scope before it runs, so a row cannot be quietly judged by a weaker check afterwards.

## A blocked row never becomes the whole cycle

At most two batches per row, then the next ticket. The block is recorded with its reading, not as a story.
## Do not grow the harness on your own

Never add a cursor or input API to the acceptance tooling on your own initiative: in-process calls first, and OS-level
input only for one batch with a fresh authorisation from the user.