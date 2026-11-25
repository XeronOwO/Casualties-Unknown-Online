# Step 13 — run an acceptance batch on the real game

Procedure: [docs/acceptance/workflow.md](../../acceptance/workflow.md). Why: [why.md](why.md).

## One session, many rows

- Start the clients once and judge every row the batch set out to; a batch that judged no row failed — build the
  capability first.
- **Suite wall budget: 480 s**, read from the suite's own report. A run whose measured cost would exceed it is replaced
  by slices of six to ten rows; an overrun is a batch defect, fixed by removing the cost the measurement names — never
  by skipping rows or calling a red row green.
- At most two batches per row, then the next ticket; two without a reading means the code, not the fixture.
- For a diagnosis, run one case at a time; the batch is for rows already ready.
- A batch is the last reading, never a status check: a red row is fixed on its own single-case run, and the full batch
  runs only when every row is green by itself.

## The clients stay up

- A client restarts only for a source change or a deploy; a declaration or recipe change is re-declared in the same
  session.
- A stuck fixture is not a third: use the game's state paths (reopen the view, re-enter the world) or record it as the
  reading.

## Every row on its own evidence

- Machine rows from a reading; visual rows from an enlarged crop plus a measured number.
- Name the instrument in the batch scope before it runs; a failed row is a finding, an unjudgeable one a named residual.

## A fixture is derived, never discovered

- Before staging a mechanic, read `docs/design/mechanics/`, `surfaces/`, `items/` and the code they cite; the gesture's
  input contract goes into the ticket first.
- Name the row's entry before the fixture, from the family table (`docs/design/items/README.md`) and the family's page,
  into the ticket; a row driven from a ruled-out entry is `not driven`.
- Name the object by the game's own prefab id, never the class or a guess: `Openable` is not a door.

## A row never invents behaviour

- A row asks whether existing behaviour is correct. If it needs something a player would notice, that is a product
  question: park it and ask.
- "A third party observes" means the bystander's client does not break or desync, not that it shows a copy.

## Two ways to drive

- In-process is the default, and extending that channel is ordinary tooling work; OS-level input (`AcceptanceHost
  input …`) needs the user's authorisation, after in-process cannot reach the row.

## Close the clients before touching `src/`

- Every project under `src/` counts, the batch-only assembly included: a running client holds the file the deploy
  overwrites.

## Reading a job is the work

- `run_in_background` is for a command that outlives one call; read it back with `job_output(wait: true)` in the same
  step. An overrun is a defect: read the clients, the report and both logs.

## Closing

- Quit the clients through the game, then stop what the run started. A reusable pitfall goes to
  `docs/acceptance/lessons.md`, a machine fact to the local file.
