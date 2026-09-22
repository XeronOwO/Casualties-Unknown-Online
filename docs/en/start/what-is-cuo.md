# What CUO is

[Documentation](../../README.md) > [Start](README.md) > What CUO is

**After this page** you can say what CUO adds to the game, what one session looks like, and which
problems it deliberately does not solve. There is nothing to install or read first.

## The short version

*Casualties: Unknown* (Demo) has no multiplayer. **Casualties Unknown: Online (CUO)** adds it: one
player's game owns the world, the others join, and everyone plays the same
[run](../reference/glossary.md) — the same map, the same loot, the same deaths.

CUO is two things at once. It is a mod you install and play, and it is a base for other mods: the mod
surface lives in `CUO.Abstractions`, and the multiplayer machinery under it is written to be reused
rather than rebuilt.

## One session, end to end

1. The [host](../reference/glossary.md) starts the game as usual and opens a session from the CUO
   online panel. That game becomes the world everyone plays in.
2. A [guest](../reference/glossary.md) joins through a Steam [lobby](../reference/glossary.md); for
   players who cannot use Steam there is a direct-IP mode.
3. From then on there is one world: the map, the items on the ground, the enemies, the containers and
   the deaths are shared.
4. When the host stops hosting, the session ends. The world itself is saved and can be opened again
   later.

## Who decides what

Making every action wait for the host would make the game feel broken, so CUO splits the decision:

- **Your own game decides what you do.** Picking something up, using a tool, taking a step: your
  machine decides it with your view and your timing, and does not wait for a round trip.
- **The host owns the facts.** It holds the authoritative world, the order in which changes were
  accepted, and the last word when two claims cannot both be true — two players grabbing the same
  item, two players emptying the same container.
- **A conflict is answered, not swallowed.** The host accepts the claim that arrived first and answers
  the other with a reason, so the losing side knows why it failed.
- **Corrections are visible.** When a machine disagrees with the committed world it is pulled back;
  two machines never drift apart quietly.

## What CUO deliberately leaves out

Each of these is a design of its own with a real cost, and a co-op session is playable without them.

- **No dedicated server** — the session lives in the host's game, not on a machine in a data centre.
- **No host migration** — if the host leaves, the session ends.
- **No automatic mod installation** — everyone installs the mods they want.
- **No prediction for other players, no rollback for you** — your own actions are local; another
  player's character shows what the host has committed.
- **No anti-cheat** — CUO trusts the machines in the session.

## What you can build on it

If you are here to write a mod rather than to play:

- the mod surface is `CUO.Abstractions`: a mod declares itself with an attribute, gets a lifecycle,
  and may send its own messages, register content and add a panel to the game's UI;
- CUO's own implementation can be patched by name, but only the abstractions are a promise — the
  [reference](../reference/README.md) will state which tier is which;
- the [adapter](../reference/glossary.md) is the only layer that knows the game's private types, so a
  game update is absorbed there instead of breaking every mod at once.

## Related reading

- [Start here](README.md) — the rest of the first-run line
- [Glossary](../reference/glossary.md) — every word this documentation uses, in everyday language
- [Reference](../reference/README.md) — what the lookup pages will hold

[Documentation](../../README.md) > [Start](README.md) > What CUO is
