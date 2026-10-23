# Acceptance record — the give gesture respects the player's own UI

- Ticket: `give-gesture-own-ui-and-center-use` — verdict: **back to `todo/`** (`- Status: Todo — Rejected
  (row 4 fails: the centre WEAR is refused on every target; row 7 unproven: the view-only rule could not be
  staged)`).
- Batch: `20261023-f` — tickets `give-gesture-own-ui-and-center-use` (its ten rows) and
  `placement-failure-report-anchor` (its row 1, which reads `pass` in
  `placement-failure-report-anchor-20261023-f.md`).
- Commit: `9226c020` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+9226c020abb665adda8bf676d63aac973511d39f` — `tools/verify-deploy.ps1` exit 0 before the clients
  started.
- Run: 2026-10-23 22:33 → 22:51, this session's clock · Host: physical machine (the GIVER) · Guest: first
  sandbox (B) · Third client: alternate sandbox (C), launched for row 8 and joined into the running session.
- Dependencies: `preflight.ps1` (`RESULT: OK - a full two-client run is possible`, 11 present),
  `session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`, `launch=ok`), the in-process
  evaluator on all three endpoints, and the physical pointer and keyboard under the standing 2026-10-22
  authorisation (the actuator is the batch-local `input-tool.ps1`; nothing under `tools/` gained a cursor or a
  key call).
- Artifacts: `placement-*.txt`, `row*-host.txt`, `row3-guest.txt`, `row1-positions.json`, `row4-cue.json`,
  `row5-cue.json`, `row8-positions2.json`, `row9-*.json`, `row4-*-worn.json`, `row-reads.txt`, `marks-*.txt`,
  `row7-admin.png`, `row7-dropdown.png` in the directory named by `acceptance-artifacts-dir`; players are
  written `<host>`/`<member B>`/`<member C>`.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Own ring open, drag an item and move the cursor through a teammate's range: their backpack stays closed, the release keeps the local action | machine | **pass** | With the ring open and a real right-button drag active, the pointer's own world point read `(0, 490.625)` against the member's `(0, 491.615)` — 0.99 units, inside the 1.5-unit range (`row1-positions.json`, `row-reads.txt`) — while `mode=list` answered `radialOpen=true viewOpen=false dragItem=Item`, and the log from `marks-row1` carries **no** `[BackpackView]`/`[DragUse]`/`GiveToBody` line before the next row's open at 22:39:34.423 (`row1-host.txt`; the release was at ~22:38:3x). The release left the bandage in the giver's slot 0 — the same instance `1134192347075` is the one the next row's give captured from container 0 (`placement-host.txt`). |
| 2 | Close the own ring during the drag (the game's own toggle): entering a teammate's range opens their backpack; releasing on a slot gives; in the range off the UI drops | machine | **pass** | One real Tab press with the button held: `radialOpen=false` (`row-reads.txt`). Moving the pointer to `<member B>` then wrote both entry lines at 22:39:34.423 — `[BackpackView] opened native backpack view for <member B> … — operable.` and `[BackpackView] opened <member B>'s backpack because the dragged item entered its detection range (the give gesture's entry).` (`row2-host.txt`). Release on the displayed slot 1 gave the item (the give's own lines are in `placement-host.txt`). A later drag released 200 px off the ring still inside the open display dropped instead: `[ItemDropped] bandage (id 1147077248963) at (3.1,488.8)` (`row2drop-host.txt`), and the host no longer carried it. |
| 3 | Centre use of a usable item on the displayed player: the target executes it, the backpack stays open | machine | **pass** | The centre release of `painkillers` with the pointer 1.965 over the 135-unit centre radius: `<host>` wrote `[ItemUse] measured 10.000 ml from painkillers's own native use action.`, `[ItemUse] <host> used painkillers (id 1138487314371) on <member B>; destroyed=False.` and `[DragUse] used painkillers on <member B> (instance 1138487314371).` (`row3-host.txt`), and `<member B>`'s own client wrote `[ItemUse] local body drank 1 stack(s) from <host>.` (`row3-guest.txt`). After the release `mode=list` answered `viewOpen=true radialOpen=true` — the displayed backpack stayed open (`row-reads.txt`). |
| 4 | Centre wear of a wearable: the displayed player wears it, the giver's body does not | machine | **fail** | The same gesture with `bikehelmet` reached the use path (`[DragUse] used bikehelmet on <member B> (instance 1142782281667).`, `row4-host.txt`) but the host refused the placement: `[ItemUse] refused: bikehelmet (id 1142782281667) cannot be placed on <member B> — target limb missing/dismembered or wear slot already occupied.` Neither body wore it (`row4-host-worn.json` and `row4-guest-worn.json` both `wornCount: 0`), and the target's stored snapshot has all 15 limbs attached (`guest-saved-limbs.json`), so the refusal is not the target's state. **The cause is a pre-existing defect in the placement resolver**: `GameWearPlacement.TryResolve` (`src/CasualtiesUnknownOnline.GameAdapter/Content/GameWearFacts.cs`, "wearSlotId = info.wearSlotId; return true;") never writes the resolved limb index, so `IWearSemantics.TryGetWearPlacement` answers `limbIndex = -1` on every success and `RemoteWearApplication.TryCreateWornItem` refuses at its first guard. `git blame` dates the lost assignment to `d0fcfa27` (2026-10-11); no test covers the success path's index. Recorded on the ticket as its rejection reason. |
| 5 | Centre with an item that has no use/wear action: nothing happens and the item stays in the giver's hand | machine | **pass** | The centre release of a `bandage` wrote no `[DragUse]`/`[ItemUse]` line at all (`row5-host.txt` reads `NO MATCHING LINES` for the rest of the run), the cue was hidden for it (`row5-cue.json` `showing:false, target:0`) while the same pointer position on a wearable showed it (`row4-cue.json` `showing:true, target:<member B>`), and the item stayed carried: the later drop line names the same instance `1147077248963`, which the drop test dragged out of the ring afterwards (`row2drop-host.txt`). |
| 6 | A release on the displayed backpack's centre is a USE/WEAR, never the native drop (`[DragUse] used …`), and the use cue names the displayed player there | machine | **pass** | The centre release produced `[DragUse] used painkillers on <member B> (instance 1138487314371).` and no `[ItemDropped]` line for that instance (`row3-host.txt`), and the cue read at the centre named the displayed player: `{"showing":true,"target":<member B>}` (`row4-cue.json`). |
| 7 | View-only displayed backpack: a centre use/wear is refused with one line and nothing moves | machine | **unproven** | A view-only display only exists under the host rule `RemoteBackpackAccess=unconscious_or_dead`, and the run could not set that rule: the admin page's rule row is a native `TMP_Dropdown` whose option list carries no CUO control ids (`row7-admin.png` shows the row reading "allow"; `row7-dropdown.png` shows the field after a real click with no list captured), no console verb sets the rule (`PluginDependencyRegistrar` binds it as a config entry, the UI is the only writer), and the in-process driver has no `ControlSelected`-with-value action. Nothing about the refusal itself was observed; the row is not a pass and not a residual — it needs a driveable rule switch (a control id per option, or a console verb) before it can be read. |
| 8 | B → C: with B's backpack displayed, dragging into C's range displays C's backpack (the last entered range wins) | machine | **pass** | With `<member B>` at (3, 491.615) and `<member C>` at (-3, 491.615) (`row8-positions2.json`), the drag opened `<member B>`'s backpack at 22:47:45.497 (`focusScreenX` 686), and moving the pointer into `<member C>`'s range wrote `[BackpackView] opened native backpack view for <member C> … — operable.` and its entry line at 22:47:50.986, with `focusScreenX` now 594 (`row8-host.txt`, `row-reads.txt`). One drag, two ranges, the last entered one displaying. |
| 9 | The local wound/medical panel open: dragging an item does not open any remote backpack | machine | **pass** | The own wound view was opened with the game's own `ToggleWoundView` (`row9-open-wound-view.json` `woundViewOpen:true`), the drag stayed live (`dragItem:painkillers` in `row-reads.txt` — the probe answers the item's `id`, see the note below), and with the pointer's world point `(2.937, 491.375)` against `<member B>`'s `(3, 491.615)` — 0.25 units, inside the range — the display stayed closed (`viewOpen=false`, `row9-after-move.json` + `row-reads.txt`). The first `[BackpackView]` line after `marks-row9` is at 22:45:33.853 and belongs to the next row's drag (`row9-host.txt`), so this row's window wrote none. |
| 10 | Repeated toggles: one press closes the displayed backpack, a second opens the player's own; no stuck state | machine | **pass** | With the displayed backpack open after the give, one real Tab press answered `viewOpen=false radialOpen=false buttonCount=0`; the second answered `viewOpen=false radialOpen=true` with six buttons listed (`row-reads.txt`), and the very next gesture (the row-3 drag) started from that clean state and produced its own use. |

## The finding this run files

Row 4 is a **real, pre-existing defect**, not a fixture problem: `GameWearPlacement.TryResolve` answers `true`
while leaving `limbIndex` at its initial `-1`, so every cross-player wear is refused before it can be applied.
The evidence is on the machine (`[ItemUse] refused: bikehelmet …`) and in the code (`wearSlotId =
info.wearSlotId; return true;` with no `TryResolveLimbIndex(… out limbIndex)`), and the target's own state
rules out the refusal's stated cause. The ticket carries it, and the fix is ordinary development work with a
red test on the success path.

## Residuals for the user

None. Row 7 is `unproven` for a tooling reason, not a judgement only a person can make.

## Limits

- **Fixtures are declared substitutions.** The items were created with `item-provide mode=create` (`painkillers`
  `1138487314371`, `bikehelmet` `1142782281667`, `bandage` `1147077248963`), the members were positioned with
  `body-place`, and the wound view for row 9 was opened with the game's own `ToggleWoundView` because the
  committed recipe's arm skips the open when the view object's `activeSelf` is already true under an inactive
  parent. The gestures themselves — the right-button press, the moves, the Tab presses, the releases — were the
  real pointer's.
- **Row 9's drag flag**: the ui-state answer spells the drag as `painkillers`, the mode list as `Item`; both
  name the same item, and the two probes read different fields.
- **One repetition per row.** Rows 2, 3, 5 and 8 were each driven once; no row repeated its gesture, so a race
  inside the gate is not excluded by this run.
- **Row 7's staging gap** is a tooling limit recorded above; it is the one row of the ticket the run could not
  put into its state.
- **The third client joined a running session.** Its entry group landed ~20 s after the join answered
  `active`, and rows 8's readings were taken after the host itself listed `<member C>` `inWorld: true`
  (`row8-positions.json` shows the single-member state before that, `row8-positions2.json` the three-body
  state).
