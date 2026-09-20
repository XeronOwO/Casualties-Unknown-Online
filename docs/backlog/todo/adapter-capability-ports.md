# Split IGameAdapter into capability ports

- Status: Todo
- Priority: Medium
- Category: Architecture / adapter seam
- Source: Loomi architecture review (2026-09-20), item 3
- Related: `todo/patch-bridge-domain-ports.md`, `review/adapter-capability-catalog.md`

## Problem (evidence)

`src/CasualtiesUnknownOnline.Runtime/GameAdapter/IGameAdapter.cs` is one 125-line interface whose 16
members span six unrelated concerns:

- lifecycle and patch installation (`ProbeGame`, `Install`, `Uninstall`, `OnApplicationQuit`),
- world bootstrap parameters (`CaptureWorldParams`, `ApplyWorldParams`),
- local heal-item queries (`HasLocalHealItem`, `GetLocalHealItems`),
- the trader recruit request (`TryRequestTraderRecruit`),
- native UI modal blocking (`SetOnlineUiModal`, `SetOnlineUiScopedBlocks`,
  `SetOnlineUiEscapeSurfaceVisible`),
- remote presentation (`OpenRemoteBackpack`/`CloseRemoteBackpack`,
  `OpenRemoteMedical`/`CloseRemoteMedical`) and the head anchor (`TryGetRemoteHeadPosition`).

Every consumer sees all of it, every test double implements all of it, and a version adapter must
implement all of it even when a capability is absent on that build. Runtime also reaches into adapter
internals through `InternalsVisibleTo`, so the seam is a shared wall rather than a narrow port.

## Goal

Capability ports instead of one widening interface. Names settled in the change, roughly:
`IGameIntegrationLifecycle`, `IWorldBootstrapPort`, `IRemoteInventoryPresentation`,
`IRemoteMedicalPresentation`, `IPlayerAnchorQuery`, `INativeInputBlocker`, and — once
`review/adapter-capability-catalog.md` lands — `IAdapterCapabilityQuery`. A consumer resolves the ports
it actually uses; a version adapter implements per capability rather than the whole surface.

## Acceptance

- Behaviour-preserving refactor: no call site changes meaning, and the adapter's own tests plus the
  patch-contract checks stay green.
- A shape gate fails when a new method is added back onto the aggregate interface, so the old
  surface cannot regrow while the ports are being introduced.
- The `InternalsVisibleTo` surface is reviewed in the same change and reduced where a port can carry
  the read instead; anything that stays records why.

## Notes

The interface is small in lines but wide in responsibility — the split is about who must implement
what, not about file size. Do not merge this with the patch-bridge split: they are different seams
with different consumers.
