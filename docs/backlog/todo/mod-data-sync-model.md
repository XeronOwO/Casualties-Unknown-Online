# Mod data sync model

- Status: Todo
- Priority: Medium
- Category: Mod ecosystem / architecture / multiplayer data

Define and implement a clear model for **mod data synchronization** that
distinguishes:

1. **Local-only mod data**: presentation/configuration/debug state that may
   differ per player, does not affect shared gameplay, and must never cross
   the wire.
2. **Shared/public mod data**: state that is either replicated to all peers,
   host-authoritative, or otherwise part of the cooperative simulation and
   must obey CUO's authority and sync rules.

## Why this exists

CUO already has the low-level building blocks:

- `IModNetwork` — opaque mod messages, star topology, host broadcast.
- `IModCommands` — host-authoritative command execution with directed results.
- `IModState` — host-persistent per-mod opaque key/value storage.
- Kernel domains for items/players/entities/fluids/run state.
- The content binding foundation (`IModContent`, `IModContentCatalog`,
  `IContentBindingProvider`, `ModContentBinder`) with a shared-content
  network-mode filter for static content.

What is still missing is a **consistent runtime data model**: when a mod wants
per-player or per-world data, which surface is appropriate, how to declare
whether the data is local-only or shared, and how shared data maps into the
kernel/authority rules instead of leaking through ad-hoc snapshots or generic
channels.

## Scope suggested for the future implementation

- Define a mod manifest/attribute or content-declaration field for
  **data scope**: `LocalOnly`, `Shared`, or `HostAuthoritative`.
- Route shared mod data to the existing typed kernel or a dedicated mod-domain
  state service, not to arbitrary snapshot protocols.
- Keep local-only data off the wire by default; enforce this with tests and
  logging.
- Clarify how `IModState` relates to local-only vs shared data: it is
  host-persistent today, which is a special case of shared/authoritative
  state, not the general rule.
- Add an explicit migration/adapter path for CUCoreLib/KrokMP style
  `JObject` snapshot modules: they are **not** a CUO model and should map to
  either kernel domains or `IModCommands`/`IModNetwork`.

## Non-goals for this ticket

- Do not add a generic JObject snapshot protocol.
- Do not expose game/Unity types through Abstractions.
- Do not let local-only mods write host-persistent or shared state.

## Evidence / current state

- Static content already enforces this distinction for item definitions:
  `ModContentBinder` only binds content from `Synchronized`,
  `Authoritative`, or `RequiresAllPlayers` mods.
- Runtime mod data sync remains open and is tracked by this ticket.
