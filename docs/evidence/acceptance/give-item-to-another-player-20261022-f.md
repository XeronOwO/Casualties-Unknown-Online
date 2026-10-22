# Acceptance record — Give an item to another player (unit 1's gesture)

- Ticket: `give-item-to-another-player` — verdict: **stays in `todo/`** (unit 2 is not implemented, and row 9 failed
  in this run; the ticket now carries the failure and the fix's own row).
- Batch: `20261022-f` — tickets: `give-item-to-another-player`. The batch deliberately covers the seven rows the
  ticket's 2026-10-22 design added and the machine had never read (1, 2, 5, 7, 8, 9, 10); rows 3, 4 and 6 are the
  ticket's unit 2 and are not in this batch.
- Commit: `0a0fb171` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+0a0fb171205331c7e39066bbdb0a4af8f4fe16ad`, re-verified against `<game-dir>` by
  `tools/verify-deploy.ps1` (exit 0) before the clients started.
- Run: 22:36 → 22:48 (2026-10-22) · Host: physical machine · Giver: the first sandbox · Second target: the alternate
  sandbox.
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `sandbox-alt`, `hotrepl`, `dotnet`, `capture`, `input`,
  `logs`, `artifacts` — 11 present, `RESULT: OK - a full two-client run is possible; the alternate third client is
  configured` (`preflight.txt`). The gesture itself needed the physical pointer and keyboard, which the user
  authorised **for this batch only** (`20261022-f-scope.md`); the actuator is the batch-local `input-tool.ps1`,
  nothing under `tools/` gained a cursor or a keyboard call, and the standing rule is unchanged.
- Artifacts: the batch directory under the directory named by `acceptance-artifacts-dir` (`20261022-f/`), cited
  here by file name only. The clients' own log windows are the extracted `evidence-20261022-f.log-<client>.log`
  files; the frames are the `fN-*.png` files.

Class per row: the open/close/refusal outcomes are `machine` (a line plus the live view state) and the two frames
are `visual`. Everything below was produced by the real right-button drag: a real press on the ring button, real
pointer moves, a real release; no probe drove the gesture.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Drag into the receiver's range; the backpack opens with no other key pressed | machine + visual | **pass** | `f1-guest-in-range.png` (the ring re-anchored on the receiver, carrying the receiver's two items, white/operable), `guest-in-range-2.json` (`viewOpen=true`, focus `76561198281246659`), `evidence-20261022-f.log-guest.log` 22:39:59.491/492: `[BackpackView] opened native backpack view for 76561198281246659 (萧稔) — operable.` and `[BackpackView] opened 76561198281246659's backpack because the dragged item entered its detection range (the give gesture's entry).` |
| 2 | Release inside the range with a usable item: used on the receiver, backpack closes | machine + visual | **pass** | `guest-before-release.json` (`viewOpen=true` at the release point), `guest-after-release.json` (`viewOpen=false`, `dragItem=none`, `radialOpen=false`), `f3-guest-after-release.png` (no ring), guest log 22:41:05.540 `[DragUse] dropped bread on 76561198281246659 (instance 9492956821).`, host log 22:41:05.600 `. . . ate bread (id 9492956821) of 76561198863287957; condition 0.65.` and `[ItemUse] ran the game's own eat of bread (id 9492956821) on the local body.` |
| 5 | Drag across two players; the last-entered range is the target for both the backpack and the use | machine + visual | **pass** | `f5-guest-first-target.png` (the first target's ring, its two items), `f6-guest-second-target.png` (the second target's ring, its one item), `guest-row5-first-target.json` (`viewOpen=true`, proxies `itemOwner=76561198281246659`), `guest-row5-second-target.json` (focus `76561199526807662`, proxy `itemOwner=76561199526807662`), log 22:41:51 host's open then 22:41:55 the second client's open, and 22:42:00.659 `[DragUse] dropped bread on 76561199526807662` — the second, last-entered player — with the host's log `. . . offers bread . . . to 76561199526807662` and the alternate client's `[ItemUse] ran the game's own eat ...`. |
| 7 | The same release onto the giver's OWN backpack: the native local move | machine | **pass** | `guest-row7-on-bread.json` (pointer inside the giver's own slot 0, `viewOpen=false`), `guest-row7-on-target.json` (pointer inside its own slot 1), `guest-row7-after.json` (the item is in slot 1, slot 0 empty), `f4-guest-row7-after.png`; the extracted guest log carries no `[BackpackView]`, `[RemoteIntent]` or `[DragUse]` line between 22:42:02 and 22:42:12, so nothing of CUO's ran for this release. |
| 8 | Host rule `deny` / `unconscious_or_dead` | machine + visual | **pass** | **deny:** `f8-guest-deny-refused.png` (the ring stays on the giver's own body; nothing of the receiver's is shown), `guest-row8-in-range.json` (`viewOpen=false` while the cursor is in the range), log 22:42:21.400 `[BackpackView] refused open for 76561198281246659: host rule RemoteBackpackAccess=deny.` **middle value with a conscious receiver:** `f9-guest-view-only.png` (red, view-only ring on the receiver), `guest-row8b-state2.json` (`viewReadOnly=true`), then the release on the receiver's own item button (`guest-row8b-on-proxy.json`) and `f10-guest-after-view-only-refusal.png` (the ring is gone), `guest-row8b-after.json` (`viewOpen=false`, `radialOpen=false`), log 22:44:23.533 `opened native backpack view ... — view only.` and 22:44:30.885 `[RemoteIntent] refused the give of this client's own item bread onto 76561198281246659's backpack: host rule RemoteBackpackAccess=unconscious_or_dead makes it view only.`, `guest-bread-after-row8b.json` (the bread is still in the giver's slot 1 — nothing moved). |
| 9 | Cursor inside the target's range, on their body: the cue names that player | visual | **FAIL** | `f1-guest-in-range.png` and `f11-guest-cue-state-true.png` (both captured with the cursor on the receiver's body; both carry no label), `guest-cue-state.json` (`{"showing":true,"target":76561198281246659}` — the cue's own decision says the label should be showing and names the host), `f11-crop.png` (the region beside the cursor, no label). **Root cause, a code fact:** `DragUseCueOverlay.Draw` has no caller anywhere in the tree — `git log -S 'DragUseCueOverlay.Draw' --all -- src` is empty, so the drawing was never wired, and the cue ticket's 2026-10-18 "the label is drawn" claim was false from the start. The decision (`DragUseCueTracker`, `DragUseCuePolicy`) ran and answered correctly; nothing draws it. |
| 10 | Cursor on the displayed backpack: no use label | visual | **unproven** | `f2-guest-on-displayed.png`, `guest-on-displayed.json` (the pointer is inside the receiver's displayed slot-3 button, `itemOwner=76561198281246659`, `proxy=true`). The frame carries no label, but row 9 shows the label is never drawn anywhere, so this frame cannot distinguish "the cue correctly hid" from "nothing ever draws it". It becomes judgeable once row 9's fix is deployed. |

## The run's own course (why the numbers above are what they are)

- The world had the three bodies placed through `body-place` (a declared setup substitution) at `(0, 493.615)`,
  `(3.5, 493.615)` and `(7, 493.615)`, and the fixtures were created by `item-provide mode=create`: the host's
  `emergencylight` (`#1168552085443`, slot 0) and the giver's `bread` (`#9492956821`, slot 0).
- The first attempt at row 1 was refused because the host's own configuration carried
  `RemoteBackpackAccess = deny` (left by an earlier run, not this one). The run set it to `allow` through the host's
  own `/hostrules` console command, read the gesture again, and restored the file to `deny` at the end — the
  configuration is as it was found.
- Row 8's middle-value half was first staged with an UNCONSCIOUS receiver, which is the opposite of the rule: an
  unconscious owner's backpack is operable under `unconscious_or_dead` (that is the rule's other half), so the run
  re-staged it with a healthy, conscious receiver, where the pack is view-only — the shape the row names.
- The row-5 drag was made on the same session as rows 1/2: the pointer entered the host's range first, then the
  alternate client's, and the release named the latter. One incidental extra open of the alternate client's pack
  happened during row 9's staging (the pointer crossed its range on the way to the host); it is the same rule
  working, not a setup defect.

## Residuals for the user

None: every row this batch claims is judged from its own evidence above, and the two rows it cannot pass are
recorded as a failure with its root cause and as unproven, not as a taste call.

## Limits

- The fixture items are created by `item-provide`, the bodies are placed by `body-place`, and the giver's own ring is
  opened with the game's own `toggleinventory` key before the drag; the drag itself is the real pointer's.
- The bread was eaten down by the row-2/row-5 uses and destroyed by row 9's last staging use; the item's own
  condition is in the logs (0.65 → 0.31 → destroyed), so no row depends on a fixture surviving the batch.
- Rows 3, 4 and 6 are unit 2's and were not run; the ticket stays in `todo/` for them whatever this record says.
