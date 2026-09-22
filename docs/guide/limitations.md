# Limitations

English | [中文](limitations.zh.md)

CUO is in active development and has not been released. Some things are deliberately out of scope,
and some are known rough edges. This page is the honest list, so that nobody discovers a boundary by
losing a run to it.

The mod is built around a shared world rather than a shared screen: everyone plays in the host's
world, with their own character, their own inventory and their own view.

## Deliberately out of scope

- There is no host migration: if the host leaves, the session ends.
- There is no dedicated server; a session always runs inside a player's game.
- Mods are not downloaded automatically — everyone installs the same mods.
- There is no generic physics synchronisation, no client-side prediction of other players, and no anti-cheat.
- The game itself has no multiplayer support, so a future game update can break part of the adapter until it is updated.

## Known rough edges

- Details that do not change gameplay (some sounds, particles and animation timing) can differ between two machines.
- Joining a world that is already running is allowed, but both sides must run the same build.
- Searching Chinese text inside the game needs the pinyin search plugin that ships beside CUO in the same repository.

## Where the detail is

The technical list of which mechanisms sync and which do not belongs to the reference layer: the
feature matrices under `docs/features/`, and the open work tracked in `docs/backlog/README.md`.
