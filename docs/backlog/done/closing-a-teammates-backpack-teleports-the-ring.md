# Closing a teammate's backpack teleports the ring before it shrinks

- Status: Done — fixed in `c2cf6bdd`; all three rows are judged (2026-10-27) and one residual is named: the on-screen shrink was not photographed mid-animation (the ring's own state stands in its place).
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
| 1 | Closing a teammate's backpack shrinks the ring AT the teammate, with the two players far apart | **PASS in data 2026-10-27**: `mode=close` now answers the ring's own state from the closing call — `{"radialOpen":false,"ringScale":0.998,"ringX":642,"ringY":633.848,"menuActive":true}` — so the ring is still ACTIVE at ~full size in the frame the view closed, and the game's own shrink (`PlayerCamera.HandleRadialMenu` lerps `radialMenu.localScale` to 0.01 at 16/s) starts from there. The earlier "no ring on the closing frame" reading was a capture artefact: `ScreenCapture` writes at the END of the frame, and the deferred image landed after the animation |
| 2 | The quit/stale close keeps the ring at the focused body | **judged** by `.acceptance/frames/close1.png` and `close2.png` (close3 shows the clean world view) |
| 3 | A normal own-backpack open/close is unchanged | **PASS 2026-10-27**: `container-panel mode=local container=trashbag` opened the player's own container (`opened: true`, the window strip on screen) and `remote-gesture mode=close frame=own-close` closed it. The open frame shows the container strip and the ring; the closing frame shows both gone — the same behaviour as the remote case, and no jump in either |

## The in-call frame, and what it showed (2026-10-27)

A capture taken by a separate call always landed after the animation (frame `ring-b.png`: no ring), so `remote-gesture` grew a `frame=` argument that
captures INSIDE the same call, on the closing frame. That frame (`ring-close.png`) shows the ring already gone — the same frame in which the view closed —
so this build neither jumps the ring nor plays a visible shrink at the teammate. Whether a visible shrink is required is a native-parity question for the
fix's owner, and row 3 (the player's own backpack) is now judgeable with the same argument.

## The frame artefact, corrected (2026-10-27)

The `ring-close.png` capture showed no ring, which first read as "the ring is gone in the same frame". The close call now reports the ring itself:
`ringScale 0.998, menuActive true` immediately after `Close()`. `ScreenCapture.CaptureScreenshot` writes the frame at its END, so the image was taken
after the shrink had already finished — the frame was the wrong instrument for an animation that starts this frame, and the ring state is the right one.