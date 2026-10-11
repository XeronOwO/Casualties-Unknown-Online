# Acceptance record — A favourite mark belongs to the local player, and it is saved

- Ticket: `favourite-marks-are-local-and-saved` — verdict: **back to `todo/`** (row 1's gesture half, row 4
  and row 6 `unproven`; row 1's mark-exists halves and rows 2, 3, 5 and 7 both halves `pass`)
- Batch: `20261011-e` — this ticket is the batch's only ticket; scope and the pre-declared row readings:
  `docs/evidence/acceptance/20261011-e-scope.md`
- Commit under test: `d305ba9c` (no `src/` edit since) · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  `ProductVersion` `0.1.0+d305ba9ca9e1200b7ff1dee0c6678cc6503cba44`, verified against `<game-dir>`
  (`tools/verify-deploy.ps1`, exit 0; `verify-deploy.txt`). The tree also carries this batch's new read
  recipe (`tools/acceptance/recipes/favourite-marks.cs`), which the run used and which is committed with
  this record; the plugin identity is untouched by it.
- Run: 2026-10-11 15:26 → 16:05 · Host: physical machine (the OWNER of the operated items) · Guest: first
  sandbox (the OPERATOR) · Third client: alternate sandbox (the third peer, in the world throughout)
- Dependencies: `preflight.txt` (11 present, "a full two-client run is possible; the alternate third client
  is configured") and `session-environment-status.txt` (`active=cuo`, `game-running=false`,
  `swap-needed=false`), both captured before the clients started
- Artifacts: the batch directory under `acceptance-artifacts-dir`; every file cited below lives there.
  The log lines quoted here carry the roles (`<owner>`, `<operator>`, `<third peer>`) where the client
  logged a SteamId; the artifacts hold the raw lines.

| # | Row (half) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1a | the mark exists and the OPERATOR's own view shows it, while nothing about the item changes for anyone else | machine | **pass** (the mark was created by the run's declared fixture, not by a tooling gesture — see 1b) | the operator's census reads the owner's `bandage` proxy `favourited=true` while the same census reads the other two fixture proxies false (`row1-guest-census.json`); the OWNER's own census reads its own `bandage` `favourited=false` (`row1-host-census.json`); the third peer's census reads its proxy of the same item `favourited=false` (`row1-alt-census.json`); the host's log window carries no `[RemoteIntent]` for it (`ev-host-window.txt`) |
| 1b | the gesture half: a favourite gesture made while looking at the owner's item | — | **unproven as a tooling drive**, with one real observation in this run | the run could not stage the gesture (its two findings are in Limits) and the mark above was created by the declared fixture (`fav1-fixture-toggle-bandage.json`). The run does hold one REAL gesture reading: the user pressed the game's own favourite key at their own machine, and the operator's client logged `[FavouriteMark] marked paincream in <owner>'s inventory` at 15:38:19.117 and `unmarked` at 15:38:48.639 (`ev-user-press.txt`; the user confirmed the press to the agent in this session). It shows the native store reaching CUO's capture, but it was not produced by the run's own actuation and carries no frame |
| 2 | the same mark after the operator leaves and rejoins the same world | machine | **pass** | the operator's client logged `Character restore brought back 1 local favourite mark(s).` at 15:53:17.790 (`ev-guest-restore.txt`) after `leave-world` → `home.leave` → `join-lobby` (`r6-guest-leave-world.json`, `r7b-guest-lobby-leave.json`, `r8-guest-rejoin.json`; the host answered with the fresh handshake and `Sending world-entry snapshot group to <operator>`); the live set still holds the mark (`row2-marks-after-rejoin.json`) and the proxy still reads `favourited=true` |
| 3 | the same mark after a cut and a load of that world | machine | **pass** | the host left the world and continued (`row3-host-leave.json`, `row3-host-continue.json`) and the operator's client logged the same line with the same N at 15:53:53.913 (`ev-guest-restore.txt`), the live set still holding it (`row3-marks.json`). The archive half is read from the world's own `live/characters/steam-<operator>.json`, which carries `"itemInstanceId": 1190026921923` (world `w-20261011-98cb`, written by that cut) |
| 4 | the marked item later coming into the operator's own possession | machine | **unproven** | the operator's body could not take it: its limbs sit at zero force after an earlier fall in this same run, and the game's own guard refuses every slot — `InventorySlot.canPickUp` is `conscious && limb.totalForce >= 0.1`, and the live read is `crouching=true, canPickUp=false` on all six slots (`probe-slots.cs` output in `ev-frame-freeze.txt`'s sibling probe run; the recipe's own refusal is `row4e-guest-pickup.json`, `no-free-slot`). The owner's drop was made and reported (`row4d-host-drop.json`, host log `[ItemDropped] bandage (id 1215796725699) at (1.1,473.1)`), but no world item carrying that id is visible to either client afterwards (`row4j-host-world2.json`, `row4j-guest-world2.json`, both `count: 0`), so the item never reached the operator |
| 5 | an item the operator never marked, which the OWNER has favourited | machine | **pass** | the owner's own `paincream` reads `favourited=true` in the owner's census (`row5-host-census.json`) while the operator's census reads the same item's display proxy `favourited=false` (`row5-guest-census.json`) — the owner's own value is not what this view shows. The owner's own half is a declared substitution (the flag was written on the owner's own item; the run has no pointer — see Limits) |
| 6 | the favourite key over the operator's OWN inventory | — | **unproven** | not driven: it needs the same gesture input row 1b lacks (the pointer inside the client window and the key reaching the game), and the run's key channel did not reach the game in this session at all — `key-hold` answered `posted: true` with `heldAtStart/heldAtEnd: false` on its own read-back (`k1-down.json`, `k2-read.json`, `k3-read2.json`), and the two paired probes that queue the key and read the frame it lands on found `keyDown=false` at 1, 2, 3 and 5 frames of spacing (`pair-a-key-down-frame.cs`, `pair-b-frame-read.cs` via `two-evals.mjs`) |
| 7a | the marked item changes hands | machine | **pass** | the operator's client logged `[FavouriteMark] mark on item 5861509230 followed it from <owner> to <third peer>.` (`ev-guest-favouritemark.txt`) on the renderer's own identity pass, and its live set kept the mark with its recorded owner refreshed to the third peer (`row7-marks-final.json`); the operator's census then reads the third peer's `emergencylight` proxy `favourited=true` (`row7-guest-census-final.json`) while the OWNER's census reads the same proxy `favourited=false` (`row7-host-census-final.json`) — the mark is this player's own and nobody else's |
| 7b | a mark whose id carries another definition | machine | **pass** | the operator's client logged `[FavouriteMark] dropped a stale mark: mark on item 1220091692995 names a bandage, but the id now carries a paincream.` (`ev-guest-favouritemark.txt`) and the entry is gone from the live set (`row7-marks-final.json`, 4 marks → 3 with that id absent) with no star on that item or on any other (`row7-guest-census-final.json`) |

## What this run could not do, and the mechanism behind it

The gesture half of row 1 and row 6 are the batch's declared staging problem. This run established WHY,
and both findings are new:

- **The evaluator freezes the game's own frame loop while it runs.** A probe that queued the game's key on
  the client's own window and then spun on `Input.GetKeyDown` read `framesAdvanced=0`, `timeAdvanced=0`
  over 400 ms and never saw the key (`ev-frame-freeze.txt`). A key queued from inside an eval therefore
  cannot land inside that same eval, and no eval can be inside the frame a queued key arrives in unless
  the pair is timed onto consecutive frames — which the paired probes then measured.
- **The UI raycast refuses a pointer outside the viewport.** `GraphicRaycaster` normalises the event
  position by the screen size and returns when it leaves the unit square, so with the cursor 300 px above
  the client window the game's own frame can never cast a ring button, no matter where the ring is staged.
  The run staged the ring's own anchor (`RemoteBackpackView._smoothPosition`) and the ring transform so
  the target button sat exactly under the physical pointer, and the target button's `Overlaps` gate passed
  (`overlaps: true`), but the cast list stayed empty — the store never ran. The committed `while-dragging`
  seam supplies the cast list itself and does reach the store, but the same run's key channel did not
  deliver the key in this session, so that route stayed closed too.
- **The key channel itself did not reach the game in this session** (`posted: true` on the client's own
  window, the game's input state never observed on any read-back), which contradicts the channel's own
  recorded measurement; it is a capability finding for the next batch, not a product finding.

## Limits

- **The fixture path is what judged rows 1a, 2, 3, 5, 7a and 7b.** The mark set was driven through
  `LocalFavouriteMarkStore.Current.Toggle(instanceId, ownerSteamId, itemId)` — the batch scope names that
  call as the fallback, and every reading above is of what CUO's own production paths then did with the
  mark (the overlay's write on the renderer's identity pass, the restore's seed, the adoption, the drop).
- **Row 7a's hand-off is staged by its recorded owner, not by a real transfer.** The mark was installed
  naming the previous owner, and the item it names is the third peer's; the follow line and the owner
  refresh are the production path (`LocalFavouriteMarks.IsMarked`), the transfer itself was not performed.
- **The frames cannot carry the visual halves.** The two captures (`row1-guest-ring-marked.png`,
  `row1b-guest-ring-marked.png`) show the world with no ring composited and the scene rendering dark; the
  ring's GameObject was not active at capture time, and this record does not attribute the darkness to any
  mechanism. The star's visual half is therefore read from the live rendering state instead: a display
  proxy's `favourited` is the one field `InvButton.UpdateGraphic` tints the button with, and every census
  above reads that field.
- **The operator's body was wrecked twice by its own fall** (57 world units, `alive=false`, then limbs at
  zero force). Its revive, its placement beside the owner and the owner's own favourite flag are declared
  setup substitutions (`row4f-guest-revive2.json`, `row4g-guest-lift.json`, `owner-favourite.cs`).
- **The fixture items do not survive a cut.** The three items created before the cut were gone from the
  owner's body after the continue (`row4-host-census.json`), and a later drop of one of them was reported
  by the owner but is not visible to either client afterwards. The rows that needed an item re-created it
  (`row4b-host-create-*.json`), and the run kept the two marks whose items were gone: by design an id that
  carries NOTHING is never dropped (pruning on mere absence is refused), which row 7b's reading confirms
  from the other direction.
- **The mark's identity still rests on the item instance id**, and the id-reuse path is
  `todo/item-id-counter-is-not-resumed-by-a-restore.md`; nothing in this run needed it.
- **The third peer was in the world throughout** and supplies the views cited in 1a and 7a; no row of this
  batch asks anything else of it.

## Residuals for the user

None. The two open rows are not a judgement only a person can make — they are an actuation the run's
tooling could not produce, and the way out is recorded on the ticket: one hand on the mouse for one key
press per row, with the run reading everything else, or the rule decision the batch scope names (letting
the tooling move the game window instead of the pointer).
