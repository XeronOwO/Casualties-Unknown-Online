# Remote backpack held-item / Tab-transfer self-check

> Development-period evidence for the remote-backpack acceptance cycle that
> landed `TransferToRequester`, `UseOnSelf`, drain-object pour/edge-drop,
> non-destructive same-owner apply, and main-hand slot routing. Real dual-client
> visual/gesture acceptance remains part of the final unified acceptance pass.

## Mechanisms

| Mechanism | Change |
|---|---|
| Tab transfer to opener | `RemoteInventoryOperationKind.TransferToRequester` -> `PlayerInventoryTakeService.HandleRemoteBackpackTake`; conscious remote owner allowed when `AllowRemoteInventoryTake`, legacy Online UI take unchanged |
| Held remote item self-use | `RemoteInventoryOperationKind.UseOnSelf` -> `PlayerItemUseService.HandleRemoteHeldItemUse`; recursive carried-tree lookup/replace/remove |
| Pour | Native `liquidDrainObject` hit sends `Pour` |
| Edge drop | Screen-left/right always sends `Drop`, even for water containers |
| Same-owner container refusal | `PlayerInteractionApply` no longer destroy+rebuild; logs native capacity fields and leaves the real source |
| Main-hand | Remote body-slot `InvButton`s are not filtered by native center-distance `Overlaps` |
| Durability | `RemoteItemPresentation.ApplySourceValues` covers all clone-inventory branches |

## Verification

- Build clean.
- Full suite: 2529 + 17 normative gates passed.
- `dotnet format` applied.
- Deployed DLLs SHA256 match build output for all six CUO assemblies.
- New tests: held self-use (host→guest and guest→host), nested container source,
  unconscious owner + conscious requester, worn item refusal, double-Tab transfer
  both directions with conscious owner, legacy take null-health refusal.

## Runtime boundary

No automated game-internal input/screenshot harness exists. The remaining
acceptance is the user's dual-client pass with the deployed latest DLLs.
