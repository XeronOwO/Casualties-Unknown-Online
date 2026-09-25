# Layers

English | [中文](layers.zh.md)

The solution is split so that a game update has one place to break. The Runtime is stable CUO code,
the Game Adapter is the only layer that touches the game's private types, and the Abstractions
project is the small surface a mod is allowed to depend on.

The dependency direction is enforced rather than merely documented: an architecture gate reads the
solution and refuses a project reference that points the wrong way.

## The projects

- **Abstractions** — the public mod-facing contract, and the only surface with a stability promise.
- **GameState** — the typed deterministic kernel; it references no other CUO project.
- **Protocol** — the wire DTOs, the codecs and the versioning.
- **Application** — the command admission seam and the kernel replication surface.
- **Runtime** — dependency-injection composition, the session state machine, networking, projections, the mod API.
- **GameAdapter** — the game-facing implementation: hooks, captures, native writes.
- **Plugin** — the thin BepInEx entry point.

## What you may patch

`Runtime` and `GameAdapter` are implementations: a mod may patch them, and a patch that breaks after
a mod update is the mod's problem. Only `Abstractions` is a promise, and even there the surface is a
recorded baseline — adding or removing a member is a deliberate, reviewed act rather than a side
effect of a refactor.

## The adapter boundary

The runtime declares the contract and the adapter implements it. That is why the adapter can absorb
a game update — a renamed game type, a method that moved — while the protocol, the session logic and
the mod API stay untouched.

## Where the detail is

`docs/contracts/abstractions-api-baseline.txt` is the recorded public surface,
`docs/api/advanced-modification-policy.md` defines the stability levels, and the
[current architecture](../architecture/current.md) holds the full dependency diagram.
