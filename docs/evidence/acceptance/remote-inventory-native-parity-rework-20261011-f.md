# Acceptance record — Remote inventory operations: run the native path end to end

- Ticket: `remote-inventory-native-parity-rework` — verdict: **moved to `done/`** (matrix row 7's
  **favourite half** was the only row the ticket still carried as `unproven`; it passes here against the
  criterion decision 258 landed, and every other row already carries a verdict from its own batch)
- Batch: `20261011-f` — tickets `favourite-marks-are-local-and-saved` and
  `remote-inventory-native-parity-rework`; scope and the pre-declared row readings:
  `docs/evidence/acceptance/20261011-f-scope.md`. The gesture this row needs is the same press the sibling
  record judges: `docs/evidence/acceptance/favourite-marks-are-local-and-saved-20261011-f.md` carries the
  full account of how it was produced (the user's on-the-spot authorisation for this batch only, the ring
  button brought under the pointer, the geometry read before the press).
- Commit under test: `f21056e4` (no `src/` edit since `d305ba9c`) · Deployed artifact:
  `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+d305ba9ca9e1200b7ff1dee0c6678cc6503cba44`, verified against `<game-dir>`
  (`tools/verify-deploy.ps1`, exit 0; `verify-deploy.txt`)
- Run: 2026-10-11 16:06 → 16:17 · Host: physical machine (the OWNER of the operated items) · Guest: first
  sandbox (the OPERATOR) · Third client: alternate sandbox (the third peer, in the world throughout)
- Dependencies: `preflight.txt` (11 present) and `session-environment-status.txt` (`active=cuo`,
  `game-running=false`, `swap-needed=false`), both captured before the clients started
- Artifacts: the batch directory under `acceptance-artifacts-dir`; the files cited for row 7 live there,
  the earlier rows' artifacts in their own batch directories.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest opens the host's backpack and operates an item | machine | **pass, carried by rows 3-9** | every operation this row names is one of the rows below, each driven by the operator against the owner's own items and judged by its own batch; the ticket's readings list them, and this batch adds the favourite gesture to that set (`f1-guest-favouritemark.txt`, `f1-guest-census.json`) |
| 2 | Host opens the guest's backpack and operates an item | machine | **pass, batches `20261006-g` (transfer half), `20261005-d` and `20261005-e` (reverse direction)** | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-g.md` (the double-Tab take lands on the operator, the custody fact moves on both sides), `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-d.md` and `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-e.md` (the same gesture replayed on the guest's bag with the guest as owner) |
| 3 | Drag an item into a nested container | machine | **pass, batch `20261005-d`** | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-d.md` (the owner's `dogfood` entered the bag's container through the native guard, `replayed native MoveIntoContainer`) |
| 4 | Take that item back out | machine | **pass, batch `20261005-e`** | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-e.md`, both owner directions, through the container's own contents window |
| 5 | Drop / edge-drop an owned item from the remote view | machine | **pass, batch `20261005-c`** (sound half a residual) | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-c.md` (the driven release classified `DropItem`, the owner replayed it, the item was in the world afterwards) |
| 6 | Slot move / swap / switch hands | machine | **pass, batch `20261006-g`** | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-g.md` (occupied-destination swap and its empty-destination control, zero divergence on either viewer) |
| 7 use / wear | the radial CENTRE action on a remote item | machine | **pass, batch `20261005-d`** | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-d.md` (`UseItem captured` → owner `[ItemUsed] … host fact broadcast.` + `replayed native UseItem`; the wear half the same shape) |
| 7 combine | the two-item merge branch | machine | **pass, batch `20261011-d`** | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261011-d.md` (condition merge and the ammunition branch) |
| 7 battery load / unload | the battery slot | machine | **pass, batches `20261005-e` (unload) and `20261011-d` (load, 2/2)** | the same two records |
| 7 worn item | a wearable dropped and re-worn remotely | machine | **pass, batch `20261011-d`** | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261011-d.md` |
| 7 favourite | the favourite gesture over a remote item, against decision 258 | machine | **pass — THIS BATCH** | the operator's own press over the owner's `paincream` proxy produced `[FavouriteMark] marked paincream in <owner>'s inventory — the mark is local to this client; the owner's own item is untouched.` (16:13:36.350, `f1-guest-favouritemark.txt`) with the ring button read under the pointer first (`remote-gesture mode=hover cast=2` → `castUnderPointer: 0`, `castOverlaps: true`); the operator's census reads that proxy `favourited=true` (`f1-guest-census.json`) while the OWNER's own item reads `favourited=false` (`f1-host-census.json`) and the third peer reads the same instance id `favourited=false` (`f1-alt-census.json`); the owner's log carries no mark line and no `[RemoteIntent]` (`f1-host-window.txt`). The criterion the row was re-ruled onto is stated in `docs/backlog/done/favourite-marks-are-local-and-saved.md` and this batch judges it end to end |
| 8 | Held remote item, backpack closed, used from the medical panel | machine | **pass, batch `20261011-d`** | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261011-d.md` |
| 9 | A gesture the native rules refuse | machine | **pass, batch `20261005-d`** | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-d.md` (the named container-guard refusal) |
| 10 | A third peer watches the result | machine | **pass, batch `20261005-c`** | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-c.md` (the alternate's own read carries the same instance ids); this batch re-reads it for its own row (`f1-alt-census.json`, the same instance id as the operator's view with the owner's own value) |
| 11 | Solo / no session | machine | **pass, batch `20261005-c`** | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-c.md` (host alone, native drop, no `[RemoteIntent]` in the whole solo window) |
| 12 | The operator's own screen during the rows above | visual + feel | **residual, batch `20261005-c`** | the same record's frame and its question to the user |
| 13 | Owner's item at 75 % condition seen by the viewer | machine + visual | **pass, batch `20261005-d`** | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-d.md` |
| 14 | Four items into the remote trash bag | machine | **pass, batches `20261005-c` and `20261005-d`** (three of the four enter, the fourth is the native refusal) | the same two records |
| 15 | Craft screen from a remote item; a remote container's window | machine | **pass, batch `20261005-c`** (container-window half) plus the stage 4 audit | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-c.md`; `docs/evidence/selfchecks/items/remote-inventory-native-intent-stage4-audit.md` §1 for the craft half |

## Residuals for the user

- **Row 12, the feel half** (unchanged from batch `20261005-c`): whether operating a remote item feels
  identical to operating one's own — animation and sound included — is the user's call, and that batch's
  frame is the artifact it is judged from.

## Limits

- **This batch judges row 7's favourite half and nothing else.** Every other verdict in the table is the
  batch named beside it; the run that produced it is not re-run here, and its artifacts stay in that
  batch's directory.
- **Row 7's favourite half is judged against the re-ruled criterion, not the old one.** Batches
  `20261011-d` and `20261011-e` read it against the owner-authoritative shape the user replaced on
  2026-10-11 (decision 258); this record's verdict is the local-mark shape, and the sibling record names
  what the press proved.
- **A mark that a remote gesture makes is never an edit of the owner's item**, which is the criterion's
  whole point: the owner's own census and the third peer's census both read the item unchanged, and the
  owner's log holds no intent for it.
