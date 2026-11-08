# A guest keeps a dead lobby after the host leaves

- Status: Done — the guest now leaves the dead lobby and joins the next one without a manual Leave (batch `20261108-o`: the guest's log line "Host left the lobby — leaving it and ending the session (save kept) after 3 consecutive checks named it absent.", its state `lobby "0", role "None", active false`; the fresh host's lobby accepted the join and the guest entered its world, `inWorld true, gateWaiting false`). Readings: docs/evidence/acceptance/a-guest-keeps-a-dead-lobby-20261108-o.md
- Priority: High (a player sitting in a dead room sees "connected" and never gets a way out except the Leave control)
- Category: Session / lobby lifecycle
- Source: the batch `20261108-n` fixture; the user's report (translated: "this connected-but-not-in-world has happened before; it may be a bug").

## The reading

Host quit while Steam1 was in its world. Steam1's log:

```
[SessionService] The lobby does not name the host this check (2/3) — the session waits for the next one.
[SessionService] Host left the lobby — ending session (save kept) after 3 consecutive checks named it absent.
[SessionService] Session ended (role Guest kept).
```

Steam1's state afterwards, unchanged for at least a minute: `lobby 109775243845132429`, `role Guest`,
`active false`, `inWorld false`, and the loading line (translated: "waiting for the host to load… the host has not entered the world yet")..
A `join-lobby` to the host's NEXT lobby was refused with `already-in-lobby`; only clicking the overlay's
Leave control freed it (then the join worked and the member entered the world in 12 s).

## The chain

1. The guest's session pump asks the Steam lobby for its members (`SessionPeerMaintenance.CheckLobbyMembers`).
2. When the lobby names no host for `HostMissingChecks = 3` consecutive readable lists, the branch logs
   "Host left the lobby — ending session (save kept)" and calls `_endSession()`
   (`src/CasualtiesUnknownOnline.Runtime/Session/SessionPeerMaintenance.cs:283-284`).
3. `_endSession` is `SessionService.EndSession` → `TeardownSession(leaveLobby: false)`
   (`src/CasualtiesUnknownOnline.Runtime/Session/SessionService.cs:508`): the session content goes
   (presence, active flag, host id), but the LOCAL LOBBY is kept deliberately ("a same-lobby outage — a
   returning host rebuilds the session", `:511-518`) and the Role stays.
4. No host migration exists in the MVP, so the kept lobby is dead: the client sits in a room whose host
   left, the overlay still shows it, and only an explicit Leave frees the identity.

## The fix

The host is GONE from the lobby — that is the dead-lobby case, not a same-lobby outage. Leave the lobby so
`LobbyLeft` → `OnLobbyLeft` → `TeardownSession(leaveLobby: true)` clears the identity: `_steam.LeaveLobby()`
in that branch (`src/CasualtiesUnknownOnline.Runtime/Steam/SteamService.cs:171`).

## Acceptance

1. A unit fact: after the host is absent for the required checks, the peer maintenance asks the Steam
   service to leave the lobby (a fake counts `LeaveLobby`) and does not merely end the session.
2. Live: host + guest in a world, the host quits; the guest's state reads `role None`, `lobby 0` within
   the check window, and a fresh `join-lobby` to a new host succeeds without a manual Leave.

## The acceptance reading (batch `20261108-o`, deployed `0.1.0+b350e821`)

- The host quit with the guest in its world at 12:12:56; the guest's log: `Host left the lobby — leaving it
  and ending the session (save kept) after 3 consecutive checks named it absent.` (12:12:56.278) and
  `Session ended (role Guest kept).` (12:12:56.281).
- The guest's state after it: `lobby "0"`, `role "None"`, `active false` — the identity was cleared (the
  earlier defect left the lobby id and the role behind).
- The fresh host created lobby `109775243848404297`; the guest ran `join-lobby` with no manual Leave and
  read `role Guest`; `start-run` then put it back in the world (`inWorld true`, `gateWaiting false`).
- Limits: one machine, one lobby pair; the leave is Steam's own and is not observable on the host side.
