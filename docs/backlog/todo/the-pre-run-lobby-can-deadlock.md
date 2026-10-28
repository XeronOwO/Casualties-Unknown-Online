# The pre-run lobby can deadlock: the host waits for the members, the members wait for the host

- Status: Todo — opened 2026-10-28 from an acceptance session that could not be started again.
- Priority: High — it blocks every multi-client batch, and the player sees a lobby that never departs with no way to tell why.
- Category: Session / lobby / pre-run handshake
- Source: an acceptance session (2026-10-28): both peers dropped out of the world and rejoined the same lobby.

## What the player sees

The host's own gate reads "waiting for the other players to load, the team departs together" (`gateText`), and each member's own gate reads "waiting for the
host to load, the host is not in the world yet". Nobody moves, and nothing times out: the host is waiting for a member report that never arrives, and the
member is waiting for a world that the host will not start until that report arrives.

## The reading that found it

Both sides read in the same session, each from its own client:

- host: `inWorld: true`, `gateText` = waiting for the other players to load (the PRE-RUN gate, not a running world);
- each peer: `inWorld: false`, `gateText` = waiting for the host to load;
- the host's member list then answered empty, so the host did not consider the re-joined peers present at all.

## The first read

Who is supposed to send the "member loaded" fact, and what makes the host drop a member from its list when that member re-joins — `LobbyJoin`/`LobbyStart`
handling on the host side and the peer's own "ready" report. The deadlock is one of: the report is not sent after a re-join, the report is sent but the host
ignores it for a member it still lists as gone, or the host's list drops the member and the gate keeps waiting for it anyway.

## Acceptance

| Row | The check |
|---|---|
| 1 | A member that re-joins a lobby whose run has not started is counted again by the host, and both gates clear without a restart (both clients' `gateText` read in the same batch) |
| 2 | A member that drops while the pre-run gate is waiting does not hold the gate: the host either departs or names the missing member (read on both ends) |
| 3 | The same session, restarted from scratch, departs on the first try — so a fresh lobby is not the only way out |
## Where the gate lives (read 2026-10-28, after the report)

The gate is CUO's own, not the game's: `SessionWaitText` words every wait from a `SessionWaitState` (`SessionWaitKind.HostLoading` / `MembersLoading`,
both keyed into the catalogue as `wait.*`), and `StartGateCoordinator` decides when a wait clears. So the deadlock is in our own start gate: those two kinds
are waiting for each other's fact, and the member list that should clear `MembersLoading` came back empty after the peers re-joined. The first read is therefore
inside `StartGateCoordinator`: what it counts for `HostLoading` and `MembersLoading`, and what removes a member from the set it counts.
## The facts the gate sums (read 2026-10-28, last)

`StartGateCoordinator.WaitingState` gathers and `SessionWaitPolicy.Decide` decides; the gathered facts are `_session.Members` (each member's `Handshaken` /
`InWorld` / display name) and `_session.IsRemoteInWorld(host)`, plus the world's start-gate countdown. So the deadlock is facts, not wording: the host's`MembersLoading` means some member entry still reads `InWorld == false`, and the peer's `HostLoading` means its own `IsRemoteInWorld(host)` reads false.
Both can be true at once if a re-joined member's `InWorld` is never set back to true on the host (and the host's world edge never reaches the peer). The next two
reads are therefore `SessionWaitPolicy.Decide` (the exact predicate order) and the single place that sets `member.InWorld` — one write, or one missing write, not a
missing timeout.
## The decision, read end to end (2026-10-28 23:55)

`SessionWaitPolicy.Decide` has three arms and the first one is unconditional for a host: `if (input.IsHost) return MembersLoading(waiting, gateRemaining)` — so the
host's loading line is ALWAYS `MembersLoading`, and what actually lets the team depart is `StartGateRemainingMs` (the world's start-gate countdown), not the
names. The member arms are `!HostInWorld → HostLoading` (the peer's own `IsRemoteInWorld(host)`) and then `OthersLoading` for named third players.

So the deadlock is two separate stale facts, not one: the host's countdown never reaches zero (nothing in the read above names WHAT holds it), and each peer's
`IsRemoteInWorld(host)` stays false after it re-joined. `MemberPresence.InWorld` is written in exactly four places — `HandshakeHandler` (host, the peer's
reported scene state, two arms), `SceneStateHandler` (host, on a scene-state message), `HandshakeAckHandler` (guest, the host's state) and `EntitySyncService` (guest,
set true on a sync) — and `Remove(steamId)` drops the entry outright, so a re-join after a drop depends on one of those two host-side writes running again.
The next read is therefore the host's re-join path in `HandshakeHandler`/`SceneStateHandler` (does a re-joining peer send a fresh scene state?) plus the source of
`StartGateRemainingMs` — a countdown that never clears is the other half.
## The gate's own release rule, and the symptom that contradicts it (2026-10-29 00:05)

`WorldStartGate` states its lifecycle in its own words: the host's click arms the gate, **every confirmed member's InWorld report releases its slot**, and a
**30 s fallback releases the gate**. `StartGateCoordinator.Update` calls `_world.MaybeForceStartGate()` → `WorldStartGate.PumpTimeout()` whenever the host's gate is
active, and `WaitingForReady` for a host is exactly `_world.StartGateActive`.

The session read the CONTRADICTION: the host's line already showed its countdown at zero — `wait.members_loading` renders the remaining seconds and it read
`0` — while `StartGateActive` was still true and no departure
happened. So either the fallback never fires at `RemainingMs == 0` (an off-by-one/`<` vs `<=`), or `PumpTimeout` is not reached in that frame order. That
is the next read, and it is one function: `WorldStartGate.PumpTimeout` plus the arm/release bookkeeping beside it. The member side (`IsRemoteInWorld(host)`) is
a separate stale fact and stays as recorded above.
## The log names it: the handshake never COMPLETES (2026-10-29 00:20)

The host's own log from the stuck session, in order:

- `Start gate armed — waiting for 2 member(s)` … `Start gate released — everyone is in the world`: the GATE WORKS. It armed for two members and released.
  The host's `(0 s)` line was the generic host arm of `SessionWaitPolicy.Decide` (`if (IsHost) return MembersLoading`), not a blocked gate.
- `Peer 76561198863287957 is retrying its handshake — the previous ack was not delivered` — five times, and the same for `76561199526807662`. That warning
  fires in `HandshakeHandler`'s else branch, i.e. the host has the member but `Handshaken` is still false because the third leg never arrived.

So the real defect is narrower than the ticket's title: **the handshake does not complete after a re-join**, and both peers sit in `HostLoading` because the ack's
`SceneState` never reaches them. The host receives their handshakes (it logs them), and `SessionPeerMaintenance` re-sends acks while a member stays unconfirmed — so
host→peer delivery is the broken direction. `HandshakeAckHandler` on the peer does the right thing when the ack arrives (sets `InWorld`/`Handshaken`,
activates the session, fires the scene edge, sends the `AckAck`), which leaves the delivery path as the thing to read: the sender's per-peer target resolution after a
re-join, and whatever the peer's packet pump refuses before the handler (latest generation / lazy channel).
