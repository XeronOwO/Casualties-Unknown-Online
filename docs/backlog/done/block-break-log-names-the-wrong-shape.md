# A block-break log line names the lost-air-write shape on every accepted break

- Status: Done — read on the machine in batch `20261017-d` (`docs/evidence/acceptance/block-break-log-shape-20261017-d.md`): the headline names the `Fresh` shape and agrees with the applier's line. Found by batch `20261016-c` run 2 while reading the S2b captured-rows half; what it distorts
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
## What landed (2026-10-17)

The headline now carries the JUDGEMENT instead of guessing it from the cell's state afterwards:
`LostAirWrite` keeps the lost-air-write reading, `Fresh` says the air write is what accepted the report,
`Repeat` says it is the cell's earlier accepted claim, and the damage-only edge is worded for what it can
actually establish — that the report carried no drop payload, so a plain hit and a break whose roll was
empty are indistinguishable there. The method's own doc no longer calls a drops-carrying report "the case
where the air-write message was lost with it", and the two sibling `[BuildingSupport]` lines on the same path
stopped naming one write from seven call sites and a judging purpose on clients that never judge. `AcceptBreak`'s
own verdict wording gained an explicit `Repeat` arm and a neutral catch-all so the same defect has no copy
left in this file.

**UNVERIFIED on the machine**: no run has seen the new sentence, which is why this ticket is in `review/`.
The red is the recorded window above; the fix's own reading is the next batch's break step.
**The next edit splits it**: `BlockBreakSync.cs` is 594 lines after this change — six under the architecture
gate — so the next change that is not a wording tweak splits the class first instead of tripping the gate.