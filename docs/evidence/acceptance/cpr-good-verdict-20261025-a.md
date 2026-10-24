# CPR: the `Good` rhythm verdict, read on the machine

- Ticket: `docs/backlog/done/remote-medical-cpr.md` (moves to `done/` with this record)
- Batch: `20261025-a` — one live two-client session, no relaunch, no deploy
- Artifact: `0.1.0+c08c99ad…` (both clients; the plugin's own load line is the identity, commit `c08c99ad`)
- Dependencies used: the in-process driver, the `cpr-press` recipe, the new `cpr-rhythm` declaration + recipe

## Why this row had never been read

The ticket's own residual named it exactly: the control measures the gap between presses from REAL game time, one driver
call is ≥ 1.0 s apart (verdict `TooSlow`) and same-frame presses measured `0.01 s` (verdict `TooFast`), so nothing the
driver could produce landed inside the band at all.

## What changed (acceptance-side only)

- `tools/acceptance/driver/eval-declarations/cpr-rhythm.cs` — a declaration owning a coroutine that invokes the same
  `onClick` a player's click performs, once per interval **on successive frames**, so the game's own clock spaces them.
- `tools/acceptance/recipes/cpr-rhythm.cs` — the starter that begins it and returns at once.
- `tools/acceptance/recipes/cpr-press.cs` — an `interval` argument (kept; a real gap is also the honest shape for a press).

## The reading

Operator: the guest viewing the host's medical panel, host body in arrest (`heartRate=10`). First run at intelligence `7`
showed `verdict None` for 0.55–0.57 s gaps — which is the intelligence screen working as specified
(`ShowsTheVerdict` hides `Good` below `SmartIntRequirement = 12`), not a rhythm miss. With the operator at intelligence
`15` the same coroutine produced:

```
[Cpr] compression 0.55s sent to operation 1; verdict Good at INT 15.
[Cpr] compression 0.56s sent to operation 1; verdict Good at INT 15.
[Cpr] compression 0.56s sent to operation 1; verdict Good at INT 15.
[Cpr] compression 0.56s sent to operation 1; verdict Good at INT 15.
[Cpr] compression 0.55s sent to operation 1; verdict Good at INT 15.
```

Five consecutive presses, every gap inside the reference band (65–220/min), every verdict `Good`, and the affirmation
visible at INT ≥ 12. The row passes.

## What this also settled

`verdict None` at INT 7 is not a miss: it is the low-intelligence presentation the ticket's stage 4 specifies (the
`Good` affirmation is suppressed below `SmartIntRequirement`). The row's two halves are therefore both read: the verdict
is produced inside the band, and its visibility follows intelligence.
