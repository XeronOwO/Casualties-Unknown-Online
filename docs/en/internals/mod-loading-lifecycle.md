# The life of a mod

[Documentation](../README.md) > [Internals](README.md) > The life of a mod

---

**After this page** you can say when the framework finds a mod, what it calls and in what order, and
what happens to a mod that crashes or that another player does not have.
[Your first mod](../start/your-first-mod.md) is the walkthrough for writing one; this page is the
machinery underneath it.

## Discovery is the first update frame

A mod is not found in the framework's own startup. BepInEx loads plugins one at a time, loading each
and then calling its `Awake`, so a scan during CUO's own `Awake` would miss every plugin loaded after
it. `src/CasualtiesUnknownOnline.Abstractions/ICuoMod.cs` states the consequence: the framework
"discovers the mod via its `CuoModAttribute` (first update frame — BepInEx loads plugins one by one, so
the framework's own `Awake` would miss plugins loaded after it)".

The scan runs once per process: the first update frame sets a flag and never scans again. Discovery is
therefore complete before a session can start, and a guest's join is never checked against a half-built
mod list.

## What gets accepted

- The `[CuoMod]` attribute **is** the manifest source — the framework builds the `ModManifest` from it,
  so a mod never declares its metadata twice
  (`src/CasualtiesUnknownOnline.Abstractions/CuoModAttribute.cs`).
- A candidate must implement `ICuoMod` and have a public parameterless constructor; the framework
  instantiates it itself.
- `NetworkMode` defaults to `Unspecified` and is **rejected** at discovery: "a mod that does not state
  its network mode does not load". Fail-closed, so a forgotten declaration can never silently become
  the most permissive mode.
- Dependencies are honoured: discovery returns the discovered list in dependency order (a topological
  sort), so a mod's dependencies load before it. A missing target, a duplicate id, a self-dependency, a
  cycle, a reserved or duplicated content namespace, or an invalid permission set rejects that candidate
  **with a log**, and a mod that depended on a rejected one is skipped with it.
- One broken mod never blocks the others. `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModRegistry.cs`
  states it: "A rejected candidate is skipped WITH a log — one broken mod never blocks" the rest.

## The order of the calls

```text
discovery frame:  Bind  ->  Initialize  ->  Start
every frame:      Update
shutdown:         Stop  ->  Dispose        (reverse load order)
```

`Bind` is the mod's one chance to receive its context: a session snapshot, the message channel, its own
logger, and the API surfaces it declared permissions for. `Bind`, `Initialize` and `Start` all run in
the discovery frame; from then on the mod's `Update` is pumped every frame on the Unity main thread.
Shutdown walks the loaded list backwards, so the mod loaded last stops first.

## What a mod is handed

Every loaded mod gets its own `ModContext` — the per-mod face of the framework. The stores behind it
(`ModStateStore`, `ModDataStore`, `ModStatusStore`, the building runtime, the command service) exist
once for the process, and the context is the filtered view a mod reaches them through. A mod never sees
another mod's context, and the framework keeps the loaded table, the pump and the persistence to
itself: `ModService` is a wiring point whose own comment calls it "a thin wiring point instead of a god
object".

## A crashing mod is isolated

Every call into mod code goes through the same guard. A throw in `Bind`, `Initialize`, `Start` or
`Update` is caught and logged — "isolated, the pump continues" — and only that mod is affected: a mod
that fails to load is skipped while the rest load, and a mod that throws during a frame does not stop
the other mods' update that frame.

The same isolation covers the mod-carrying traffic. An incoming mod message is rate-limited per sender,
checked against the payload cap, dropped when no local mod has that id, and dropped when the local mod
did not declare the network permission — each with its own log line rather than an exception thrown into
somebody's mod.

## What another player has to have

A mod's `NetworkMode` is its contract with the session, and the host enforces it at the join
[handshake](../reference/glossary.md). Quoted from
`src/CasualtiesUnknownOnline.Runtime/Session/Handlers/HandshakeHandler.cs`:

```text
RequiresAllPlayers/Synchronized/Authoritative missing on either side, or
version-unequal, → reject (the host cannot arbitrate state the member
lacks or claims with a different version); HostOnly is host-side only (a
guest lacking it passes); ClientOnly/Cosmetic differences pass (local
surfaces).
```

A malformed list — an empty or duplicated id, an unknown network mode, permissions that do not fit the
mode, a state-bearing version that is not parseable — is rejected too. And a check that cannot run yet
is not a pass: if discovery has not finished on the host, the join is refused as "not checked yet"
rather than allowed, and the guest's retry is checked properly a second later.

## What the lifecycle does not do

- **No hot reload.** There is no "load this mod now" call: discovery runs once per process, and a mod
  file added to the game afterwards is not picked up in the same run.
- **No per-mod unload.** `Stop` and `Dispose` belong to framework shutdown, and they walk the whole
  loaded list.
- **No session restart of a mod.** A mod is not re-bound when a session ends; the context fires
  session-ended events, and the loaded table stays as it was.
- **No state reload mid-process.** A mod's persisted table is read once, before discovery, so `Bind`
  can already read it; the in-memory copy lives for the process.

## Related reading

- [Your first mod](../start/your-first-mod.md) — the two files a mod needs
- [Permissions and what they protect](permissions-and-security.md) — what a declared permission buys
- [Declare permissions and host commands](../how-to/declare-permissions-and-commands.md) — the mod-facing flags
- [The four envelopes](envelope-protocol.md) — the join handshake in context
- [Glossary](../reference/glossary.md) — mod, adapter, runtime, handshake, mod state

---

[Documentation](../README.md) > [Internals](README.md) > The life of a mod
