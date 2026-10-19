# Acceptance record — Give an item to another player (row 4 re-read after the loss fix)

- Ticket: `give-item-to-another-player` — verdict: **moved to `done/`** (`- Status: Done`); row 4 re-read on
  the fixed artifact, and it passes: the receiver's own container rule refuses the handed-over item, one
  observable line is written, and the item is ALIVE in a slot afterwards instead of going terminal.
- Batch: `20261022-a` — ticket `give-item-to-another-player` (row 4 only: the one row its rejection owed)
- Commit: `0632310a` · Deployed artifact: `CasualtiesUnknownOnline.dll`
  `0.1.0+0632310af74c682d7b543af872fa66e340d824a5` — `tools/verify-deploy.ps1` exit 0 before the clients
  started, and both clients were started after the deploy
- Run: 2026-10-19 21:01 → 21:08 by the session's clock · Host: physical machine · Guest: sandbox
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `logs`, `artifacts` —
  preflight `RESULT: OK - a full two-client run is possible; the alternate third client is configured`
- Artifacts: the `g4-*` files in the directory named by `acceptance-artifacts-dir`; Steam ids are written
  `<host>`/`<member>`

This run re-read the row the batch `20261021-a` rejection failed (`docs/evidence/acceptance/give-item-to-another-player-20261021-a.md`),
on the commit that changed the placement. Rows 1 (both shapes), 2, 3 (refusal half), 5 and 6 were read in
earlier batches and are not re-judged here.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| G4 | The receiver's bag refuses the item by weight, on the RECEIVER's own client, with the native feedback and an observable line — and the item survives | machine | **pass** | The receiver's `plasticbag` (`id 1215796725699`, slot 0) refuses the member's `toolbox` (`id 9492956821`): `Restore: toolbox (id 9492956821) does not fit plasticbag(Clone) — refused by the container's own rule, not loaded.` then `[PlayerInteraction] … falling back to a slot.` then `placed toolbox (id 9492956821) in local slot 1.` (`g4-host-window.txt`). **The item is alive afterwards**: the receiver's own tree lists `toolbox id=9492956821 slot=1` at the moment of the give and again 20 s later, past an item-keyframe interval (`g4-host-tree.json`, `g4-host-tree-after.json`); the host's kernel table has `terminalCount: 0` and lists `9492956821` under `carried` for `<host>` (`g4-host-ledger.json`); the giver's tree is empty (`localCount: 0`, `g4-guest-tree-after.json`); and the receiver's log carries **no** `ItemTrace … OnItemDestroyed` for that id (`g4-host-window.txt`). The giver's own removal is refused by the host's kernel as it should be: `DestroyItemCommand actor <member> … NotAuthorized: item 9492956821 is neither world content nor the sender's own`. |

## What the run did, in order

```text
21:01:38  host client launched through Steam (physical machine), CUO loaded
21:02:30  guest client launched in the sandbox (box Steam was already running), CUO loaded
21:03:1x  host creates the lobby; guest joins; the member's session becomes active
21:04:55  host starts the run; the start gate releases and the world generates (282 world items)
21:05:18  the fixtures: host receives a plasticbag (id 1215796725699), guest receives a toolbox (id 9492956821)
21:05:29  first give (guest's emergencylight → the host's bag) lands ACCEPTED: placed inside the bag
21:06:45  the row-4 give: the guest releases its toolbox onto the host's bag (a container destination)
21:06:45  the host's own container rule refuses it, one line, and the slot fallback keeps the item
21:07:05  re-read after a keyframe interval: the toolbox is still in slot 1, the kernel keeps it non-terminal
```

The release was driven by the batch-local probe `give-item-probe.cs` (the same
`ReportLocalReleaseOntoProxy` call the drag patch makes, with the item selected by type — the committed
`give-probe` recipe skips a container ITEM, and a toolbox is one). The probe is a local artifact, not a
committed recipe; the record names it so the shape is reproducible.

## Residuals for the user

None.

## Limits

- **One fixture shape.** The weight refusal of a container item (`toolbox`, 4 g) against a `plasticbag`;
  the occupied-slot and tag-restriction shapes of row 4 were read in earlier batches and are not re-read.
  The numbers behind the refusal (3.501 g per item) were measured in batch `20261021-a` on the same item
  pair; this run read the refusal, not the numbers.
- **Two clients, no third peer.** The item's survival is judged from the two participants' trees and the
  host's kernel table.
- **The host's item row still says "carried, no slot"** for the placed item (`carried` in
  `g4-host-ledger.json`), not slot 1: the receiving client's placement does not travel back. That is the
  recorded finding of `todo/placement-outcome-never-returns.md`, not a row-4 failure — row 4 asks for the
  receiver's refusal and an observable line, and both are read.
- **The fixture ids are this session's.** `1215796725699` and `9492956821` are the ids this run's fixtures
  got (the toolbox id is the same value batch `20261021-a` saw); the claim is about the chain, not the
  numbers.
