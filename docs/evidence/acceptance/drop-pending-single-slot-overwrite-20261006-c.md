# Acceptance record — A same-frame second drop overwrites the pending report of the first

- Ticket: `drop-pending-single-slot-overwrite` — verdict: **stays in `review/`**, its one runtime row was not
  driven by this batch (the ticket has no code left to develop; the reading is what is missing)
- Batch: `20261006-c` — tickets `container-move-snapshot-only-sync`, `drop-pending-single-slot-overwrite`
- Commit: `8da00be3` · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+8da00be32f5ae66c8ea5e45957b86d0051823cf4`
- Run: 2026-10-06 12:14 → 12:21 local · Host: physical machine · Guest + third peer: Sandboxie sandboxes
- Dependencies: the same eleven the batch's preflight reported present
- Artifacts: `20261006-c/` in the directory `acceptance-artifacts-dir` (this ticket's own probes are the ones the
  container-move record names; the session's gesture sequence is logged there)

## The rows as planned before the run

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A slot release onto an OCCUPIED destination slot drops two items inside one frame; both reach the world table and the third peer finds the one that left the first slot | machine | **unproven — not driven** | the session's budget went to `container-move-snapshot-only-sync` row A1g (the fix that made this machine's second producer reachable); no occupied-destination slot release was driven before the session's scenarios were exhausted |
| 2 | The operator's and the third peer's clone-fact monitor stays at zero over that gesture | machine | **unproven — not driven** | as row 1 |
| 3 | The machine holds one entry per item: a second departure in one frame does not swallow the first | unit | **pass** | `DropPendingStateTests.ASecondDepartureInTheSameFrame_DoesNotSwallowTheFirst` / `…TwoDeparturesInOneFrame_BothSettleAndReportAfterTheFrame` (read RED against HEAD with the then-current three-argument `EnterDrop`), `…EnterDrop_SameItemTwice_ReplacesThatItemsOwnEntry`, `…ResetAll_ReturnsEveryOpAndClears`; the mechanism is stated in decision 238 |

## What this run does and does not say

- The unit half of this ticket's fix is verified (row 3) and the ticket's code is complete; what is missing is the
  three-client reading of the gesture that reaches the machine at runtime.
- The batch's session DID exercise the same pending machine on its new producer: `container-move-snapshot-only-sync`'s
  row A1g read the departure registered, consumed and reported as ONE container move, in both owner directions, with
  zero divergence warnings — and a body drop in the same session (`op=23`, 12:19:48.624 → `[ItemDropped]`
  12:19:48.640) shows the per-item flush still reporting the next frame after a plain drop.
- The gesture this ticket needs (a release onto an occupied destination slot) was NOT driven, so nothing here is
  claimed about it. The rejection path does not apply: no row of this ticket failed.

## Residuals for the user

None.

## Limits

- The occupied-destination slot release needs a destination slot that already holds an item AND a dragged item from
  another slot; the batch's fixture set was built for the container pair instead.
- The consistency this ticket is about (a second departure in one frame) is not observable in the two windows this
  batch did drive: row A1g's expansion moves ONE child, and the drops it drove were single.
