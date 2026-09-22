# Overview

English | [中文](overview.zh.md)

CUO adds multiplayer to a game that has none. It does that by owning the gameplay facts itself: a
typed deterministic kernel holds the authoritative state of the world, while the Unity scene, the
network caches and the save files are views built from it.

The mod is split into a stable runtime and a replaceable adapter, so that a game update breaks the
adapter instead of the protocol, the session logic and the mod API.

## The shape of the mod

- A **host** runs the authoritative kernel; **guests** send their actions and apply what the host commits.
- The **Runtime** owns the protocol, the session state machine, mod loading, serialization and logging.
- The **Game Adapter** is the only layer that knows the game's private types, and it absorbs game-update churn.
- The **GameState kernel** owns the typed state: items, players, entities, world, fluids, and the rules that change them.
- **Projections** turn committed state into Unity objects, remote clones, wire batches and saves.

## A command's journey

A player does something. The client turns it into a command and sends it. On the host the kernel
decides whether to accept it, produces a committed batch, and broadcasts that batch; every client
then reduces it and projects the result. Nothing is authoritative until it has been committed.

## Why deterministic

Every client has to reach the same world state from the same inputs, so the kernel never reads a
clock, a random number or a Unity object directly; those arrive as explicit inputs. Replay is then
possible, and replay is how the harness verifies behaviour without running the game.

## Where to go next

[Layers](layers.md) explains what each project may reference, and [sync model](sync-model.md)
explains who is allowed to decide what. The [current architecture](../architecture/current.md) is
the full design.
