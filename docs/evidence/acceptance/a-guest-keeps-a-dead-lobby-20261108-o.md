# Acceptance — a guest keeps a dead lobby

Batch `20261108-o`, deployed `0.1.0+b350e82126bd9038cddf69e9998752bda381351c` (acceptance channel).
The defect: a guest whose host quit stayed in the host's lobby with the role and the lobby id kept;
the session had ended, so the overlay showed a dead room and the next join was refused with
`already-in-lobby` until the player clicked Leave (measured 2026-11-08, batch `20261108-n`).

## The fixture

`launch-clients.ps1 -OutDir .acceptance/20261108-o` started the three clients; the host created lobby
`109775243848344797`, the guest and the alt joined, and all three entered the world at 12:12:35.

## The reading

1. The host quit through the driver at 12:12:56 with the guest in its world.
2. The guest's log (12:12:56.278–.281):

   ```
   [SessionPeerMaintenance] Host left the lobby — leaving it and ending the session (save kept) after 3 consecutive checks named it absent.
   [SessionService] Session ended (role Guest kept).
   ```

3. The guest's state after it: `lobby "0"`, `role "None"`, `active false`, `inWorld false` — the identity
   was cleared, where the defect left the old lobby id and the role behind.
4. The host relaunched alone (`-Only host`) and created lobby `109775243848404297`; the guest ran
   `join-lobby` with no manual Leave and read `role Guest`; `start-run` then put it back in the world
   (`active true`, `inWorld true`, `gateWaiting false`).

## What it settles

The host-absent branch leaves the lobby (the `LobbyLeft` path), so a guest is never bound to a room its
host has left, and a fresh join needs the overlay's Leave control for nothing.

## Limits

One machine, one lobby pair; the leave is Steam's own and is not observable on the host side. The
`lobbyIdInput` overlay field keeps the last typed id by design — the state's `lobby` field is the one the
session reads.
