# Cross-player component medicine self-check

Owner cycle: backlog "Cross-player item use" component-medicine candidate.
Decision: add the `analgesicgauze` opiate component to the existing
cross-player heal operation. No new wire message and no protocol bump; the
existing `PlayerHealResultMsg` already carries the full post-heal `Health` and
`Limbs`.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | `analgesicgauze` native limb use | `Item.cs:437-467` — BandageMinigame full pass adds `SkinHealAmount += 20`, `BandageSlowAmount += 50`, `Pain -= 300`, and `Painkillers.opiateAmount += 28` |
| 2 | Painkillers component wire state | `CharacterHealthMsg.OpiateAmount` (ProtoMember 68) + `PainkillersSync` (see `docs/tech-decisions.md` #102) |
| 3 | Existing cross-player heal wire | `PlayerHealRequestMsg` / `PlayerHealResultMsg` — reused unchanged |
| 4 | Host target snapshot | `PlayerHealService` already saves the complete target `CharacterDataMsg`, including `Health` |
| 5 | Local body apply | `CharacterDataSync.ApplyHealState` maps the full `CharacterHealthMsg` and runs `PainkillersSync.Apply` |
| 6 | Heal item catalog | `RemoteHealProfiles` already treats `analgesicgauze` as a heal item; only the opiate component was missing |

## 2. Design

- `RemoteHealProfile` gains `OpiateAmount = 0f`; `RemoteHealProfiles` sets it
  to `28f` for `analgesicgauze`, matching the full-success native value.
- `RemoteHealApplication` gains a health+limb overload:
  `Apply(CharacterHealthMsg, CharacterLimbMsg, RemoteHealProfile)`. It reuses
  the existing limb-only apply and adds the opiate amount to the health
  snapshot, clamped non-negative.
- `PlayerHealService` calls the new overload with the cloned target health, so
  the host's saved snapshot and the result message both carry the opiate
  component.
- **Scope limits** — this closes the opiate *component* part of
  `analgesicgauze`. Timed/random medicine branches, minigame-random tools and
  timed tools remain future slices.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Profile data | `RemoteHealProfiles` exposes `analgesicgauze` with `OpiateAmount = 28` | `RemoteHealApplicationTests.Profiles_KnownItemSetExists` |
| Pure apply | health+limb overload adds opiate and keeps limb effects | `RemoteHealApplicationTests.Apply_WithHealthAddsOpiateComponentAndKeepsLimbEffects` |
| Host operation | guest uses `analgesicgauze` on host — saved host health has opiate 28 | `PlayerInteractionServiceTests.Guest_UsesAnalgesicGauzeOnHost_AddsOpiateComponentAndSendsResult` |
| Result wire | result `Health` carries opiate; no new NetMsg | `PlayerHealResultMsg` / `DirectionTests` unchanged |
| Local apply | `CharacterDataSync.ApplyHealState` + `PainkillersSync.Apply` writes the component on the target body | existing opiate slice evidence (`docs/tech-decisions.md` #102) |

## 4. Verification

- **L0 unit**: `RemoteHealApplicationTests` +1, `PlayerInteractionServiceTests`
  +1; full suite 1460 green.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet format`,
  check-architecture / check-event-replay / check-entity-event-dispatch all
  pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
