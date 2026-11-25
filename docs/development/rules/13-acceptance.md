# Step 13 — run an acceptance batch on the real game

Procedure: [docs/acceptance/workflow.md](../../acceptance/workflow.md). Why: [why.md](why.md).

## One session, many rows

- Start the clients once and judge every row the batch set out to; a batch that judged no row failed — build the
  capability first.
- At most two batches per row, then the next ticket; two batches without a reading means the code, not the fixture.
- For a diagnosis, run one case at a time; the batch is for rows that are already individually ready — not how a case
  that is not yet understood gets its evidence.
- A batch is the last reading, never a status check: while any row is known red, that row is fixed on its own single-case
  run, and the full batch runs only when every row is green by itself. A batch started to see where the work stands
  spends both clients on a reading the failing rows already gave (2026-11-25: a batch was started with a dozen rows
  still red and had to be stopped; each of those rows had already named itself in the previous batch).

## The clients stay up

- A client restarts only for a source change or a deploy; a declaration or recipe change is re-declared and the
  session continues.
- A stuck fixture is not a third: use the game's state paths (reopen the view, re-enter the world) or record it as the
  reading; a restart throws the session away.

## Every row on its own evidence

- Machine rows from a reading; visual rows from an enlarged crop plus a measured number for position, width or
  alignment.
- Name the instrument in the batch scope before it runs; a row is never judged by a weaker check afterwards. A failed row
  is a finding; an unjudgeable row is a named residual, never a weaker check.

## A fixture is derived, never discovered

- Before staging a mechanic, read `docs/design/mechanics/`, `surfaces/`, `items/` and the code they cite; write the
  gesture's input contract into the ticket first.
- Name the row's entry before the fixture: read the family table (`docs/design/items/README.md`) and the family's page,
  write the entry into the ticket; a row driven from a ruled-out entry is `not driven` and stays open.
- Name the object by the game's own prefab id, never the class or a guess: `Openable` is not a door
  (2026-11-07).

## A row never invents behaviour

- A row asks whether existing behaviour is correct. If seeing it needs something a player would notice - a bystander's
  copy, a prompt, an object - that is a product question: park the row and ask.
- "A third party observes" means the bystander's client does not break or desync, not that it shows a copy.

## Two ways to drive

- In-process is the default; extending that channel is ordinary tooling work.
- OS-level input (`AcceptanceHost input …`) needs the user's authorisation, only after in-process provably cannot
  reach the row.

## Close the clients before touching `src/`

- Every project under `src/` counts, the batch-only assembly included: a running client holds the file the next deploy
  overwrites.

## Reading a job is the work

- `run_in_background` is for a command that outlives one call; read it back with `job_output(wait: true)` in the same
  step (step 0's job rule). An overrun is a defect, not a wait: read the clients, the report and both logs.

## Closing

- Quit the clients through the game, then stop what the run started. A reusable pitfall goes to
  `docs/acceptance/lessons.md`, a machine value to the local facts file, a wrong rule to the owning page.
