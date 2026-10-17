# Acceptance record — the item-data umbrella's two landed sections (cursor, host registration)

- Ticket: `item-and-entity-data-commands` — verdict: **stays in `todo/`** (stage D, `spawn`, is still
  unbuilt, so the ticket does not move whatever these rows say). The host-registration section's rows 1-5
  pass; the cursor section's rows 1-5 are `unproven` for a named dependency.
- Batch: `20261017-q` — tickets `give-item-to-a-player-command`, `cheat-permission-and-op-commands`.
- Commit: `bb2350e7` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+bb2350e760208d6e3cce3effd5917caef141bae4` (verified by `tools/verify-deploy.ps1`, exit 0).
- Run: 2026-10-17 21:46:18 → 21:52:14 · Host: physical machine · Member: `Steam1` sandbox.
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input`, `logs`,
  `artifacts`.
- Artifacts: the `c*` and `b*` readings, `r2-host-log.txt` in the directory named by
  `acceptance-artifacts-dir`. Steam IDs are written `<host>`/`<member>` below.

## The host's own carried inventory is registered (the section's rows 1-5)

| # | Row (the ticket's own) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The host runs `/data get` and its starting supplies and worn items are listed with instance ids | machine | **pass** | `c1-host-data.json`: `Items recorded for <host> (1): #1142782281667 emergencylight [slot 3] condition 1`; the host's own log line `r2-host-log.txt`: `[Items] the host's own carried inventory is registered: 1 item(s) with ids and rows at world entry.` — the record that used to be empty for a host who had not used anything yet |
| 2 | The host runs `/data set id <one of them> condition 0.5` WITHOUT having used, moved or dropped that item: it lands and both peers read it back | machine | **pass** | `c2-host-set.json`: `#1142782281667 emergencylight: condition 1 -> 0.5 — written and reported as this client's own item state.`; `c3-host-read.json` and `c4-guest-read.json` both: `#1142782281667 emergencylight [carried by <host>, slot 3] revision 2 / condition 0.5` |
| 3 | The member runs `/data get id <the same id>` and reads the host's row | machine | **pass** | `c4-guest-read.json` (above) — the member's replay names the host as the owner and shows revision 2 |
| 4 | The member's own supplies still register the way they did (a regression row) | machine | **pass** | `c5-guest-data.json`: `Items recorded for <member> (1): #5197989525 emergencylight [slot 3] condition 1`; the host's log: `Registered 1/1 carried items of <member> in the transfer table (0 already registered, 0 refused by the kernel).` |
| 5 | The host takes the next-level transition: after the layer is entered, `/data get` lists the host's restored carried items with ids | machine | **pass**, with the substitution named below | `c7-skiplayer.json`: the game's own `skiplayer` command applied; the host log then carries `[LayerReset] dropped the previous layer's world-rooted items; 2 item record(s) remain (carried items cr…)`, `Committed kernel layer advance (run 1, layer 1).` and a SECOND `[Items] the host's own carried inventory is registered: 1 item(s) with ids and rows at world entry.`; `c8-host-data-after-layer.json` still lists `#1142782281667 emergencylight [slot 3] condition 0.5` — not an empty set |

**The substitution row 5 carries.** The transition was the game's own `skiplayer` debug command — a plain
layer change, not a death and auto-revive. The observable the row asks for (the listing after the layer, with
ids, rather than an empty record) is read on both the console and the log; the restore-HOLD branch the row's
sentence names (the registration held until a queued character restore lands) is not exercised by a plain
layer change, and this record does not claim it. The ticket's own Limits describe the difference.

## The cursor target (the section's rows 1-5)

All five rows are `unproven` for one dependency, read before any staging: **the pointer is outside the host
window's client area**. `b1-pointer-read.json` (an in-process probe, read-only):

```json
{"ok":true,"mouseX":1265,"mouseY":-658,"screenW":1280,"screenH":720,"inside":false,"under":"none"}
```

The game's own pickup query would run at the world point under that position, which is not a point of the
host's screen at all; an item staged there would not be "the item under the pointer" the row means. The run
did not move the pointer (the driver bans OS-level input and the batch did not ask for a one-off cursor
authorization) and did not substitute a weaker check. The ticket's own note covers exactly this case: "a
pointer left outside the client window makes the rows unstageable, and then the batch names that dependency
rather than replacing the check."

| # | Row (the ticket's own) | Class | Verdict | Dependency |
|---|---|---|---|---|
| 1 | The host drops an item, points at it and runs `/data get cursor` — the row of the item under the pointer | machine | **unproven** | the pointer is outside the host window (`b1-pointer-read.json`) |
| 2 | `/data set cursor …` on that world item, read back on both peers | machine | **unproven** | same |
| 3 | An id the pointer names but the record has no row for — the named refusal and nothing written | machine | **unproven** | same |
| 4 | The pointer over ground, a body or a building — "nothing is under the pointer", nothing written | machine | **unproven** | same (the pointer is over no part of the window, so it is over nothing the row means) |
| 5 | The id-less row (a guest's just-dropped item) — the named refusal | machine | **unproven** | same, and the window itself would need to be held |

## What the run adds beyond the rows

- **The host's registration is a world-entry fact, and the layer change re-registers it**: two
  `the host's own carried inventory is registered` lines, one at entry and one after the `skiplayer`
  transition, with the same single item and its id preserved (`c10-host-registration-log.log`).
- **The member's replay receives the host's row and keeps it**: the member's `/data get id` answers the
  host's row with the host as owner (`c4`), and its own row stays its own (`c5`).
- **The write survives the layer change on the record**: after the transition the item still reads
  `condition 0.5` (`c8`), so the row the console wrote at entry is the row the new layer carries.

## Residuals for the user

None from this record. The cursor rows are `unproven` rather than residual: they need the machine's pointer
over the host window, which is a dependency and not a taste judgement.

## Limits

- **The pointer dependency is the whole cursor section's limit** — five rows, one cause. A run with the
  pointer inside the host window (or a one-off cursor authorization) is what would judge them.
- **Layer row 5's restore-hold branch is not exercised** (above); the plain layer change is what the run
  drove.
- **One member, no third client**: the member's replay is read on the member itself; a second guest's view of
  the same ids is not claimed.
- **The run did not re-read the registration across a reconnect or a mid-run restore**; the ticket's
  persistence questions stay where its own ticket puts them.
