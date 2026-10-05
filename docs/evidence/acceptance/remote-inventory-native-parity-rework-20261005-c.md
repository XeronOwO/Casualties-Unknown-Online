# Acceptance record — Remote inventory operations: run the native path end to end

- Ticket: `remote-inventory-native-parity-rework` — verdict: **stays in `review/`** — rows 1–6, 10, 11, 14
  pass and row 15's container-window half passes; row 12 is a residual for the user; rows 7, 8, 9 and 13
  are `unproven`
- Batch: `20261005-c` — the batch's plan named `remote-inventory-native-parity-rework`,
  `remote-fentanyl-injection-and-medical-panel-desync` and `remote-medical-panel-acceptance-issues`; the
  session reached the inventory rows and closed there, so the two medical tickets keep their rows unjudged
  (see Limits) and stay in `review/`
- Commit: `f15d3fa1` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+f15d3fa18875912fe5e5c9a40bc85b5f51d71318` (`deploy.ps1` and `verify-deploy.ps1` exit 0)
- Run: 2026-10-05 20:24 → 20:41 (+08:00) · Host: physical machine (evaluator `18590`, PID 15552) · Guest:
  primary sandbox `Steam1` (evaluator `18591`, PID 46828) · Third peer: alternate sandbox `Steam2`
  (evaluator `18592`, PID 25612)
- Dependencies: preflight `11 present`, exit `0`; machine gate before launch `active=cuo`,
  `game-running=false`, `swap-needed=false`, `launch=ok`; neither sandbox carried a plugin shadow (both
  plugin directories absent, so both read the physical deployment)
- Artifacts: the batch directory under `acceptance-artifacts-dir` — probe JSON, log excerpts and one frame,
  cited by name below

## The mechanism this run added (state it before the verdicts)

The game's own release entry is `PlayerCamera.HandleReleaseDragging(List<RaycastResult>)`
(`PlayerCamera.cs:1456`, private), and `PlayerCameraDragUsePatch` brackets exactly that invocation, so a
display proxy released through it becomes a native intent. A run may not use OS-level input, so the run
drives that entry in process: the committed recipe `tools/acceptance/recipes/remote-gesture.cs` stages the
three inputs the native branch reads — `PlayerCamera.dragItem` (the rendered proxy, by authoritative
instance id), `PlayerCamera.clickPos` and the `uiCasts` list — and then invokes the method. The ring's
screen position is staged under the real pointer as well, because `InvButton.Overlaps` answers
`152 < distance/uiScale <= 281` for a centre-class button and the run cannot move the pointer; the guard
still decides, and the branch that runs is the game's own.

Two consequences are recorded as limits, not hidden: the first attempt without the ring staging produced
`the release of item … produced no intent — unclassified native gesture` (`r3-guest-insert.json`) — the
guard refused, exactly as it would for a pointer outside the ring; and a take-out is released with an empty
cast list, which is the native "release over empty space" case.

## Row verdicts

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 11 | Solo / no session: local gestures unchanged | machine | **pass** | Host launched alone, `s0-host-start-run.json` (`active=false`, `inWorld=true`), `item-provide` created bandage `1194321889219` (`s0-host-provide.json`), the driven release dropped it natively (`s0-host-release-drop.json`, `dragAfter=none`), the body no longer carried it (`s0-host-find-after.json` `found=false`) and the host log carries `[ItemDropped] bandage (id 1194321889219) at (-0.4,483.1)` with **no** `[RemoteIntent]` line in the whole solo window |
| 3 / R3 | Drag a carried item of the owner into his trash bag | machine | **pass** | Guest (operator) with the host's backpack open: `MoveIntoContainer captured for item 1155667183555 of 76561198281246659 (container 1147077248963)` (`r1-guest-log.txt` 20:33:40.337) → owner `item dogfood entered container 1147077248963 through the native guard` + `replayed native MoveIntoContainer` (`r1-host-log.txt`) |
| 4 / R4 | Take that item back out | machine | **pass** | `TakeOutOfContainer captured for item 1155667183555` → owner `replayed native TakeOutOfContainer` (`r4-guest-takeout.json`, both log excerpts); the item left the bag on both sides and **stayed out**: the owner's carried tree no longer held it (`r4-host-find-dogfood.json` `found=false`) and the owner picked the same instance id back up from the world through the native guards (`r4-host-pickup-dogfood.json`, `picked=true`). The reported "comes out and then disappears" did not reproduce |
| 1 / R1 | Guest operates the host's backpack (guest → host) | machine | **pass** | Rows 3, 4, 5, 6 and 14 below are all this direction, each with the guest's capture line and the owner's replay line |
| 2 / R2 | Host operates the guest's backpack (reverse) | machine | **pass** | `r2-host-open.json` (`viewOpen=true`), `r2-host-list.json` (the guest's bag `9492956821` at button slot 0), insert `r2-host-insert.json` → host `MoveIntoContainer captured for item 13787924117 of 76561198863287957` + forward `76561198281246659 → 76561198863287957` → guest `item soup entered container 9492956821 through the native guard`; take-out `r2-host-takeout.json` → guest `replayed native TakeOutOfContainer`, and the guest no longer carried it (`r2-guest-find-soup.json` `found=false`) |
| 5 | Drop / edge drop out of the ring | machine | **pass** | Guest dropped the host's soup: `r5-guest-drop.json` (`moved=1`, empty casts) → owner `76561198863287957 → 76561198281246659 DropItem (item 1151372216259)` + `replayed native DropItem`; the item was in the world afterwards and the owner picked it back up natively (`r5-host-pickup-soup.json`, `picked=true`). The sound half is a residual (checklist §4) |
| 6 | Slot move / swap / switch hands | machine | **pass** (slot move half; see Limits) | Host moved the guest's bandage to an empty ring slot: `r6-host-slot.json` (cast at slot 5) → guest `item bandage now holds the owner's slot 5` + `replayed native PickUpToSlot on item 18082891413 (container 0, slot 5)` |
| 10 | A third peer watches the result | machine | **pass** | The alternate's own read (`r10-alt-list.json`, 8 proxies) carries the same instance ids as the owner's state: the host's `1164257118147` (lantern) and `1159962150851` (waterbottle) both render under `trashbag(Clone)`, and the guest's items keep their ids — an independent client, not a second view of the operator's |
| 12 | The operator's own screen during the rows above | visual + feel | **residual** | Frame `f12-guest-operator-screen2.png` read by the agent: the guest's window shows the native ring open on the owner's body with the owner's item icons and the selected-item label (`垃圾袋 (100%)`); whether the animation and sound feedback feel identical to operating one's own backpack is the user's call (see Residuals) |
| 14 | Several items into the remote trash bag | machine | **pass** (three of the four items; see Limits) | `r14-guest-insert-1159962150851.json` and `r14-guest-insert-1164257118147.json` → owner `item waterbottle entered container 1147077248963 through the native guard` and `item lantern entered …`, each with its `replayed native MoveIntoContainer`; the dogfood of row 3 is the third. The viewer's render shows both nested under `trashbag(Clone)` (`r4-guest-list.json`) and the third peer agrees (`r10-alt-list.json`) |
| 15 | Craft screen from a remote item; a remote container's window | machine | **pass** (container-window half; see Limits) | Guest ran the committed `container-panel` recipe with `mode=remote container=trashbag guest=<host>`: `r15-guest-container-panel.json` → `opened=true`, `contentsCount=2`, matching the owner's real bag contents (lantern + waterbottle). The game's own `PlayerCamera.OpenContainer` opened it on the proxy; no `[RemoteIntent]` was produced for the window itself |
| 7 | Use / wear / combine / battery load-unload / favourite | machine | **unproven** | Not reached: the use/wear branch is the radial CENTRE action (`PlayerCamera.cs:1638`), whose buttons exist only inside the native while-dragging frame, and a synchronous eval cannot stage that frame. No weaker check was substituted |
| 8 | Held remote item, backpack closed, used from the medical panel | machine | **unproven** | Not reached: the row needs the medical-panel fixture (held remote item + a limb) that this session did not build; the ticket's own matrix marks it as the landed `ApplyToLimb` route |
| 9 | A gesture the native rules refuse | machine | **unproven** | Not reached: no refusal fixture (a full container, a blocked slot, a radial no-op) was staged. The run did observe the window's refusal path once — the unstaged first attempt logged `produced no intent — unclassified native gesture` — but that is the guard refusing a pointer outside the ring, not the row's fixture |
| 13 | Owner's item at 75 % condition seen by the viewer | machine + visual | **unproven** | Not reached: the run has no staging step that sets an item's condition, so the fixture could not be built |

## Residuals for the user

1. **Row 12** — frame `f12-guest-operator-screen2.png`: is the operator's feedback (ring, drag image, alerts,
   sounds) the same as operating one's own backpack? The frame answers the "it renders" half; the feel is
   yours.
2. **The item's own sound plays on the owner's client** (checklist §4.1) — combining, loading a battery,
   eating and drinking are heard where the mutation runs, so the operator may not hear them.
3. **The radial weight readout shows the operator's own weight** (checklist §4.2) while the remote ring is
   open.
4. **`TransferToBody` and the medical use keep host-authoritative paths** (checklist §4.3), so the owner's
   body runs no release animation for that custody move.

## Limits

- The gesture is driven in process with staged inputs (see the mechanism section): the ring's screen
  position is moved under the real pointer so the native geometry guard decides. A row that failed because
  of that staging would be a finding, and the first attempt's `unclassified native gesture` is recorded as
  exactly that.
- A take-out is released with an empty cast list (the native "release over empty space" case), so the item
  leaves the container and ends up in the world; a slot-directed take-out (release over a ring slot) was not
  driven.
- Row 6 exercised the slot move; a swap of two held items and the switch-hands gesture were not driven.
- Row 14 covered three of the four items its text names: `metalscrap` is not a creatable type under the
  names tried (`item-provide` `found=false`), so the metal-scrap half is unproven inside a passing row.
- Row 15's craft half was not driven: the craft screen from a remote item needs the craft fixture, and the
  audit already records its code fact.
- The viewer's rendered slot numbering is the UI's, not the owner's body index (the same item can read as
  `InvSlot (2)` on one client and slot 5 on the owner); every slot claim above uses the owner's log line.
- The two medical tickets the batch planned were not reached — the session built and used the inventory
  gesture primitive, and the syringe-drag-on-a-remote-limb path needs its own staging. Their rows stay
  unjudged, and both tickets stay in `review/`.
- One session cannot disprove a rare race; every verdict above is a reading from this run against the
  deployed artifact.

## Findings filed

- `container-move-snapshot-only-sync` — every remote container move this run drove made
  the OPERATOR's clone fact monitor warn (`nested container contents changed without an event sync`,
  `left the inventory without an event sync`), while the owner's client logged none. The monitor, the move
  and the replay are all this repository's code, so the carrier question is ours to answer.
