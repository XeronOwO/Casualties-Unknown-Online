# Session lifecycle: from a lobby to a world

How a party comes together, plays, leaves and returns.

## What the player does

One player opens a lobby and starts a run; the others join the lobby and wait; when the host starts the run, everyone
enters the world together. Players leave and rejoin, save and continue a run, and finish a run. A player expects a
rejoined party to find the same world, the same items and the same progress.

## Rules and states

- **Closing the room is a safe exit.** A host closing the room ends the session for the party cleanly rather than dropping them into a broken world: the case has its own record (a "close-room safe exit" selfcheck) and belongs to this page's lifecycle rules.- The lobby is a membership: who is present, whose Steam identity, and which host rule set applies. Membership and the
  world are two different facts — joining the lobby does not place a body in the world, and re-entering a world is done
  from the running client, never by restarting it.
- The run is the host's world. A member's character is restored by the client that can apply it, on a continue, from the
  stored keys the present peers claim.
- **Every run entry the game has asks the same first question.** `PreRunScript.StartRun` and `PreRunScript.LoadRun`
  (`PreRunScript.cs:273-300`) both refuse while `PlayerPrefs.GetInt("didbasiccourse") == 0`, showing the tutorial warning and
  hiding the run-settings screen instead — so "start a run", "continue a run" and the tutorial are one gate in the game, and a
  CUO path that offers a run without passing it is offering something the game does not.
- **Acquiring a lobby leaves the current one first, for create and join alike.** `Runtime/Steam/LobbyLifecycle.cs`: "Steam
  requires a leave before a fresh create — a re-create without leaving strands the old lobby: the host's friends keep joining
  the dead lobby while the host is already in the new one (observed live: F8 re-create left the old lobby with a residual member
  and the guest could not connect)". Join uses the same explicit leave, because "the official JoinLobby docs do not promise an
  automatic leave of the previous lobby, so CUO never depends on one".
- A save is a multi-world repository: one archive per world, keyed by an immutable world identity, and the display name is
  free to change.
- A save holds every member present at the cut; a restore is repair mode with minimal loss, per entry, and an entry whose
  content no longer exists is skipped with a recorded reason rather than failing the whole cut.
- The save is independent of the game's own save file: CUO never writes it and never reads it.
- A mid-run cut is armed by the event that asks for it and taken by the host's pump at one seam, never inside the
  callback that asked for it.
- **The start gate is a wait with one forced release, and three freezes while it holds.**
  `GameAdapter/Run/StartGateCoordinator.cs`: it is "the 'everyone loads together' wait — host forces the gate after 30 s",
  and "both sides freeze the local player while it holds (timeScale 0 + movingAllowed locked + the loading screen kept visible
  for a guest) and restore everything on release".
- **A wait may only say what the client can SEE.** `Run/SessionWaitPolicy.cs`: "a wait may only say what the client holding the
  facts can SEE, and the roster it prints must be the set the gate is actually waiting for"; the host owns the gate and its
  countdown, while "a member's row arrives already in-world, so a member waiting on a third player usually has nothing to name,
  and the host's flag being false covers the menu, loading and 'no report yet' alike" — the wording is built to say no more
  than those facts support.
- **The gate's panel is a corner strip over the frozen world, never a blackout.** `Plugin/StartGateOverlay.cs`: the waiting text
  sits "in a translucent panel pinned to the BOTTOM-RIGHT corner (the loading screen's own info slot) over the LIVE frozen
  world. No full-screen blackout: the gate freezes the world behind it and the panel keeps the wait readable without turning the
  wait into 'a black screen' (the original black-texture attempt)" — and it is drawn at the point in the frame the HUD would
  have been drawn, "with the start gate holding the HUD back".
- State that is in flight at a cut is classified per class — captured, resolved before the save, or dropped — and never
  silently kept or silently lost.
- **Presence is keyed by the account, and three one-shot messages converge on a repeat.**
  `MemberPresenceTable.cs` keeps the roster "keyed by SteamId, stable across reconnects", with each member's scene state.
  `SessionControlConvergence.cs` records why three of them needed their own re-report (sync-coverage rows R3/R4): the lazy-P2P
  swallow window "drops frames the sender never learns about, and three of them had no re-report at all: a swallowed
  `SceneState` report left the host without the member's InWorld edge (no world-entry fan-out, no entity sync, the guest's own
  60 s start-gate valve back to the menu as the only escape), a swallowed `WorldReady` left the guest waiting at the gate
  although the host had released it, and a swallowed `HandshakeAckAck` left the host's member unconfirmed for the whole
  connection".

## Refusals

| Condition | The player reads |
|---|---|
| a member tries to act in the world before it is placed | the world interaction is refused; the placement is retried |
| a continue finds a stored key no present peer claims | that entry waits for its owner rather than being given to another |
| a restore entry's content no longer exists | the entry is skipped and the skip is reported |

- **World time accelerates only when the party sleeps, and the initiator's own clock moves first.** Manual world-time acceleration is gated on being asleep (the ticket's own title), and its last accepted shape is LOCAL FIRST: the initiator's own client applies the accelerated rate at once and the others follow. Decision 159 records the local-first half, and the later world-time tickets its consequences.
- **The authoritative speed is re-broadcast, not trusted once.** `WorldTimeMsg` is "sent when the host's speed changes, as the answer to a guest's request (even when the speed did not change — the initiator is waiting for the verdict), when a member (re)enters the world, and every 5 s as an idempotent self-heal" — because direct `Time.timeScale` writers "and local-only effects can move a side away", and the next broadcast brings it back.
- **Entry repair rides the guest's readiness window, not a blind timer.**
  `src/CasualtiesUnknownOnline.Runtime/Session/World/EntryRepairSchedule.cs`: the entry group and the tables re-sent on a
  member's InWorld edge are "one-shot over a reliable transport whose retry only covers a lost frame while the peer is
  reachable", and the documented lazy-P2P swallow window "drops everything sent before the session is up", so the heal
  "rides the guest's own readiness window instead of a blind timer" — `SessionControlConvergence` re-asserts the member's
  scene report every 5 s until the entry-group completion marker AND the start-gate release are both in.

## Who judges

The host owns the run, the world and the cut. A member owns its own body and its own character data; on a continue the
stored character is handed to the client that can apply it.

## What travels

Membership and host rules; the world's state as the kernel streams it; the checkpoint at a cut; the stored character data
and world diff a restore delivers.

## What the player sees and hears

A lobby screen listing the party; the world appearing when the host starts the run; the same world again after a
continue, with each player back on their own body.

## Records

Decisions 162–170 (save identity, salvage, repository, independence from the native save, cut arming, in-flight classes),
242 (layer ownership). Human pages: `../../en/internals/state-and-snapshots.md`, `../../en/internals/save-archive.md`.

## Gaps

- The exact member-facing steps of a rejoin (what a member sees while the host has not started the run) are not recorded
  here yet.
