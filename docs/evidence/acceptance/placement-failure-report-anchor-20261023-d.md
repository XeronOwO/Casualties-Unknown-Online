# Batch 20261023-d — the placement outcome's machine row: attempted, no reading

- Applies to acceptance row 1 of `docs/backlog/done/placement-failure-report-anchor.md` ("a give applied at the
  answered destination reports the same place; the host sees a match and writes nothing").
- Artifact: `0.1.0+1bf7104ccbb9a580050aebe576c34b8967d67e4e`, deployed and verified against `<game-dir>` before
  the clients started (`verify-deploy.ps1` exit 0, "Deployment matches this tree's build output").
- Dependencies: `preflight.ps1` reported 11 present, `RESULT: OK`; `session-environment.ps1 -Mode status` reported
  `active=cuo`, `game-running=false`, `launch=ok`.
- Run: host on the physical machine (the GIVER) and the first sandbox (the RECEIVER); one world, one session. Both
  clients reached `inWorld=true`; the guest joined the host's lobby and the host started the run. The CUO Online UI
  window was closed on the host before the gesture.
- Setup substitutions (declared): the guest's body was placed with the `body-place` recipe (`x=3`, the host's own
  spawn y) and the giver's item was created with `item-provide mode=create` (`bandage`, id `1112717510595`, its own
  answer: slot 0). Everything below was driven with the real pointer and keyboard (`.acceptance/20261022-f/`,
  local, never committed), not with a call seam.

## What was tried, and what was observed

The host window was the foreground window and `WindowFromPoint` named it for every attempt; the keyboard worked
throughout (the game's own `toggleinventory` key opened and closed the native radial), and the pointer's world
hover answered with the game's own item labels — so the client was reading both the keyboard and the cursor.

| # | Attempt (real pointer) | Observed |
|---|---|---|
| 1 | Right-press on the HUD cell that shows the created `bandage`, move, capture | No drag started: no drag sprite followed the cursor, and no CUO line was written |
| 2 | Right-press on the native radial's main-hand sector, then the arm/back sectors (radial opened with the game's own key) | Same: the sectors show slot icons and no item of this body's; the press changed nothing |
| 3 | Right-press on a world item under the cursor | Same |
| 4 | Right-press on the other player's on-screen body | The native radial did not open; the other player is a CUO clone render, not a native `Body` collider |
| 5 | Left-click on the radial's center (`物品栏`) | The native backpack panel did not open |

No client wrote a `[BackpackView]`, `[DragUse]`, `[RemoteIntent]` or `[Placement]` line during the attempts, so the
give never reached the host's placement service and the new outcome message was never exercised.

## Why the reading was not produced

The gesture's PRESS has to land on a native `InvButton` or `ItemLabel` (`PlayerCamera.HandleStartDragging` →
`TryPickupFromUI`), and on this client the created item appeared only in the HUD grid, which is not a raycast
target for that path; the native radial's sectors are the body's slot buttons and none of them showed the item.
The receiving half of the gesture (the backpack opening when the dragged item enters the other player's range) is
CUO's own, so it needs a drag that never began. This is a SETUP problem — where the item has to sit for a player's
own hand to reach it — not a defect of the change: no code, artifact or data was modified during the attempt, and
the client and server logs show nothing of the give family being asked.

## What this record does NOT claim

- No give was performed. Row 1 therefore has **no machine reading**; `PlacementAnswerTests` (12 cases) and the
  shape pins in `GiveGestureGateTests` remain the only evidence for the change.
- Rows 2-3 were not staged either: they need the same drag, and the answer-first window they depend on cannot be
  produced by hand even when the drag works (batches `20261022-i`/`-j` and the user's 2026-10-23 ruling).
- Nothing here is evidence about the outcome message's behaviour on a real pair of clients.

## What a next batch needs

The item must be reachable by the player's own hand — either placed in a radial slot (the shape batch
`20261022-f`'s giver used) or with the native backpack panel open — before the drag can be performed. That is the
only open question; the gesture's remaining steps (the drag to the other player's range, the release, the host's
match line and the receiver's report) are unchanged from batches `20261022-f`/`-h`, which read them on the previous
artifact.

Local artifacts for this attempt (not committed): `.acceptance/20261023-d/` holds the window captures
(`host-1.png` … `host-13-onplayer.png`), the body positions and the deployment/preflight output.
