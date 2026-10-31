# A disconnected member leaves his arrow on the HUD

- Status: Review — opened 2026-10-28 from the user's report (translated): "why does the arrow marker of a teammate still show after the guest disconnected?".
- Priority: Medium — a wrong marker is a lie about who is in the session, and it sits on top of the world the player is playing in.
- Category: Online UI / remote render clones / member lifecycle
- Source: user report (2026-10-28), observed after both peers dropped out of the world in the acceptance session.

## What the player sees

A teammate's off-screen direction arrow keeps pointing at a player who has left — the session roster no longer names them, the world has no body for them, and the
arrow still tracks a position.

## What is established, and what is not

- **Established:** both peers left the world during the 2026-10-28 batch (`inWorld: false` read from their own clients through the driver) while the host stayed
  in world; the report of the stale arrow came after that.
- **Not established:** WHICH surface draws the arrow. The two candidates, in the order they are cheapest to tell apart:
  1. **A render clone that outlived its member** — CUO's clone teardown (`RemoteCloneRender`, `CloneInventoryRenderer`, `RemoteBackpackView.ClearIfStale`) is
     driven by the member list; a clone left in the scene (or left active) would keep whatever marker the game itself attaches to a body, and the arrow would
     then be the game's own, not CUO's drawing.
  2. **CUO's own overlay drawing a marker** for a member row that was not removed from the projection it reads.

## The first read

Keep the client in the session, drop ONE peer, and read in the same frame: the member list (`remote-gesture mode=list` answers the roster), the scene's
`RemoteCloneRender` instances (name + active + position), and whether the arrow's object is a child of one of them. That tells the two candidates apart
before any code is touched; a fix without that read would be a guess about which half owns the object.

## Acceptance

| Row | The check |
|---|---|
| 1 | A peer that leaves the world takes its arrow with it within one frame, read on the still-connected client (frame before/after and the roster read) |
| 2 | A peer that rejoins gets its arrow back without a restart |
| 3 | A peer that leaves while its arrow is off-screen leaves no marker, and no clone is left active in the scene |
## Observed while trying to reproduce (2026-10-28 23:35)

After both peers dropped and rejoined the same lobby, each peer's own gate read `Waiting for the host to load… The host is not in the world yet.` while the
host's own client answered `inWorld: true`. So the disagreement this ticket is about is not only the ARROW: a member's client can hold a stale answer about
the host's world, and the host can hold a stale answer about the member. The first read should therefore capture both sides of that fact in one frame (the
host's member list and each peer's gate) — the arrow is one symptom of it, and fixing the arrow without it would be treating the picture, not the fact.
## Design

Read `docs/design/model/session-lifecycle.md` (what a member's presence means to the other clients) and `docs/design/surfaces/online-window.md` (the HUD surface
this marker belongs to) before opening this ticket.
## Reproduction attempt on the fixed build: no arrow, and the clone is gone (2026-10-29 00:50)

Fresh three-client session on `b43734d7` (the session fix): the guest first left the world (`leave-world`), then quit for good (`quit`). Read at each step on
the HOST:

- roster after the quit: both peers still listed (`76561198863287957`, `76561199526807662`) — the host keeps a row for a peer that is gone;
- scene scan around the host (`clone-components`): no `Body` clone for the peer — only the pancake clone and particle/liquid effects, so the peer's render clone was
  torn down;
- frames before and after the quit (`arrow-a` / `arrow-c`): pixel-identical in the HUD, and NEITHER shows an arrow or marker for the peer.

So on this build a clean quit leaves no visible marker, and the ticket cannot be closed as fixed on this evidence — it also cannot be reproduced, so the cause is not
established. What the read rules out: `ArrowGuide` (`reversing/…/ArrowGuide.cs`) is a decoration that bobs ±1 unit on `sin(Time.time)`, not a teammate marker;
no CUO catalogue string or class draws an arrow (`arrow`, the catalogue words for "direction" and "pointing", `Marker`, `Indicator` and `Pointer` all come up empty outside `ArrowGuide`).
The remaining candidate is the situation the USER saw, which this batch did not recreate: a peer whose row the host still holds (the roster above proves the host keeps it) drawn
by whichever surface the game uses for an off-screen teammate. The next step is to read the game's own UI for how a second `Body` is presented at the screen edge
(`PlayerCamera`'s UI actions and the pod's HUD), now that `ArrowGuide` is excluded by reading rather than by guess.
## The read the ticket asked for: the arrow leaves with the peer (2026-10-31, build `0c282f7f`)

The batch needed a peer whose arrow was actually on screen, and the two bodies spawned on the same tile in terrain that boxes a walking body in (both stopped after two
12-unit nudges). A new acceptance recipe `teleport` puts the local body at a given world point for exactly this: the guest went to x=400 and its marker left the screen.

**Before** (`arrow-edge` frame, read as an image): the host's screen shows the guest's OFF-SCREEN arrow at the right edge — the label carries the guest's name and
its distance (read off the frame, quoted in translation: "Kaguya (alt) 128 m") with the arrow mark beside it — while the roster read answers that member
inWorld: true. So the arrow really is drawn, by the overlay, from the same member row the roster answers.

**After** (`arrow-gone` frame, same screen, one host action between): the host kicked that member (the console line read off the frame, translated: "kicked member
76561198863287957"); the roster read then answers **no members at all**, and the
arrow and its label are **gone from the frame** — no marker at the right edge, no residue anywhere on the screen.

**Row 1 PASS**: a peer that leaves takes its arrow with it, read from the frame before/after and the roster in the same batch. **Row 3** passes for the marker half (the arrow was off-screen when thepeer left and no marker remained); its second half — no clone left active in the scene — has no clone census in this batch and is a named residual. **Row 2**
(an arrow returns after a rejoin without a restart) needs a rejoin, which this batch did not drive, and stays a named residual.

**What the code says, and why the report does not reproduce here:** `OnlineUiOverlay.BuildNameplateMarkers` skips every remote whose `ctx.Session.IsRemoteInWorld` is false, and the
marker buffer is CLEARED at the start of every build (`_worldMarkers.Clear()`), so a member that leaves cannot keep a marker from either direction. The frame pair above shows that
behaviour end to end. The remaining candidate for the user's report is the GAME's own render-clone marker (candidate 1 in this ticket), which this batch did not exercise: the
overlay's marker is not the only thing that can sit on the HUD, and a leftover clone would carry the game's own marker rather than a CUO one.

The ticket moves to `review/` with rows 1 and 3 judged, row 2 and the clone-census half named as residuals, and the render-clone candidate recorded for whoever picks the clone
teardown up next.
