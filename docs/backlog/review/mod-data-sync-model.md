# Mod data sync model

- Status: Review
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

## Landed first seam (runtime data scope)

The first concrete implementation is the scope-declared runtime data seam:

- `ModDataScope` (`LocalOnly`, `Shared`, `HostAuthoritative`) in Abstractions.
- `IModData` in Abstractions — per-mod runtime data surface.
- `ModDataStore` / `ModDataPolicy` in Runtime — process-local ephemeral slot
  table, safety rails, and scope eligibility.
- `IModContext.Data` exposes the per-mod adapter.

Semantics:

- `LocalOnly`: any network mode, any role, never persisted, never wire.
- `Shared`: state-bearing mode + `SendNetworkMessage`; host writes, a guest
  applies a host-originated value explicitly (`TryApplyShared`). No automatic
  replication.
- `HostAuthoritative`: state-bearing or host-only; host-only framework store,
  guests have no mirror and coordinate through `IModCommands` / `IModNetwork`.
- `IModState` remains the host-persistent special case; it is not the general
  runtime sync rule.

This deliberately does **not** add a generic JObject snapshot protocol or a
new wire message. Existing `IModNetwork` / `IModCommands` remain the transport
for shared values; the runtime store is only the local scope-aware state
surface.

Evidence: `docs/evidence/selfchecks/mod-api/mod-runtime-data-selfcheck.md`,
`docs/api/mod-api.md` §4j, `ModDataTests`.

## Why this exists

CUO already has the low-level building blocks:

- `IModNetwork` — opaque mod messages, star topology, host broadcast.
- `IModCommands` — host-authoritative command execution with directed results.
- `IModState` — host-persistent per-mod opaque key/value storage.
- Kernel domains for items/players/entities/fluids/run state.
- The content binding foundation (`IModContent`, `IModContentCatalog`,
  `IContentBindingProvider`, `ModContentBinder`) with a shared-content
  network-mode filter for static content.

What was missing before this seam was a **consistent runtime data model**: when
a mod wants per-player or per-world data, which surface is appropriate, how to
declare whether the data is local-only or shared, and how shared data maps into
the kernel/authority rules instead of leaking through ad-hoc snapshots or
generic channels.

## Remaining / future work

- Per-player / per-limb mod status runtime values still need a
  host-authoritative status domain boundary; that is tracked separately under
  the CUCoreLib migration ticket (`docs/architecture/mod-status-domain.md`)
  and is not part of this ticket.
- If a future mod genuinely needs framework-owned replicated state (not just a
  local mirror fed by explicit mod messages), that should be designed as a
  typed kernel domain with dedicated events, not as a generic snapshot API.

## Non-goals for this ticket

- Do not add a generic JObject snapshot protocol.
- Do not expose game/Unity types through Abstractions.
- Do not let local-only mods write host-persistent or shared state.
- Do not auto-replicate `IModData` values over a hidden framework wire.
