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
