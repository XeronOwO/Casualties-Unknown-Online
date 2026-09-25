# Glossary

[Documentation](../README.md) > [Reference](README.md) > Glossary

---

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
- **Domain** — one slice of the kernel's state with its own typed model and rules, such as items or fluids.
- **Command** — a typed request for something to happen; it may be refused.
- **Event** — a fact the kernel accepted, which every machine will follow.
- **Batch** — one atomic set of accepted facts.
- **Checkpoint** — a complete copy of the world state, used when someone joins or reconnects.
- **State stream** — high-frequency field updates that may be dropped and superseded.
- **Snapshot** — a point-in-time copy of state.
- **Projection** — a view built from the authoritative state, such as the objects you see.
- **Envelope** — one framed message of the kernel protocol: a command, a committed batch, a checkpoint or a state stream.
- **Effect** — what an outer layer must do after a batch, such as move an object or play a sound; derived, never stored.
- **Reduce** — applying a batch's accepted events to the authoritative state, the same way on every machine.
- **Revision** — the kernel's own increasing order number; not a mod version.
- **Epoch** — the identity of one run; traffic from an old run is rejected.
- **Deterministic** — the same inputs produce the same result.
- **Vitals** — the projected state of one player's body: health, hunger, thirst, stamina, energy, temperature.
- **Read model** — state meant for reading. A reading is what CUO last heard, never a verdict.
- **Judgment ownership** — whose machine decides what happens to a player: that player's own client, on its own screen and timeline; the host keeps the world and the arbitration.
- **Rollback** — undoing a locally applied action after the host's arbitration refused the claim.
- **Admission** — the host's decision whether a member's submission may reach the kernel at all.
- **Payload** — the opaque bytes a message or a content definition carries; CUO never reads inside them.

## Joining and versions

- **Handshake** — the join-time exchange of version and identity.
- **Protocol version** — the number both sides compare when a guest joins; a mismatch ends the join.

## Mods and the code

- **Mod** — third-party content loaded through the mod API.
- **Permission** — a capability a mod declares on its attribute; CUO grants nothing implicitly.
- **Network mode** — a mod's contract with the session: which members must have it, and where it runs.
- **Lifecycle** — the calls the framework makes on a mod, from bind to dispose.
- **Native binding** — the game surface a mod declares it patches; a declared fact, never a promise or a grant.
- **Patch** — one change applied to existing code; a plugin is not a patch.
- **Adapter** — the only layer that knows the game's private types and absorbs game updates.
- **Runtime** — the stable CUO layer: protocol, session, mod loading.
- **Native** — the game's own code and behaviour.
- **World archive** — CUO's save layout: one folder per world, with cuts and backups.
- **Lease** — the record naming which process is writing a world folder.
- **Mod state** — the small key/value table a mod keeps in the host's save, so its data survives a restart.
- **Content id** — the canonical `namespace:path` address of one registered content definition.
- **Namespace** — the mod-declared first half of a content id; it is what keeps two mods' ids apart.
- **Content kind** — the kind a definition is registered under, such as `item`, `recipe` or `tile`.
- **Schema version** — the version a mod stores next to its own opaque payload; the framework carries it and never migrates it.
- **Tombstone** — a recorded refusal that stops the same creation being retried.

## Contracts and modification

- **Contract** — a surface CUO promises to keep; every other surface is an implementation.
- **Implementation** — a surface with no promise: a mod may patch it, and it may change shape in any commit.
- **Public surface** — every public type and member a caller can reach; the reviewed list of them is the baseline.
- **Stability level** — what a public surface declares with `[ApiStability]`; a surface without the attribute is `Stable`.
- **Tier** — how a mod binds CUO: the contract, CUO's own implementation, a declared native binding, or an unmanaged plugin.
- **Promotion funnel** — how a need becomes an API: a patch, then a second consumer, then an experimental surface, then a stable one.
- **Diagnostics** — the log lines an author may rely on; their absence is a bug worth reporting.
- **Framework core** — a capability the framework's own operation needs; it ships with the plug-in.
- **Satellite mod** — a game-facing feature that still makes sense with CUO uninstalled, so it is its own mod.
- **Repository tool** — a development or verification tool that sits outside the plug-in's dependency graph.
- **Reusable component** — shared machinery that waits for its second consumer before it is extracted.
- **Enemy** — a hostile creature the game's own AI drives; its movement is host-authoritative.
- **Runtime spawn** — an entity created during play rather than by world generation.
- **Attack announcement** — the host publishes that an attack happened, and the client it lands on judges what it did.

## Interface and comfort

- **Online panel** — the CUO overlay that creates or joins a session.
- **Interaction panel** — the CUO panel opened with the configured key, `F6` by default.
- **Host rules** — the gameplay switches the host owns.
- **Pinyin search** — lets the game's search boxes match Chinese by pinyin.
- **Configuration profile** — a named snapshot of every setting, saved beside the config file.
- **Hot reload** — a settings change taking effect without restarting the game.

## Working on CUO

- **Gate** — a repository check that refuses a change which breaks a rule; the gates run as tests.
- **Baseline** — a recorded, reviewed state that a gate compares the current tree against.
- **Evidence** — the recorded proof for a claim: a file, a command result or a runtime trace.
- **Review** — the independent check a change passes before it is delivered.
- **Process record** — a document that records what a cycle did, such as the backlog, the evidence files or a decision register; it is not part of the human documentation.
- **Breadcrumb** — the navigation line at the head and the tail of a page, naming where the page sits.
- **Seam** — a named boundary where one side may be checked or replaced.
- **Port** — a narrow interface a capability is reached through; the adapter is a composition of capability ports.
- **Feature matrix** — the table of game features and whether each one is synced; the CSV file is the machine copy.

## Related reading

- [Reference](README.md) — the lookup pages
- [Start here](../start/README.md) — the first-run line
- [Term registry](../../standard/terminology.txt) — the file every Chinese page takes its wording from

---

[Documentation](../README.md) > [Reference](README.md) > Glossary
