# Acceptance record — A favourite mark belongs to the local player, and it is saved

- Ticket: `favourite-marks-are-local-and-saved` — verdict: **moved to `done/`** (row 1's **gesture** half,
  row 4 and row 6 were the only rows left `unproven` by batch `20261011-e`; all three now pass)
- Batch: `20261011-f` — tickets `favourite-marks-are-local-and-saved` and
  `remote-inventory-native-parity-rework`; scope and the pre-declared row readings:
  `docs/evidence/acceptance/20261011-f-scope.md`
- Commit under test: `f21056e4` (no `src/` edit since `d305ba9c`) · Deployed artifact:
  `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+d305ba9ca9e1200b7ff1dee0c6678cc6503cba44`, verified against `<game-dir>`
  (`tools/verify-deploy.ps1`, exit 0; `verify-deploy.txt`)
- Run: 2026-10-11 16:06 → 16:17 · Host: physical machine (the OWNER of the operated items) · Guest: first
  sandbox (the OPERATOR) · Third client: alternate sandbox (the third peer, in the world throughout)
- Dependencies: `preflight.txt` (11 present, "a full two-client run is possible; the alternate third client
  is configured") and `session-environment-status.txt` (`active=cuo`, `game-running=false`,
  `swap-needed=false`), both captured before the clients started
- Artifacts: the batch directory under `acceptance-artifacts-dir`; every file cited below lives there.
  Log lines quoted here carry roles (`<owner>`, `<operator>`) where the client logged a SteamId.

## How the gesture was produced

Batch `20261011-e` could not produce the pointer-and-key gesture, established why (the evaluator freezes
the game's own frame loop; a pointer outside the client window makes the frame's `GraphicRaycaster` cast
nothing at all), and its scope named two ways out. This batch took the first — the user was at the machine
— and the user then **authorised the run to drive the pointer and the key itself, for this batch only**,
choosing "this once" over a change to the standing rule that acceptance tooling never takes the physical
pointer and keyboard. That authorisation lives here and in the batch scope; nothing about it was committed
as tooling (the actuator is the batch artifact `input-tool.ps1`, and the pointer is never touched by
anything under `tools/`).

What the run staged before each press, so the press could be judged:

- the operator's client was brought to the foreground and read `focused: true` with the pointer
  `inside: true` at `(552, 699)` (`screen-info.cs`);
- the ring button carrying the owner's `paincream` proxy was brought under the pointer through the game's
  own ring geometry: `remote-gesture mode=hover cast=2` answered `castUnderPointer: 0`,
  `castOverlaps: true`, `pointerDistanceOverScale: 214.5` (below the game's own `600` close threshold);
- the pointer was placed over the operator's own item button for row 6 by moving it to that button's own
  reported position, and `mode=list` then read that button `pointerInside: true`.

| # | Row (half) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1a | the mark exists and the OPERATOR's own view shows it, while nothing about the item changes for anyone else | machine, visual | **pass** | the operator's census reads the owner's `paincream` proxy `favourited=true` (`f1-guest-census.json`) while the owner's own census reads its own item `favourited=false` (`f1-host-census.json`) and the third peer reads the same instance id `favourited=false` (`f1-alt-census.json`); the captured frame carries the native star on the item's own hover description (`f1b-guest-ring-marked.png`, also `f1-guest-ring-marked.png`) |
| 1b | the GESTURE half: a favourite gesture made while looking at the owner's item | machine | **pass** | the operator's client logged `[FavouriteMark] marked paincream in <owner>'s inventory — the mark is local to this client; the owner's own item is untouched.` at 16:13:36.350 (`f1-guest-favouritemark.txt`); it is the FIRST of only two mark lines the whole session produced (the other is row 4's adoption line at 16:15:20.277, `f6-guest-all-favouritemark.txt`); the mark was created by the press and by nothing else — the batch never called the fixture path (`favourite-marks.cs mode=toggle`); the OWNER's log holds no mark line and no `[RemoteIntent]` in the same window (`f1-host-window.txt`) |
| 2 | the same mark after the operator leaves and rejoins the same world | — | **pass, batch `20261011-e`** | `docs/evidence/acceptance/favourite-marks-are-local-and-saved-20261011-e.md` (`Character restore brought back 1 local favourite mark(s).`) |
| 3 | the same mark after a save and a load of that world | — | **pass, batch `20261011-e`** | same record (the same restore line after the cut, and the mark in the world's own `live/characters/<playerKey>.json`) |
| 4 | the marked item later coming into the operator's own possession | machine | **pass** | the owner dropped it through the game's own drop (`[ItemDropped] paincream (id 1168552085443) at (3.0,505.1)`, `f4-host-dropped.txt`) and the operator took it through the game's own pickup guards (`item-provide mode=pickup` → `instanceId 1168552085443`, `picked: true`, `slot 0`, `onBody: true`); the operator's client then logged `[FavouriteMark] item paincream (1168552085443) is the local player's own now — the local mark became its own favourite flag.` at 16:15:20.277 (`f4-guest-adoption.txt`), the item's own field reads `favourited=True` (`f4b-guest-census.json`) and the local entry is gone — the live set reads `count: 0` (`f4-marks-after-adoption.json`) |
| 5 | an item the operator never marked, which the OWNER has favourited | — | **pass, batch `20261011-e`** | same record (the owner's own value is not what the operator's view shows) |
| 6 | the favourite key over the operator's OWN inventory | machine | **pass** | with the pointer over the operator's own item button (`mode=list` reads slot 3 `pointerInside: true`) the press flipped that item's own native flag (`emergencylight`, `favourited=False` → `True`, `f6-guest-census.json`) and produced **no mark line at all**: the session's complete mark log holds exactly two lines, 16:13:36 (row 1b) and 16:15:20 (row 4), and this press fell between them at 16:14:49 (`f6-guest-all-favouritemark.txt`) |
| 7a | the marked item changes hands | — | **pass, batch `20261011-e`** | the same record (`mark on item … followed it from … to …`, the owner refreshed, the third peer's view unchanged) |
| 7b | a mark whose id carries another definition | — | **pass, batch `20261011-e`** | the same record (`dropped a stale mark: mark on item … names a bandage, but the id now carries a paincream.`) |

## Residuals for the user

None. Every row of the ticket now carries a verdict from an agent-run batch.

## Limits

- **The press was the run's own actuator, not the user's hand.** The scope planned the user's hand and the
  user then authorised the actuator instead; the game's own input path is what received it either way (the
  store is `Input.GetKeyDown(favourite)` inside the frame the client ran itself), but the record says
  which hand produced it rather than leaving it to be assumed. The standing rule is unchanged, so the next
  batch that needs this gesture starts again from the scope's two options.
- **Row 4's value needs a second read.** The census taken in the same call as the pickup read
  `favourited=False` (`f4-guest-census.json`) and the re-read eight seconds later read `True`
  (`f4b-guest-census.json`): the adoption rides the character-data path, so a row that reads the item in
  the same call as the transfer can read the pre-adoption value. The verdict rests on the re-read and on
  the adoption's own log line.
- **The pickup path this time was drop-then-pickup, and it worked.** Batch `20261011-e` could not move the
  item because that operator's body had limbs at zero force (`InventorySlot.canPickUp` refuses every
  slot); this run checked `canPickUp` first (all six slots `true`) and moved the item through the game's
  own drop and pickup.
- **`item-world-read` did not see the dropped item on the operator's client.** With the pointer position
  used as the cell (`x=0,y=507,radius=30`) it answered `count: 0` while the operator's own log shows the
  item materialized (`[ItemSpawn] materializing paincream (id 1168552085443) at (3.0,505.1)`) and the
  pickup then found it; the reading is recorded as a tool observation, not as a product finding.
- **The visual halves rest on one frame per row.** The star is read from the item's own hover description
  in `f1b-guest-ring-marked.png` and from the live rendering state (the display proxy's `favourited`, the
  one field `InvButton.UpdateGraphic` tints with) in the censuses; the ring's own buttons are partly
  covered by the hover panel in the frame.
