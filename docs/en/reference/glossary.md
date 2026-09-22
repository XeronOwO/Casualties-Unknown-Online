# Glossary

[Documentation](../../README.md) > [Reference](README.md) > Glossary

Every word this documentation uses, explained in everyday language. A term is linked to this page on
its first use in a page. The exact Chinese rendering of each term is registered in
[`../../standard/terminology.txt`](../../standard/terminology.txt).

## A session

- **Host** — the player whose game owns the world. There is no dedicated server.
- **Guest** — a player who joined the host's world.
- **Client** — one machine in a session. It is a machine, never a role: the roles are host and guest.
- **Session** — one host together with the guests connected to it.
- **Lobby** — the Steam room that carries the group before and during a session.
- **World** — the saved place you play in; it survives between sessions.
- **Layer** — one level of the world; a run moves through layers.
- **Run** — one playthrough, from entering the world to dying or finishing it.

## What happens to players

- **Carry** — one player physically carrying another.
- **Unconscious** — alive but unable to act, waiting for help.
- **Revive** — bringing an unconscious or dead teammate back at a trader.
- **Respawn** — the game putting a dead player back into the world.
- **Permadeath** — the setting where death is final.
- **Nameplate** — the label above a character.
- **Off-screen arrow** — the marker that points at a teammate outside your view.

## How the state works

- **Kernel** — the part of CUO that owns the authoritative state.
- **Command** — a typed request for something to happen; it may be refused.
- **Event** — a fact the kernel accepted, which every machine will follow.
- **Batch** — one atomic set of accepted facts.
- **Checkpoint** — a complete copy of the world state, used when someone joins or reconnects.
- **State stream** — high-frequency field updates that may be dropped and superseded.
- **Snapshot** — a point-in-time copy of state.
- **Projection** — a view built from the authoritative state, such as the objects you see.
- **Revision** — the kernel's own increasing order number; not a mod version.
- **Epoch** — the identity of one run; traffic from an old run is rejected.
- **Deterministic** — the same inputs produce the same result.

## Joining and versions

- **Handshake** — the join-time exchange of version and identity.
- **Protocol version** — the number both sides compare when a guest joins; a mismatch ends the join.

## Mods and the code

- **Mod** — third-party content loaded through the mod API.
- **Patch** — one change applied to existing code; a plugin is not a patch.
- **Adapter** — the only layer that knows the game's private types and absorbs game updates.
- **Runtime** — the stable CUO layer: protocol, session, mod loading.
- **Native** — the game's own code and behaviour.
- **World archive** — CUO's save layout: one folder per world, with cuts and backups.
- **Lease** — the record naming which process is writing a world folder.

## Interface and comfort

- **Online panel** — the CUO overlay that creates or joins a session.
- **Interaction panel** — the CUO panel opened with the configured key, `F6` by default.
- **Host rules** — the gameplay switches the host owns.
- **Pinyin search** — lets the game's search boxes match Chinese by pinyin.

## Working on CUO

- **Gate** — a repository check that refuses a change which breaks a rule; the gates run as tests.
- **Baseline** — a recorded, reviewed state that a gate compares the current tree against.
- **Evidence** — the recorded proof for a claim: a file, a command result or a runtime trace.
- **Review** — the independent check a change passes before it is delivered.
- **Process record** — a document that records what a cycle did, such as the backlog, the evidence files or a decision register; it is not part of the human documentation.
- **Breadcrumb** — the navigation line at the head and the tail of a page, naming where the page sits.

## Related reading

- [Reference](README.md) — the lookup pages
- [Start here](../start/README.md) — the first-run line
- [Term registry](../../standard/terminology.txt) — the file every Chinese page takes its wording from

[Documentation](../../README.md) > [Reference](README.md) > Glossary
