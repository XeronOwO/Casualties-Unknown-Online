# Interaction gate authority — the reach half moves to the actor's client

Date: 2026-09-19
Scope: stage 1 of `todo/remote-interaction-local-gating.md` (the reach half), plus the
architecture-watchlist split it needed before anything could land in the medical family's
two 600-line services.

## What landed

- **One seam rule**: a reach gate is evaluated where the actor is. Every cross-player
  request runs `IPlayerInteractionVisibility.HasLineOfSight(LocalSteamId, target)` on the
  client that FORMS the request — the Runtime `Send*Request` methods — and the host
  handlers no longer consult the oracle for a remote actor at all. A host's own action goes
  through the same method, so the host is judged by its own client like everyone else.
- **Twelve host-side judging sites retired**: nine Runtime services (medical injection,
  shrapnel, stage-3, heal, item use ×2, take, remote inventory, push, carry) plus
  `TraderRecruitCoordinator`'s host re-check. The two sites that already ran on the actor's
  own client (`RemoteBackpackCoordinator.Open`, `TraderRecruitCoordinator`'s send path) and
  the UI projection (`OnlineUiMemberRow.CanSee`) stay as they were.
- **Push reach moved with the visibility gate**: `MaxPushDistanceSq` is compared on the
  pusher's own client (`HasPushReach`). The host keeps the degenerate-distance guard (two
  bodies at one point have no push direction) and the push cooldown, which is
  effect-frequency arbitration — uniform for every player and not a tolerance window — and
  is therefore explicitly kept.
- **Refusal parity**: a medical start the local gate refuses fires the same
  `MedicalOperationStartAckMsg` (`Accepted=false`, `RejectReason="No line of sight."`) the
  host used to send, on the actor's own client. The other families never answered a refusal
  on the wire, so their local refusal is a log line exactly like the host's was.
- **Prerequisite extraction**: the medical family's reservation bookkeeping
  (`_reservedItems`, `_reservedTargetLimbs`, the cross-service "operator busy" lambda chain)
  moved into `MedicalOperationClaims`; the shrapnel start validation moved out of the
  service into the pure `ShrapnelStartValidator`. Both were demanded by
  `watchlist/architecture-watchlist.md` before the next change could land.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Reach gate | evaluated at the actor's request-formation seam | `Runtime/Session/PlayerInteraction/Player*Service.cs`, `MedicalOperationSessionService`, `ShrapnelOperationSessionService`, `OtherMedicalOperationSessionService` |
| Host judging | no host handler calls the oracle for a remote actor | the same nine services; `TraderRecruitCoordinator` |
| Push reach | `HasPushReach` on the pusher's client; degenerate guard stays host-side | `PlayerPushService.cs` |
| Refusal surface | local medical refusal reuses the host's ack shape | `MedicalOperationSessionPublisher.RejectStart` |
| Claim bookkeeping | one owner for item/limb/operator claims | `MedicalOperationClaims.cs` |
| Shrapnel start rules | pure validator, session lifecycle stays in the service | `ShrapnelStartValidator.cs` |
| Wire | **no protocol change** (no NetMsg, no `ProtocolVersion` bump) | `ProtocolVersion.Current` stays 24 |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors.
- Focused: `dotnet test tests/CasualtiesUnknownOnline.Tests --filter
  "FullyQualifiedName~MedicalOperation|FullyQualifiedName~Shrapnel|FullyQualifiedName~InteractionGateAuthority|FullyQualifiedName~LineOfSightRefusal|FullyQualifiedName~Carry|FullyQualifiedName~Push|FullyQualifiedName~Heal|FullyQualifiedName~ItemUse|FullyQualifiedName~Take"`
  — green. The exact filter is named because a focused count means nothing without it.
- Direction coverage is per family, never a blanket claim. `InteractionGateAuthorityTests`
  (13 cases) pins the NEGATIVE direction — the actor's own blocked oracle refuses before
  anything leaves its client, asserted as "the host received no request frame" — for push,
  injection start, stage-3 start, take, carry, heal, item use and remote inventory; and the
  POSITIVE direction — a host whose own oracle says "blocked" must not refuse an actor whose
  client sees a clear line, with the request reaching the host — for push (both the
  visibility gate and the moved reach constant), injection start and heal. The other
  families' positive direction is covered by their existing success tests, which run with an
  allow-all oracle and would fail if the relocated gate were over-strict. The pre-existing
  `LineOfSightRefusalTests` blocks BOTH sides, so it passes under either design and is not
  counted as direction evidence here.
- Behaviour of the refusal REASONS is unchanged for the medical family (asserted by the
  precise-reason cases for injection and stage-3).

## Accepted limitations

- The gate is still a confirmed-wall blocker: no angle, range or collider refinement, and
  missing position evidence still allows (the oracle's own convention). Reach for push is
  the one distance constant, judged on the actor's client with the same constant.
- A client that lies about its own reach is out of scope (anti-cheat is a non-goal of the
  ticket), and because the host no longer re-judges, a frame crafted around the client gate
  reaches the host ungated — the stage-3 dispatcher reads `Kind` from the wire, so the
  deleted host check was the only thing in front of it. An unmodified client cannot take
  either route: every GameAdapter start site sends the Kind that matches its gated send
  method, and a locally refused start ends its minigame and restores the item
  (`Accepted=false` cleanup), so the player is never left in a stuck session.
- The target's BODY preconditions are still judged by the host from the target's 1 Hz
  report; that is stage 2 of the ticket and it needs a new message pair (see the ticket's
  "What remains").
