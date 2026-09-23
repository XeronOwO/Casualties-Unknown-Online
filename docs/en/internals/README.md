# Internals

[Documentation](../README.md) > Internals

---

Why CUO works this way, in `docs/en/internals/`: the shape of the system, the rules that decide who owns
what, the protocol and state machinery, saves, the adapter boundary, and what the framework does and
does not protect. Every page here explains a design; the steps for doing something are in `../how-to/`
and the exact contracts are in `../reference/`.

1. [The shape of CUO](architecture-overview.md) — the kernel, the layers above it, and one writer per
   fact.
2. [Who decides what happens to a player](judgment-ownership.md) — judgment ownership, arbitration, and
   why latency is never an input.
3. [The four envelopes](envelope-protocol.md) — commands, committed batches, checkpoints and the state
   stream, and how a guest catches up.
4. [State streams and snapshots](state-and-snapshots.md) — what a client may read, and why a reading is
   never a verdict.
5. [The life of a mod](mod-loading-lifecycle.md) — discovery, the call order, failure isolation, and what
   another player has to have.
6. [How a world is saved](save-archive.md) — the world archive, the two cut seams, and the transaction
   behind a save.
7. [The adapter and a game update](adapter-and-updates.md) — the one layer that knows the game, and how
   it absorbs churn.
8. [The game behind the adapter](game-internals.md) — the parts of the game CUO has to know about, and
   what a game update can move.
9. [Permissions and what they protect](permissions-and-security.md) — what a declared permission
   enforces, and what CUO deliberately does not defend against.

The last page of the line is [Contributing](../contributing/README.md): how to build, test, review and
document a change to any of this.

---

[Documentation](../README.md) > Internals
