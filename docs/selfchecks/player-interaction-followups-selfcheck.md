# Player-interaction carry/piggyback follow-ups — self-check (2026-08-26)

Closes the four open follow-ups recorded after the piggyback direction +
drag-use pass: carrier-side real-time rider presentation, piggyback
weight/encumbrance host rule, release floating-body restore, and the missing
local-as-carrier piggyback UI direction.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Carry/piggyback relation | `PlayerCarryService` — host-owned `_carriedBy`/`_carrying`, broadcast `PlayerCarryStateMsg` |
| 2 | Carry request wire | `PlayerCarryStartRequestMsg` (NetMsg 99) + additive `RequesterIsCarrier` |
| 3 | Carried local body presentation | `CarriedBodyDriver` + `PlayerInteractionApply.UpdateCarriedBody` |
| 4 | Remote rider clone rendering | `RemotePlayerRenderer` / `SessionStatePump` — 20 Hz render proxies |
| 5 | Release/restore path | `PlayerInteractionApply.ApplyCarryStateToBody` + new `CarriedBodyPlacement.RestoreLocalBody` |
| 6 | Native encumbrance | `Body.GetTotalEncumberance` (`Body.cs:2223-2250`) and `Item.totalWeight` (`Item.cs:13-23`) |
| 7 | Host-rule surface | `HostRulesOptions` / `IHostRules` / `HostRulesService` + BepInEx config |
| 8 | UI action eligibility | `OnlineUiMemberProjection` + `OnlineUiMemberListDrawer` + context menu + quick panel |

## 2. Changes

- **Local-as-carrier piggyback** — `PlayerCarryStartRequestMsg` gains an
  additive `RequesterIsCarrier` field. `SendCarryOnBackRequest(target)` keeps
  the requester as carrier and invites the conscious/alive target to ride on
  the requester's back. The host branches on the field in the existing
  carry/piggyback validation. The UI gets a `Carry on back` / `背起` action
  alongside the existing `Piggyback` / `骑背` action; both share the same
  eligibility projection and the quick panel/context menu reuse the row flags.
- **Carrier-side real-time follow** — `RemotePlayerRenderer.Update` now takes
  the local body and, after applying the 20 Hz state to every clone, overrides
  the clone of the player the local player is currently carrying with a direct
  back-offset attachment to the local body. This is presentation-only; the
  rider still reports its own authoritative position through the normal stream.
- **Release restore** — the carry-state release branch now places the released
  local body at the carrier's current position, re-enables the body/limb
  rigidbodies that the carried-proxy path froze, and restores the native
  standing pose for conscious/alive bodies or the ragdoll/limb-physics pose
  for unconscious/dead bodies. The shared placement/restore rules live in
  `CarriedBodyPlacement`.
- **Piggyback weight host rule** — `[HostRules] PiggybackWeightMultiplier`
  (default 0.8, range 0–3) is added to the host rules surface and Admin page.
  A `Body.GetTotalEncumberance` postfix reads the local carrier's current
  carry mirror, computes the carried player's full snapshot encumbrance from
  the authoritative character data, and adds `full * multiplier` to the
  carrier's own result so movement/encumbrance reacts to the load.
- **No protocol bump** — only an additive protobuf field; no new `NetMsg`, no
  `ProtocolVersion` change, no event/item/entity matrix row touched.

## 3. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Local-as-carrier mode | additive field + service method + host direction branch | `PlayerInteractionServiceTests.Guest_InvitesHostToRideOnGuestBack...` and `Host_InvitesGuestToRideOnHostBack...` |
| UI eligibility | `CanCarryOnBack` projected from the same conscious/alive/in-world/no-carry rules | `OnlineUiMemberProjectionTests.ConsciousAliveRemoteCanRideOnLocalBack...` + carried-local negation |
| Weight host rule | option/service exposure + config + Admin editor | `HostRulesPolicyTests.HostRulesService_ComposesNewFlagsAndRespawnFlags` |
| Weight contribution | multiplier + patch surface | `CarryEncumbrancePatchTests` (pure multiplier + Body.GetTotalEncumberance contract) |
| Carrier-side follow | renderer overrides carried clone position from local body | static Unified-code path; no L0 game-object harness by design |
| Release restore | re-enables frozen physics + native pose restore | static `CarriedBodyPlacement.RestoreLocalBody` + code review |

## 4. Verification

- **L0**: `dotnet test CasualtiesUnknownOnline.slnx` — **1509 passed / 0 failed**.
- **Gates**: `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1` all pass.
- **Format**: `dotnet format` run; `--verify-no-changes` only flags the
  gitignored generated `obj/.../MyPluginInfo.cs`.
- **Runtime verification**: development-period rule — L0 simulation + static
  evidence, **no manual acceptance**.

## 5. Structure review

- New files are small top-level types: `CarriedBodyPlacement`,
  `CarriedEncumbranceCalculator`, `CarryEncumbrancePatch`.
- `RemotePlayerRenderer` grows one private presentation method; no state bools.
- `PlayerInteractionApply` stays under the 600-line gate; `GameAdapterDomains`
  gains only the host-rule field it already received in the constructor.
- No dead mechanism remains; the periodic stream remains the fallback for all
  other peers while the carrier-side attachment is a local presentation cache.
