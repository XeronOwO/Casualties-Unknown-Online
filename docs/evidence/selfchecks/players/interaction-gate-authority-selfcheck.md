# Interaction gate authority — each client judges its own side

Date: 2026-09-19
Scope: both halves of `review/remote-interaction-local-gating.md` — stage 1 moved the reach gate
to the actor's client, stage 2 moved the target-body preconditions to the TARGET's client — plus
the prerequisite splits each half needed before it could land in the medical family's 600-line
services (the claim bookkeeping, then the injection start path).

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

## Stage 2 — the target's body half

- **What moved**: the preconditions that describe the TARGET's body no longer come from the
  host's copy of the target's 1 Hz report. The host validates what it owns, parks the request in
  `MedicalTargetBodyGate` under a ticket, and asks the target's client
  (`MedicalOperationTargetCheckRequest`, 137); the target runs `MedicalTargetBodyValidator`
  against its OWN live body and answers (`MedicalOperationTargetCheckAnswer`, 138) with its
  verdict, its own refusal reason and its live shrapnel count. The host then commits, or refuses
  the operator with that reason. Protocol 24 → 25.
- **What the host keeps**: the participants (in-world, and the operator's own conscious/alive as
  participant eligibility), the operator's item facts, and the claim arbitration — plus a
  re-check of all of them (`MedicalStartRecheck`) when the answer arrives, because the answer is
  a round trip old. A shared shrapnel session's own limb claim does not refuse the second
  operator joining that session.
- **The liveness bound** (`MedicalTargetBodyGate.AnswerTimeoutMs = 3000`) only abandons an
  unanswered request: the operator gets "Target did not answer the body check.", a target that
  left gets "Target left before answering the body check.", and an operator that left drops the
  parked request. No latency value is read by any verdict.
- **The local body**: `ILocalCharacterCapture`, implemented in the GameAdapter by
  `LocalCharacterCapture` (through the same `CharacterDataCapture` helper the save cut uses) and
  registered by DI replace, exactly like the visibility oracle and for the same constructor-cycle
  reason. The seam declares `HasLiveCapture`, which is what decides what a null capture MEANS:
  with a live capture the null is this client's own answer "my body cannot be read right now", so
  the start is refused ("Target body is unavailable.") rather than judged — a guest's stored
  snapshot is the character it entered the world with, and falling back to it would judge a
  present operation on data older than the report this stage removed. A composition WITHOUT a
  live capture (tests, a non-game root) has no live source at all and answers from this side's own
  stored snapshot; with neither, it refuses. Either way the data is this client's own, never
  another client's picture of this body.
- **Ordering changed on purpose**: the host's own checks now run before the target is asked, so a
  refusal the host already knows costs no round trip; where the old code judged the target limb
  before the operator's item, the item refuses first.

| Mechanism | Change | Evidence |
|---|---|---|
| Target-body verdict | asked of the target's own client, parked under a ticket | `MedicalTargetBodyGate.cs`, `NetMsg` 137/138 |
| Target half | one pure validator for all three families | `MedicalTargetBodyValidator.cs` |
| Operator halves | one home each, every reason string verbatim | `OtherMedicalOperationStartValidator.cs`, `ShrapnelStartValidator.cs`, `InjectionStartValidator.cs` |
| Window re-check | the host's own facts re-validated on the answer | `MedicalStartRecheck.cs` |
| Piece layout | seeded from the answer's live count | `ShrapnelOperationSessionService.CommitStart` → `ShrapnelSessionStateWriter.InitializePieces` |
| Local capture | adapter seam + composition default | `ILocalCharacterCapture.cs`, `GameAdapter/LocalCharacterCapture.cs`, `PluginDependencyRegistrar.cs` |
| Wire | two new ids, protocol bump | `NetMsg.cs` 137/138, `ProtocolVersion.Current = 25` (that cycle's value — the version register carries the current one) |
| Coverage index | both members declared on the P11 row | `docs/evidence/sync-coverage-matrix.md` (P11, 20 anchors) |
| Line budget | the injection start path and the shrapnel session's operator/item bookkeeping extracted | `InjectionStartCoordinator.cs` (`MedicalOperationSessionService.cs` 595 → 510); `ShrapnelOperatorBookkeeping.cs` (`ShrapnelOperationSessionService.cs` 597 → 552) |

### Stage 2 verification

- `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` — 56/56.
- `dotnet test tests/CasualtiesUnknownOnline.Tests --filter
  "FullyQualifiedName~Medical|FullyQualifiedName~Shrapnel|FullyQualifiedName~InteractionGateAuthority"`
  — 113/113. The filter is named because a focused count means nothing without it, and it is wider
  than the three families: it also matches `MedicalOperationIdAllocatorTests`,
  `MedicalToolApplicationTests`, `RemoteMedicalContractTests`, `RemoteMedicalDisplayProjectionTests`,
  `PacketSenderMedicalUpdateClassificationTests`, `ShrapnelPositionStreamTests`,
  `MedicalTargetBodyValidatorTests` and `OnlineUiMemberProjectionTests`. The medical family's own
  suites pass with each target node seeded with its own body
  (`PlayerInteractionTestSession.SeedOwnBody`), which is the test-side expression of "the client
  that owns the body answers for it".
- `InteractionGateAuthorityTests` is 22 cases, and `MedicalTargetBodyValidatorTests` (9 cases, no
  session, no nodes) pins the target half's own rules and their reason strings one family at a
  time. The authority class pins the target half in BOTH directions: the target's own body
  refusing overrides a host report that says the target is fine, and the target's own body being
  fine overrides a host report that says it died. The edges are pinned too: a live body that
  cannot be read is refused instead of falling back to a stored snapshot, a target that cannot be
  judged at all is refused rather than judged by the host on its behalf, the shrapnel layout
  follows the target's own count (4 live pieces where the host's report said 1), and an answer
  from somebody other than the asked target is dropped. The liveness bound is pinned directly on
  the gate: a parked request survives its first `Tick`, 4000 ms later `Tick` abandons it with its
  own refusal and an empty pending table, a second answer for one ticket resolves nothing, and a
  target that leaves is refused while an operator that leaves is dropped silently.
- Refusal reason strings are byte-identical to the pre-split code: "Target limb not found.",
  "Target limb is dismembered.", "Target limb has no splint.", "Target limb has no tourniquet.",
  "Target limb is not dislocated.", "Target limb cannot be amputated.", "Target limb is not
  infected enough for amputation.", "Target limb has no shrapnel.", "Target is not
  conscious/alive.", "Target is not alive.", plus the operator/item reasons that stayed
  host-side.

## Accepted limitations

- The gate is still a confirmed-wall blocker: no angle, range or collider refinement, and
  missing position evidence still allows (the oracle's own convention). Reach for push is
  the one distance constant, judged on the actor's client with the same constant.
- A client that lies about its own reach or its own body is out of scope (anti-cheat is a
  non-goal of the ticket). Since the host no longer re-judges either half, a frame crafted
  around the client gate reaches the host ungated; an unmodified client cannot take that
  route, because every GameAdapter start site sends the Kind that matches its gated send
  method and a locally refused start ends its minigame and restores the item.
- The refusal for an unreadable body ("Target body is unavailable.") IS reachable in the
  production composition: the adapter-backed capture declares `HasLiveCapture`, so a null from it
  is the body's own answer and never falls back to a stored snapshot, which on a guest is the
  character it entered the world with. The stored-snapshot fallback belongs to compositions with
  no live capture at all. The refusal is a real behaviour change for a target whose body cannot be
  read right now (a scene transition, a body that is not up yet) — observed as a precise refusal
  the operator can retry, not as a stuck session. How often that happens on a real client is not
  measurable from here.
- Not verifiable here: how the refusal reads on a real two-client session with real latency,
  and the real `Physics2D`/`PlayerCamera` behaviour of the live capture. Those need the
  physical-machine acceptance pass.
