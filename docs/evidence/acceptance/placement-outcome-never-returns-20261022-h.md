# Batch 20261022-h — placement-outcome-never-returns rows 1-6

- Artifact: `0.1.0+fb782e5ce211780e843a2a33973b36c2fc93e620`, deployed and verified against `<game-dir>` before
  the clients started (`verify-deploy.ps1` exit 0, "Deployment matches this tree's build output").
- Run: the host on the physical machine as the GIVER, the first sandbox as the RECEIVER; one world, one session,
  both in world from 23:47. The scope and the declared fixtures are `20261022-h-scope.md`.
- Fixtures as declared there: the receiver carried a `plasticbag`, a `bandage` and its starting
  `emergencylight`, later filled to every slot; the giver carried a `toolbox`, an `emergencylight` and a
  `bandage`. The gestures were the real pointer's (right-button drags from the ring, a real Tab to open it).

| # | Verdict | Evidence |
|---|---|---|
| 1 | PASS | Release on the receiver's displayed `plasticbag` with the 4 g `toolbox`: the receiver's own client answered `[Placement] answered query 1 for item toolbox (id 1245861496771): refused: toolbox does not fit container plasticbag by the container's own rules.`, the host answered the giver `[RemoteIntent] refused GiveToBody: toolbox does not fit container plasticbag by the container's own rules.`, the `toolbox` was still on the giver's body (`item-provide find` → slot 0, onBody), and no transfer followed |
| 2 | PASS | Every receiver slot filled, release on a displayed slot: the receiver answered `[Placement] answered query 4 ... refused: toolbox does not fit any free slot on this client's body.`, the host answered the giver the same sentence, and the `toolbox` stayed with the giver |
| 3 | PASS | Release on the receiver's occupied slot 1: the host asked `(query 3, slot 1, container 0)`, the receiver answered `slot 2`, the host committed (`[Take] ... bandage ... from ...`), the receiver's own client placed it (`[PlayerInteraction] placed bandage (id 1254451431363) in local slot 2.`), and the host's own snapshot for the receiver names the ANSWERED slot (`1254451431363:bandage@2`) |
| 4 | PASS | Release on the `plasticbag` with the light: the receiver answered `inside container 9492956821`, the host's row nested it (`[Take] ... (remote-backpack allowConscious=True, container 9492956821)`), the receiver placed it (`[PlayerInteraction] placed emergencylight (id 1250156464067) into container plasticbag (9492956821).`), and the host's snapshot shows the light under the bag (top-level rows: `plasticbag@0`, no light at top level) |
| 5 | PASS | The deny arm: with `RemoteBackpackAccess=deny`, dragging into the receiver's range produced `[BackpackView] refused open for <receiver-steam-id>: host rule RemoteBackpackAccess=deny.` on the giver's own client, the view stayed closed (`viewOpen=False`), and no placement question, transfer or row followed |
| 6 | UNPROVEN | The residual's window is the answer→transfer round trip (the run's own row 3: answer at 23:49:53.812, apply at 23:49:54.353). The batch changed the receiver's body only between drags, never inside that window. Worse, the review of the batch's own artifact (`fb782e5c`) found the residual's branch could not run at all at that commit: the exact flag was dropped at the kernel projection and the adapter never received the resolver, so no client could produce the failure report. Row 6 is therefore unproduced and unproducible as that artifact was wired — it is not merely a race this run could not stage |

## Limits

- The row-2 fixtures are created junk items that fill the six slots, and the bodies were placed with the
  `body-place` recipe; both are declared setup substitutions, the gestures were the real pointer's.
- Row 6 could not be staged without a call seam (a probe calling the failure report), which is not evidence a
  player can produce it; the review of this batch's artifact then found the branch itself was unreachable, which
  is why the row is recorded `unproven` and the ticket kept the wiring as the next cycle's work.
- The rows 3 and 4 host-snapshot readings (`bandage@2`, the light nested under the bag) were read in-session
  through a host eval and were NOT saved into this batch's artifact directory; the saved artifacts are the two
  log extracts, which carry the receiver's own placement lines and the host's `[Take]` lines. The snapshot file
  was re-created later and is not part of this record's evidence.
- The batch set `RemoteBackpackAccess=allow` for rows 1-4 and `deny` for row 5. The clients were closed with the
  rule at `deny`, the value the machine held before the batch.
- One window-level frame (`host-final.png`) records the host's screen after the run; the row evidence is the log
  and state readings above.

## Addendum (2026-10-23)

The user ruled that row 6's reading is the behaviour test plus the wired return chain, because the row's 60 ms
window cannot be produced by hand (batches `20261022-i`/`-j`). The `UNPROVEN` verdict above stays as this batch's
machine reading; the row's closure is `placement-outcome-never-returns-20261023-ruling.md`.
