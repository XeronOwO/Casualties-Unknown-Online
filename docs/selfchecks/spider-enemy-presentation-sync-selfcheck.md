# Spider enemy presentation sync — self-check (2026-08-23)

The animation audit listed two `SpiderHandler` presentation rows open: frozen
guest copies do not receive the host's leg IK target poses
(`SpiderHandler.cs:49-59`), and host-ordered remote spider bites never replay
the native one-shot `ClawAnim` visual (`SpiderHandler.cs:201-208`;
`EnemyCombatReplay.cs:72-104`). This closes both in the enemy-presentation
domain.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Spider leg IK update | `SpiderHandler.Update` writes `legs[i].rootPos` and lerps `legs[i].targetPos` every frame (`SpiderHandler.cs:57-58`); `IKHandle.Update` renders the segments from those fields (`IKHandle.cs:40-56`). |
| Frozen copy behavior | `EnemyPatches` disables `SpiderHandler.Update`/`FixedUpdate` on `RemoteEnemyDriver` copies, so the guest never receives the host leg pose. |
| Spider bite claw visual | `SpiderHandler.CheckForLimbDamage` instantiates `Resources.Load("ClawAnim")`, orients it with the collision normal, parents it to the spider and destroys after 5 s (`SpiderHandler.cs:201-208`). |
| Host-ordered remote bite | `EnemyCombatDirector.TryOrderSpiderBite` sends `EnemyAttackMsg` to the remote victim (`EnemyCombatDirector.cs:305-351`); `EnemyCombatReplay.ApplyHostSpiderBite` replicates the bite side effects on the victim side but previously omitted the claw visual (`EnemyCombatReplay.cs:72-104`). |
| Existing enemy stream | `EnemyStateMsg` carries the presentation subset (position/velocity/rotation/health/stunned) at 20 Hz and in the world-entry snapshot. |

## 2. Changes

- **Leg IK wire** — `EnemyStateMsg.SpiderLegTargets` (ProtoMember 7)
  carries a nullable list of `NetVector2Msg` (world-space `IKHandle.targetPos`);
  `EnemyEntity.SpiderLegTargets` mirrors it.
  `ProtocolVersion` 44 → 45 because older peers cannot render the crawl.
- **Leg IK capture** — `SpiderLegPresentation.Capture` reads every
  `SpiderHandler.legs[i].targetPos` on the host; non-spider enemies carry null.
- **Leg IK apply** — `SpiderLegPresentation.Apply` mirrors the host targets onto
  the frozen copy and re-derives the leg root from the copy's own leg transform,
  so only the target positions travel.
- **Bite claw replay** — `SpiderClawReplay.Play` reproduces the native
  instantiation/rotation/parent/destroy; it is called by
  `EnemyCombatDirector.TryOrderSpiderBite` for the host's own view and by
  `EnemyCombatReplay.ApplyHostSpiderBite` on the victim. No new `NetMsg` and no
  direction-table row.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1350 passed |
| `EnemyStateRoundtripTests.SpiderLegTargets...` | leg-target list roundtrips and missing → null |
| `SpiderEnemyPresentationTests` | 3 passed (helper surface locked) |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` / `tools/check-entity-event-dispatch.ps1` | passed (no entity event kind touched) |
| `dotnet format` | run |
| Deploy | `tools/deploy.ps1` to the real game directory succeeded |
| Manual acceptance | Not required by the developer-cycle rule; L0 + static evidence, no manual acceptance. |

## 4. L0 proof

- `EnemyStateRoundtripTests` proves `SpiderLegTargets` survives
  `EnemyEntity` → `EnemyStateMsg` → `ApplyTo`, and that a missing list stays
  null.
- `SpiderEnemyPresentationTests` locks the adapter boundary:
  `SpiderLegPresentation.Capture(SpiderHandler) -> List<NetVector2>?`,
  `SpiderLegPresentation.Apply(BuildingEntity, IReadOnlyList<NetVector2>)`,
  and `SpiderClawReplay.Play(SpiderHandler, Vector2)`.
- The full suite has no behavioral regression; the existing
  `PatchContractTests` still verify the enemy freeze patch surfaces against the
  game assembly.

## 5. Structure review

- `SpiderLegPresentation` is a focused static bridge (capture/apply only, no
  cross-call state).
- `SpiderClawReplay` is a one-shot display helper with no state.
- `EnemySyncCoordinator` remains under the 600-line gate; it only adds one
  captured field and one apply call.
- `EnemyCombatDirector` / `EnemyCombatReplay` remain under the 600-line gate
  and each stays within its existing responsibility.
- No dead mechanism is left behind; the existing periodic enemy stream remains
  the fallback for positional state, while the claw visual is a one-shot local
  replay of the same host-ordered attack command.

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选一个并完成"), so this cycle's plan is approved
without a separate interactive approval step.
