# Permissions and what they protect

[Documentation](../README.md) > [Internals](README.md) > Permissions and what they protect

---

**After this page** you can say what a declared permission actually enforces, where each check happens,
and what CUO deliberately does not defend against.
[Declare permissions and host commands](../how-to/declare-permissions-and-commands.md) is the
walkthrough for using them; this page is the model behind them.

## Nothing is implicit

A mod declares its capabilities on the `[CuoMod]` attribute, and the default is `ModPermission.None`.
`src/CasualtiesUnknownOnline.Abstractions/ModPermission.cs` states the rule: "CUO never grants a
capability implicitly — a mod states exactly what it needs." The eight flags are:

| Flag | The surface it gates |
|---|---|
| `ReadGameState` | the read-only game-state projection |
| `WriteGameState` | host-persistent mod state |
| `SpawnEntity` | entity and item spawning, tile, structure and liquid placement |
| `SendNetworkMessage` | the mod message channel, sending **and** receiving |
| `RegisterContent` | registering content the framework treats as part of the world |
| `RegisterCommand` | registering host commands |
| `ExecuteHostAction` | additionally required for a command marked as a host action |
| `AccessNativeApi` | the curated native operation registry |

Every flag has a live enforcement point rather than a documentation value: the same file lists them,
"the mod message channel, the host-command domain, the mod-state store, content registration, the
read-only game-state projection, entity/item spawn and tile/structure/liquid placement and the native
operation registry". A refused call is a log line and a negative result, not an exception thrown into
somebody's mod.

## Declared once, checked twice

The declaration is validated by the same pure policy in two places.
`src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModPermissionPolicy.cs` is "the pure permission
judge": discovery validates the local manifest with it, and "the handshake validates the guest's
declared flags with the same policy before admitting it".

Two things it refuses are worth naming:

- **Unknown bits.** A permission that is not one of the eight is not a future-proofing opportunity, it
  is garbage in — the set must be a subset of `ModPermission.All`.
- **Host and state permissions on a local-only mode.** `WriteGameState`, `SpawnEntity`,
  `RegisterContent`, `RegisterCommand` and `ExecuteHostAction` are refused together with `ClientOnly`
  and `Cosmetic`: commands execute on the host and content becomes part of the world, so a mod that
  runs on one client only cannot own either.

## What the host checks before foreign code runs

Three checks stand between a remote caller and a mod's handler, and all three happen before the
handler itself:

- **The join.** The declared mod list is compared against the host's own (the rule in
  [The life of a mod](mod-loading-lifecycle.md)), so a state-bearing mod that a member does not have is
  refused at the handshake instead of failing later.
- **The command window.** A host command request is validated before the handler runs: the name is at
  most 64 characters, at most 16 arguments of at most 256 characters and 4 KiB in total, the sender has
  completed the handshake, the mod and command are registered, and the flags are present. Per guest,
  requests are limited to four per second with a burst of eight.
- **The admission seam.** A command that reached the kernel through the wire passes
  `src/CasualtiesUnknownOnline.Application/Kernel/KernelCommandGateway.cs`, which owns "who may submit
  this" for the mapped path. It checks, in a fixed order, that the command's actor is the submitting
  member, that the declared authority kind is one a member may author, and that a destroy report names
  an item that member may report destroyed.

The seam's scope is part of its contract: it "decides ELIGIBILITY only (who may submit), never what
happens to another player's body, reach or timing… the kernel still owns every domain verdict". The
order matters too, and the file says why: "a refusal that runs earlier can mask one the kernel would
have produced".

## The native API surface is bounded

`AccessNativeApi` does not hand over the game. It reaches a curated registry whose value surface is
deliberately narrow: operation ids are capped at 128 characters, calls at 16 arguments, strings at 4096
characters, byte arrays at 64 KiB and primitive arrays at 1024 elements. Unity and game-assembly
objects and arbitrary object graphs are rejected on both sides of the adapter seam, so a native
operation cannot smuggle a live game object out to a mod.

## Visibility, not sandboxing

This is the part people over-read. A permission gates CUO's own surfaces; it does not put the mod in a
box.

- A mod is ordinary code in the process. It may patch anything it likes — that is what
  `NativeBinding` records, and the attribute says exactly what that declaration is worth: "A declared
  FACT, not a permission: `ModPermission` is what CUO enforces, and CUO enforces nothing here — an
  undeclared binding is undetectable (the framework takes no anti-cheat stance), so this declaration is
  opt-in honesty whose only force is another peer's parity policy."
- The native binding buys visibility to other peers, never stability: what it names belongs to the
  game, and a game update may break it without any CUO decision.
- Permissions make a mod's intent legible and stop accidental reach. They are not a security boundary
  against a mod that means harm.

## What CUO does not defend against

- **A modified client.** Anti-cheat is explicitly deferred — `docs/backlog/future/strict-validation-anti-cheat.md`
  is a low-priority future item, and it records the current honest limit: the authority kind a member's
  command declares carries no member-versus-host information yet. The accepted limitation is stated on
  the judgment side too: a client that lies about its own reach or its own body can reach the host
  ungated, and closing that is a later hardening pass.
- **Save contents.** There is no anti-cheat on what a world archive holds.
- **A mod that misbehaves inside the surface it was granted.** Once a mod has `SendNetworkMessage`, the
  channel checks size and rate, not meaning — the payload is opaque bytes by design.
- **A peer's honesty about its own body.** A judgment on the judging client's own screen cannot be
  verified from outside; that is the deliberate cost of [judgment ownership](judgment-ownership.md).

The reason for this posture is the product: CUO is co-op between people who chose to play together,
not a competitive server. Blocking an honest player is a worse failure at this stage than tolerating a
dishonest one, and the checks above exist to keep accidents and carelessness from becoming desync, not
to catch an adversary.

## The integrity boundary is still the version check

What CUO does enforce absolutely is that two sides are running the same protocol and the same
state-bearing mods. A protocol mismatch ends the join at the handshake; a state-bearing mod that is
missing or version-unequal on either side is refused there too. That check is why a wire change never
has to keep an old shape alive, and it is also the only integrity claim the framework makes about a
peer's build.

## Related reading

- [Who decides what happens to a player](judgment-ownership.md) — what a local verdict costs, and why
- [The life of a mod](mod-loading-lifecycle.md) — where the two permission checks run
- [Declare permissions and host commands](../how-to/declare-permissions-and-commands.md) — the mod-facing walkthrough
- [Register content](../how-to/register-content.md) — the surface `RegisterContent` gates
- [Glossary](../reference/glossary.md) — mod, handshake, protocol version, native, command

---

[Documentation](../README.md) > [Internals](README.md) > Permissions and what they protect
