# Piggyback (conscious-alive ride) + carried-player release self-check

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/selfchecks/MANIFEST.md` and
> `docs/architecture-evolution/protocol.md` before citing.

Owner cycle: backlog "Other lower-priority KrokMP candidates" — push/piggyback.
Decision: implement the **piggyback first slice** by extending the existing
cross-player carry relation, plus a small adjacent quality item: the carried
player can also request release. Push remains future work.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Existing cross-player carry relation | `PlayerCarryService` — host-owned `_carriedBy`/`_carrying`, one-carrier/one-carried, broadcast `PlayerCarryStateMsg` |
| 2 | Carried body presentation | `CarriedBodyDriver` + `PlayerInteractionApply.OnCarryStateChanged` — the carried client skips local simulation and follows the carrier |
| 3 | UI eligibility projection | `OnlineUiMemberProjection.Build` — action booleans are pure Runtime L0-testable |
| 4 | Existing wire message | `PlayerCarryStartRequestMsg` (NetMsg 99), `PlayerCarryStopRequestMsg` (100), `PlayerCarryStateMsg` (101) |
| 5 | Host authoritative snapshots | `PlayerCharacterAccess` / `PlayerCarryService` health validation |

## 2. Design

- **No new NetMsg or protocol bump.** `PlayerCarryStartRequestMsg` gains an
  additive `Piggyback` field (ProtoMember 2). New peers can start either the
  classic unconscious/dead carry or a conscious-alive ride; old peers simply
  send the default false and keep the original carry path.
- **Host validation** now branches on `msg.Piggyback`:
  - classic carry = target must be unconscious or dead (unchanged);
  - piggyback = target must be conscious and alive, carrier must be
    conscious/alive, and neither side may already be in a carry relation.
- **Direction** — `SendPiggybackRequest(target)` means the local player climbs
  onto `target`'s back: `target` becomes the carrier and the requester becomes
  the carried rider, matching KrokMP's "Climb on their back." Classic carry
  remains requester-carries-target.
- **Same relation/broadcast.** Both modes write the same carry tables and
  broadcast the same `PlayerCarryStateMsg`; `CarriedBodyDriver` already gives
  the ride presentation without a second network surface.
- **Carried player can release.** `HandleCarryStopRequest` now accepts the
  current carried player as the requester in addition to the carrier. The UI
  shows a "Get down" action on the local row when the local player is being
  carried.
- **UI** adds a `Piggyback` button on the Players page and the in-world
  right-click context menu for conscious/alive in-world remotes, plus the
  `Get down` button for the local carried row. Eligibility remains in
  `OnlineUiMemberProjection`.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Piggyback request | Additive `Piggyback` bool + `SendPiggybackRequest` API | build + `PlayerInteractionServiceTests` guest/host piggyback cases |
| Host piggyback gate | conscious/alive target accepted, dead target refused | `PlayerInteractionServiceTests.Piggyback_DeadTarget_IsRefused` |
| Carry mode preserved | classic carry still refuses conscious/alive targets | existing `Carry_ConsciousTarget_IsRefused` remains green |
| Release by carried | host resolves stop from either carrier or carried | `PlayerInteractionServiceTests.CarriedPlayer_CanRequestRelease` |
| UI projection | `CanPiggyback` / `CanRequestDrop` booleans | `OnlineUiMemberProjectionTests` new cases |
| Localization | English/Chinese labels | `LocalizationCatalog` entries |

## 4. Verification

- **L0 unit**: `PlayerInteractionServiceTests` +4 cases,
  `OnlineUiMemberProjectionTests` +3 cases; full suite **1393 green**.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` all green,
  `dotnet format` on changed files, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, **no manual acceptance**.
