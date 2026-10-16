# A block-break log line names the lost-air-write shape on every accepted break

- Status: Todo — found by batch `20261016-c` run 2 while reading the S2b captured-rows half; what it distorts
  is not the bound but the diagnosis of which shape ran.
- Priority: Low
- Category: Diagnostics / world
- Source: `docs/evidence/acceptance/validation-and-source-checks-s2b-20261016.md` — the host's own window
  carries the line on a break whose air write DID land, beside the applier's accurate Debug line.
- Related: `todo/validation-and-source-checks.md` (the S2a/S2b stages that own this path),
  `docs/architecture/member-report-validation.md` (`NetMsg.BlockDamaged`'s row)

## What the line says, and when it runs

`BlockBreakSync.OnRemoteDamageBrokeBlock` runs on EVERY accepted break with a drop payload, right after
`AcceptBreak` — the Fresh verdict, the Repeat and the LostAirWrite alike — and logs:

    [BlockBreak] {Sender}'s remote damage broke the block at ({X},{Y}) without an air-write report — the block
    went through the game's own roll when it still stood here (see the applier's Debug line for which of the
    two shapes ran), ...

On Fresh and Repeat the air write DID arrive, and that record is exactly what accepted the report, so the
headline is false for them; only `Verdict.LostAirWrite` matches it. The run's window carries both lines for one
cell — the applier's Debug ("found the cell already air — written without a second presentation", accurate)
and this Information line above it (not).

## What it costs, and the fix direction

This is the line a reader opens when asking "which of the two shapes ran", and on the ordinary path it answers
"no air write" — a diagnosis pointing at the wrong branch, on the path every member break takes. The judgement
already knows the difference (`Verdict` is in hand where the call is made), so the fix is to carry it into the
message — or to log the lost-air-write sentence only on that verdict — rather than inferring it from the
cell's own state after the fact.
