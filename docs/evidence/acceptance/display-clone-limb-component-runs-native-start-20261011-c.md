# Acceptance record — a live body put under the remote medical focus runs the game's own limb component

- Ticket: `display-clone-limb-component-runs-native-start` — verdict: **moved to `done/`** (rows 1-6 pass)
- Batch: `20261011-c` — tickets `medical-removal-limb-component-not-applied`,
  `display-clone-limb-component-runs-native-start`, `medical-state-names-no-kind` (row 4); scope:
  `docs/evidence/acceptance/20261011-c-scope.md`
- Commit under test: `7c5646bd` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+7c5646bd…`, verified against `<game-dir>` (`tools/verify-deploy.ps1`, exit 0)
- Run: 2026-10-11 13:24 → 13:31 · Host: physical machine (the operator) · Guest: first sandbox (the
  patient, whose limbs carry the components) · Third client: alternate sandbox (the third-party view)
- Dependencies: `preflight.txt` and `session-environment-status.txt` in the batch directory
- Artifacts: the batch directory under `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | with a peer whose limb carries a splint, one client keeps the remote medical panel open on that peer for a full minute and logs zero `[Unity:Exception]` lines | machine | **pass** | the panel was opened through production at 13:27:16.065 and stayed open past 13:29; the host's log window from the run mark (1412 lines, `c-host-medical-log.txt`) holds **0** `[Unity:Exception]`, **0** `NullReferenceException` and **0** `OnLimbWound` lines. Batch `20261011-b` counted 6065 `NullReferenceException` lines in the same surface |
| 2 | in the same window the panel shows the splint (the icon is drawn from the state, not from the game's own `Start`) | visual | **pass** | `c-host-panel-splint-icon.png` (the operator's window, captured per client) and its 4× crop `c-host-panel-icons-zoom.png`, read by the run: the displayed limb shows the game's own splint icon. The display copy is the only body the panel is pointed at, it is parked inactive, and its components are disabled — so no game code could have registered that icon |
| 3 | the same holds for a tourniquet (`TourniquetScript`) | visual + machine | **pass** | the same crop shows the game's own tourniquet icon beside the splint's, on a limb whose display copy reads `components="TourniquetScript"` (`c-host-display-limb3.json`); after the driven tourniquet removal the icon is GONE in the next crop (`c-host-panel-icons-after-zoom.png`) while the splint's and the chilled limb's remain — the icon's lifetime is the state's |
| 4 | the same holds for a chilled limb (`ChilledLimb`), whose own `Update` acts on the limb it is attached to | machine + visual | **pass** | the display copy reads `components="ChilledLimb"` for the guest's limb 4 (`c-host-display-limb4.json`) and the panel draws the game's cold icon (the blue snowflake in `c-host-panel-icons-zoom.png`); the same window carries 0 exceptions, so the component's own `Update` ran nowhere |
| 5 | leaving and re-entering the focus on that peer (the display copy is rebuilt) stays quiet | machine | **pass** | the panel was closed (`c-host-panel-close.json`) and reopened through production (`c-host-reopen-view.json`: `opened=true`, a rebuilt `MedicalDisplay_…` copy); the window since the rebuild mark (139 lines) holds 0 `[Unity:Exception]` and 0 `OnLimbWound`, and the rebuilt copy draws its icons again from state (`c-host-panel-after-reopen.png`, `c-host-panel-icons-after-reopen-zoom.png`) |
| 6 | the production open path is the one the run drives, and its guards report the readings that let it through | machine | **pass** | every open in this batch went through `RemoteMedicalCoordinator.Open`: `c-host-open-view.json` / `c-host-reopen-view.json` answer `opened=true, viewOpen=true, displayBodyName="MedicalDisplay_110000135D30695"` with `sessionActive/localInWorld/isRemoteInWorld/hasSnapshot/hasHealth` all true, and the host's first log line of that kind in any retained log appears here — `[MedicalView] opened native medical view for <guest> (target).` at 13:27:16.065 (`c-host-medical-log.txt`). The recipes also refuse a focus that is not production's own copy (the `RemoteMedicalDisplayBody` marker on the copy's root), so a hand-built focus cannot be driven without the drive failing its own witness |

**The mechanism the fix rests on, measured in one client** (`c-inert-*.json`, the host, before the session's
removals): a `SplintLimb` added and left ENABLED reads `splinted=true, startRan=true` on the next call
(the game's own `Start` ran), while the same component added DISABLED reads `enabled=false,
splinted=false, startRan=false` and stays readable — which is what lets a display copy carry the state the
native panel asks for without carrying its behaviour.

**The peers' exception census** (`c-peer-exception-census.txt`): the guest and the third client each logged
**one** `[Unity:Exception]` in the whole run — the known, unrelated startup `TypeLoadException` about
`System.ComponentModel.DataAnnotations` at 13:25:31 / 13:25:40, i.e. before the session — and **zero**
`NullReferenceException` lines.

## Limits

- **Row 6's witness changed shape during the run, and the change is recorded rather than retro-fitted.**
  The scope page declared the witness as `displayBodyName` STARTING `MedicalDisplay_`. The first open
  answered `displayBodyName: "Body"`: the Body component sits on a CHILD of the cloned root, and it is the
  root the coordinator names. The recipes were corrected to read the root's name AND to require production's
  own `RemoteMedicalDisplayBody` marker, which is the stronger witness (it is the same fact the codec's own
  display guard reads, and a live render clone fails it). The criterion — "the focus is production's own
  display copy, never a hand-built one" — is unchanged; only its reader is.
- The panel frame is judged from the operator's own window capture, read by the run; the icon
  identification rests on the game's own sprite set (`Special/splintIcon`, `Special/tourniquetIcon`) and on
  the one-to-one correspondence between the three seeded component states and the three icons, whose
  removal-of-state / removal-of-icon pair (row 3) is what makes that identification checkable.
- A chilled limb has no removal action in the game (an icepack's component expires on its own), so row 4 is
  read as presence + silence, not as a removal.
