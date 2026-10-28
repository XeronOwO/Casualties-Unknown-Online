# A disconnected member leaves his arrow on the HUD

- Status: Todo — opened 2026-10-28 from the user's report (translated): "why does the arrow marker of a teammate still show after the guest disconnected?".
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
