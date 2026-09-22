# Sync model

English | [中文](sync-model.zh.md)

CUO keeps one authoritative copy of the world and many views of it. The host owns the authoritative
copy, each client decides what its own player is doing, and the host settles conflicts between claims
that cannot both be true.

The model exists to keep a session playable on an ordinary connection: a decision that needs a round
trip before the player may act makes the game feel broken, so as little as possible waits for one.

## Who decides what

- **The acting client** decides what its own player does, from its own view and its own time.
- **The host** owns the authoritative state, the order of committed changes, and the arbitration of a conflict.
- **Every client** applies what the host commits, so all views converge on the same facts.
- **Presentation** is local: particles, some sounds and animation timing are not synchronised.

## Conflict arbitration

When two claims cannot both hold — the same item picked up twice, the same container emptied twice —
the host accepts the first claim it receives and rejects the second with a typed reason. The rejected
side learns why it lost instead of silently losing its action.

## Corrections

A view that disagrees with the committed state is corrected, and the correction is visible. The mod
never lets two machines diverge quietly, because an undetected divergence costs far more to diagnose
than a visible snap-back.

## What is deliberately not here

There is no client-side prediction of other players, no rollback of the local player, and no
anti-cheat: each of these is a design with its own cost, and none of them is needed for the co-op
case CUO targets today.
