# Closing a teammate's backpack teleports the ring before it shrinks

- Status: Review — fixed in `c2cf6bdd`; the frame evidence and its one limit are recorded below.
- Priority: Medium-High — the player sees the ring leave the teammate and land on themselves, which reads as "the game moved my bag".
- Category: Remote backpack view / radial menu presentation
- Source: user report (2026-10-25): "after opening another player's backpack ring, closing it makes the ring jump to me and then play the close animation".

## Design

`docs/design/mechanics/give-and-take.md` states the ring belongs to the FOCUSED body while a displayed backpack is open (the displayed backpack's ring is the clone's). Nothing on that page says what the ring does on the way OUT; the rule this ticket adds is the obvious continuation of the same ownership: the ring closes where it opened.

## What the game does

`PlayerCamera.HandleRadialMenu` shrinks `radialMenu` over a few frames once `radialOpen` goes false, and re-anchors it to the LOCAL body as soon as nothing holds it — the anchor is the game's own per-frame work, so a remote view that stopped anchoring at close handed the ring back to the game mid-animation.

## The fix

`RemoteBackpackView.Close()` keeps the last focused body as a CLOSING anchor (`ClosingBody`), and `PlayerCameraHandleWhileDraggingPatch` keeps anchoring to it until the ring reaches its closed scale, then calls `EndCloseAnchor()`. No clock is involved on purpose: the view is exercised by unit tests and any Unity call (`Time.time`) throws in the test host, which is how the first attempt failed.

## What was judged, and the limit

`.acceptance/frames/close1.png`, `close2.png`, `close3.png`: with the ring open on the teammate, the guest quitting closed the view and the ring stayed centred on the FOCUSED CLONE across the closing frames (frames 1-2), then the world view was clean (frame 3). That is the quit-triggered close, not the player's own close.

**The limit**: the two bodies stood about 40 px apart in that batch, so a jump of that size would not be visible; the frames prove the ring did not leave the clone, but they do not exercise the far-apart case the user described. That case is one key press away for the user (open a distant teammate's backpack, close it) and is recorded here as unjudged rather than passed.

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | Closing a teammate's backpack shrinks the ring AT the teammate, with the two players far apart | **MEASURED 2026-10-27, NOT MET**: the view was closed in-process (`remote-gesture mode=close`) and a frame was captured INSIDE that call (the new `frame=` argument, so the capture lands on the closing frame). With the ring open around the teammate (`ring-a.png`), the closing frame (`ring-close.png`) shows NO ring at all — no jump to the local player, but no shrink at the teammate either: the ring is gone in the same frame. So the "teleport" is gone and the animation half of this row is not what the build does |
| 2 | The quit/stale close keeps the ring at the focused body | **judged** by `.acceptance/frames/close1.png` and `close2.png` (close3 shows the clean world view) |
| 3 | A normal own-backpack open/close is unchanged | **owed**: the same in-call frame now makes it judgeable (`container-panel` to open the local container, then `remote-gesture mode=close frame=…`), which the next batch does |

## The in-call frame, and what it showed (2026-10-27)

A capture taken by a separate call always landed after the animation (frame `ring-b.png`: no ring), so `remote-gesture` grew a `frame=` argument that
captures INSIDE the same call, on the closing frame. That frame (`ring-close.png`) shows the ring already gone — the same frame in which the view closed —
so this build neither jumps the ring nor plays a visible shrink at the teammate. Whether a visible shrink is required is a native-parity question for the
fix's owner, and row 3 (the player's own backpack) is now judgeable with the same argument.