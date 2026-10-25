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
| 1 | Closing a teammate's backpack shrinks the ring AT the teammate, with the two players far apart | **owed**: needs the player's own close |
| 2 | The quit/stale close keeps the ring at the focused body | **judged** by `.acceptance/frames/close1.png` and `close2.png` (close3 shows the clean world view) |
| 3 | A normal own-backpack open/close is unchanged | **owed**: the far case and this one share the same key press |
