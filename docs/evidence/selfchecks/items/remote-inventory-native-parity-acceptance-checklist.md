# Remote inventory native parity — real-machine acceptance checklist (2026-09-25)

Companion to the stage 4 audit
(`docs/evidence/selfchecks/items/remote-inventory-native-intent-stage4-audit.md`) and to the rework
ticket (`docs/backlog/review/remote-inventory-native-parity-rework.md`). The audit closes the matrix
rows that code facts can close; this page is what a session on the real machine has to answer. It is a
release-cycle run: no development step depends on it, and nothing here is claimed as verified until it
has been run.

## 1. Before the first click

1. Deploy the build this cycle produced and verify the artifact identity:
   `powershell -ExecutionPolicy Bypass -File tools/deploy.ps1 -GameDir "<game-dir>"` then
   `powershell -ExecutionPolicy Bypass -File tools/verify-deploy.ps1 -GameDir "<game-dir>"` (exit 0, and
   the printed `ProductVersion` `+<sha>` must be the commit under test).
2. Record which commit that `+<sha>` is; every finding below belongs to that build.
3. Start the host, join with a guest, and — for row 10 — join with a second guest.
4. Keep these logs open while running: `CUO.log` for the family's own lines (`[RemoteIntent]`,
   `[Crafting]`, `[CloneRender]`) and `BepInEx\logs\latest.log` for runtime exceptions.

## 2. The reported behaviour (re-run until it no longer reproduces)

| # | Reproduction | Expected now |
|---|---|---|
| R1 | Guest opens the host's backpack (radial ring on the host's body) and operates any item. | Every native gesture works exactly as it does on the guest's own backpack: drag, drop, slot move, container move, pour, use, wear, combine, battery, favourite, trader hand-in. |
| R2 | Host opens the guest's backpack and operates an item. | Same, in the reverse direction. |
| R3 | Drag an item into the trash bag of the displayed body, wait several seconds, re-open the backpack. | The item is inside immediately, keeps its weight and condition, and is still there after the next character snapshot and after re-opening. |
| R4 | Take that item back out of the trash bag. | It comes out immediately and stays out — it does not disappear a short while later. |

## 3. Matrix rows that need a session

| Row | Steps | Expected |
|---|---|---|
| 5 | Drag an owned item (and a worn item) out of the ring into the world until the drag leaves the ring. | The native drop happens at the native position with the native sound; the owner's body shows the item leaving. |
| 6 | Move an item between slots, swap two held items, switch hands. | The owner's body ends in the native arrangement; nothing snaps back. |
| 7 | Use, wear, combine two items, load and unload a battery, press the favourite key over an item. Also confirm the WORN-item path once: a worn item of the owner shows up in the ring's wear slots and can be dragged, dropped and re-worn (the audit's worn-item resolution rests on the game parenting worn items under the body's limb, which no test here opens). | The native result, the native animation and (see §4) the native sound; the favourite star follows the owner's own value. |
| 8 | Hold a remote item, close the backpack, open the medical panel and use the held item on a limb. | The operation works; the owner's item is consumed and the requester's body takes the effect. |
| 9 | Try a gesture the native rules refuse (a full container, a blocked slot, an item that is neither usable nor wearable in the radial centre). | Refused with the native feedback and one log line naming the intent and the reason; never a silent no-op. |
| 10 | With a second guest watching, repeat rows 3-7. | The third peer sees the same item state on the owner's clone. |
| 11 | Play solo (no session) and repeat the local gestures. | Completely unchanged local behaviour. |
| 12 | Watch the operator's screen during every row above. | The same ring, cursor, drag image, slot scale changes, alerts and backpack sound as operating one's own inventory; the exceptions are §4's items. |
| 13 | Host holds metal scrap at 75% condition; guest opens the host's backpack. | The guest sees 75%, and a later condition change on the owner's client follows. |
| 14 | Put a water bottle, dog food, a lantern and metal scrap into the remote trash bag. | Every item the native weight and tag rules allow enters immediately and stays; the ones the guard refuses do not move. |
| 15 | Drag a remote item onto the craft button, and open a container's window from the remote backpack. | The native craft screen and the native container window open on the viewer; no item of the owner's is consumed by the craft (it uses the operator's own materials), and the container window's contents survive a re-render. |

## 4. Judgement items (a limit is not a bug until you say so)

1. **The item's own sound plays on the owner's client.** Combining, loading a battery and eating or
   drinking are heard where the mutation runs, so the operator may not hear them at all. Is that
   acceptable, or should a dedicated item-sound channel be opened? (That would be a new decision and a
   wire change — a separate cycle, not a fix inside this ticket.)
2. **The radial weight readout shows the operator's own weight** while the remote ring is open, because
   the native readout reads the local body. Is that acceptable, or must it show the owner's?
3. **`TransferToBody` (the double-Tab transfer) and the medical use keep their host-authoritative
   paths**, so the owner's body runs no release animation for the custody move. Is that noticeable in
   the hand-over flow?

## 5. If something fails

Capture, in this order: (1) the exact steps and which client did what; (2) the `CUO.log` lines around
the action, especially `[RemoteIntent]` (send, refusal, replay) and `[Crafting]`; (3) any exception in
`BepInEx\logs\latest.log`; (4) which build `+<sha>` was running. A refusal line is not a failure by
itself — it names the native rule that refused — but a *silent* no-op is: an operation that neither
moved an item nor wrote a line is exactly the defect this rework exists to remove.
