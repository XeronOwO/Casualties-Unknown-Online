# Acceptance — a refused intent reaches the actor, and an accepted one is silent

- Ticket: `docs/backlog/done/intent-refusal-never-reaches-the-actor.md`
- Artifact: `0.1.0+1ae6b691`
- Batch: `.acceptance/20261024-c/` (local, gitignored; run `ir`)
- Date: 2026-10-24
- Result: **all four rows pass** — row 1 and row 4 were the two remaining readings and both ran this batch.

## What was run

The host created a Steam lobby and one member joined; the host started the run. The host set
`/gamerule remote_backpack_access deny` (the rule read back as `deny`), the member drove the production
remote-backpack open against the host, and the run was repeated after `/gamerule remote_backpack_access
allow`.

## Rows

| Row | Observed |
|---|---|
| 1 — a gesture refused on the ACTOR's side | the member's own log: `[BackpackView] refused open for <host>: host rule RemoteBackpackAccess=deny.` plus the player-facing notice `[BackpackView] Host rule: opening another player's backpack is not allowed.`; the view did not open; the HOST's log has **zero** `BackpackView` lines — the refusal never left the client |
| 2 — a refusal on the HOST's side | read in the ticket's 2026-10-18 run: the giver's own client printed `[Warning] [InventoryIntentRefusedHandler] [RemoteIntent] the host refused 16 on item <id>: <receiver> carries no item <container>`; not re-driven this batch |
| 3 — the host's refusal of a malformed request | the intent service's invalid-operand refusal is answered to the actor (`ARefusedIntent_IsAnsweredToTheClientThatPerformedIt`, whose red was observed by sending the answer under a wrong discriminator) and the host keeps its own log line; the request was not re-forged this batch (a forged request needs a modified sender) |
| 4 — an ACCEPTED gesture | with the rule `allow` the open was accepted (`viewOpen: true`), the log says `[BackpackView] opened native backpack view for <host> (acceptance) — operable.`, and no refusal line exists anywhere; the host's log again has no `BackpackView` line |

## Limits

- Row 2 is the ticket's own earlier machine reading, and row 3's actor line is the service test's; neither was
  re-driven in this batch (row 3 would need a forged intent from a modified sender). Both are named here
  rather than hidden.
- The member's line appears in the member's own sandbox log, not the host's — the two clients do not share a
  log, the exact reading error the ticket records.
- No OS-level mouse or keyboard input was used.
