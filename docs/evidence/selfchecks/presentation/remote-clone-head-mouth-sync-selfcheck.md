# Remote Clone Head/Mouth Sync — Host Fall-Injury Mouth-Expression Desync Self-Check

Delivery-cycle fact sheet for the re-opened
`docs/backlog/todo/host-fall-injury-mouth-expression-desync.md` item
(moved to `docs/backlog/review/` after this cycle).

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | `FacialExpression.Update` selects the head sprite from `Body.eatTime > 0.15f`, `Body.HoldingItem(2)`, or `Body.limbs[0].dislocated`; eat-time only past 0 still selects the half-open sprite | `reversing/Assembly-CSharp/Assembly-CSharp/FacialExpression.cs:93-104` (decompiled via dnSpy) |
| 2 | A remote render clone never runs the original `Body.Update`, so its `eatTime` is reset every frame and its slot/limb presentation comes from the 1 Hz clone renderer | `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyUpdatePatch.cs`; `RemoteBodyFactory.cs`; `CloneInventoryRenderer.cs`; `CloneLimbRenderer.cs` |
| 3 | The previous stale-input fix zeroed `eatTime` on render clones, which removed the inherited stale mouth but also stopped the clone from being able to reproduce the owner's actual mouth decision through that field | `src/.../BodyUpdatePatch.cs` `NeutralizePoseInputs`; `docs/evidence/selfchecks/presentation/remote-clone-facing-auto-flip-selfcheck.md` |
| 4 | The remaining mouth triggers (`HoldingItem(2)`, `limbs[0].dislocated`) are still derived on the clone from proxy/inventory/limb data, not from the owner's actual visual state; a fall injury can therefore expose a remote-only mouth when the owner's own `FacialExpression` does not show one | `CloneInventoryRenderer.cs`; `CloneLimbRenderer.cs`; `FacialExpression.cs` |
| 5 | The 1 Hz `CharacterHealthMsg` already carries the face/body snapshot to every peer; adding the owner's final mouth decision uses the existing self-healing carrier | `CharacterDataSync.cs`; `CharacterHealthMsg.cs` |
| 6 | The fact table applies carried/limb/enemy events immediately; the mouth decision must be refreshed there too, otherwise a replay postfix would pin the old snapshot state until the next 1 Hz report | `CloneFactTable.cs` event methods |

## 2. Root cause

The host fall-injury desync is not a missing "mouth-open" patch. The root cause is
that the remote clone's head sprite is still **re-derived on the receiving side
from clone-local state** (slot-2 item presence, head-limb dislocated flag, and
the zeroed inherit-eat-time). Those clone-local inputs are presentation data,
not the owner's visual truth, and any mismatch between them produces exactly the
reported remote-only mouth. The fix carries the owner's actual mouth decision on
the existing 1 Hz character snapshot and replays it on the clone after the
game's own `FacialExpression.Update` has run. Because the owner's `eatTime` is
the only mouth trigger not otherwise present in the snapshot, it also rides the
same message so event-driven slot/limb changes can recompute the mouth decision
without waiting for the next full snapshot.

## 3. Implementation

- `HeadMouthState` (Closed / HalfOpen / Open) and `EatTime` added to
  `CharacterHealthMsg` (ProtoMember 78/79) and carried by the existing
  character snapshot / limb-state event capture path.
- `HeadMouthRule` is a pure Runtime rule replicating the game's own mouth-sprite
  branch, so the adapter capture is a thin call and the decision is L0-testable.
  `HeadMouthRule.Refresh` recomputes the same state from a `CharacterDataMsg`
  after slot/limb fact-table events.
- `CloneFacePresentation.Capture` records the owner's actual mouth state and
  eating timer from the live `Body` (same facts the owner's `FacialExpression`
  uses).
- `CloneFacePresentation.ApplyVitals` stashes the captured state on
  `RemoteBodyDriver.HeadMouth`.
- `CloneFactTable` event paths call `HeadMouthRule.Refresh` after every carried
  item / limb-latch / enemy-terminal mutation, so the replayed state never
  lags behind an event that already changed the clone's rendered slot/limb.
- `CharacterDataStore` save-path event handlers and
  `PlayerKernelRestoreProjection` also call `HeadMouthRule.Refresh` before
  persist/restore, so a disconnect-crash or reconnect cannot hand back stale
  head/mouth state.
- New `FacialExpressionHeadPatch` postfix restores the owner's head sprite on
  remote clones after the game's formula; local bodies and disfigured heads are
  left to the game's own native path.
- `ProtocolVersion.Current` 5 → 6 because the 1 Hz character wire shape changed.

## 4. Regression / tests

| Test | Coverage |
|---|---|
| `HeadMouthRuleTests` | closed default, half-open short eat-time, all three open triggers, disfigured overrides open triggers, and `Refresh` after slot-2 add/remove, head-limb dislocate, and eat-time-only states |
| `FacePresentationVitalsTests` | `HeadMouth` is part of the projected face-vitals set and maps from the wire message |
| `NetPacketTests.CharacterHealth_FaceLatchPresentation_RoundTrips` | `HeadMouthState.Open` and `EatTime` survive protobuf round-trip |
| `PatchContractTests` | the new `FacialExpressionHeadPatch` target resolves and its Harmony contract is valid |

## 5. Verification (development-period, no manual dual-client acceptance)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2268 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | checked as part of this cycle |
| Deploy identity | `tools/deploy.ps1` deployed to the real game directory; adapter/runtime/plugin DLL SHA256 matches build output (see below) |

## 6. Whole-family alignment

This is the same presentation family as the earlier stale facing/eat-time fix:
the owner-side visual state is the source of truth and the frozen clone no
longer invents a head sprite from proxy inputs. It covers:
- fall-injury / head-limb dislocated remote-only mouth;
- eating/drinking short mouth (half-open) that the previous `eatTime = 0`
  neutralization could not show;
- holding-a-mouth-item mouth;
- disfigured heads, which stay on the existing synced disfigurement path.

## 7. What was NOT changed (and why)

- No change to carry/movement authority, inventory authority, or any gameplay
  state.
- No new NetMsg / separate face channel: the existing 1 Hz character snapshot
  remains the carrier.
- No suppression of legitimate owner mouth states: the clone now shows the
  owner's actual mouth, not a hard-coded closed face.
- The game's own `FacialExpression.Update` still runs; the new postfix only
  restores the head sprite from the captured owner decision after the formula
  has run on the frozen clone.
