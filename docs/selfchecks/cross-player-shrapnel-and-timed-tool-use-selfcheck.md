# Cross-player shrapnel and timed tool use self-check

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/selfchecks/MANIFEST.md` and
> `docs/architecture-evolution/protocol.md` before citing.

Owner cycle: backlog "Cross-player item use" remaining tool slices. Decision:
add `tweezers` (minigame-random shrapnel removal) and `medicalsuture` (timed
bleed tick) to the existing `PlayerItemUseRequest`/`PlayerItemUseResult`
operation. No new wire message; the result gains one additive protobuf field
and there is no `ProtocolVersion` bump.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Tweezers native limb use | `Item.cs:1687-1706` — starts `ShrapnelMinigame`, consumes 0.01 condition, full success removes every shrapnel piece (`limb.shrapnel = 0`). |
| 2 | Medicalsuture native limb use | `Item.cs:368-390` — immediate `pain += 12.5`, `skinHealAmount += 25`, condition `-= 0.51`, then `CoUtils.instance.DoTimedOp("suture"+limb.name, bleed -= 4.5, 10f)` (1 Hz tick semantics, `CoUtils.cs:157-169`). |
| 3 | Existing cross-player use wire | `PlayerItemUseRequestMsg` / `PlayerItemUseResultMsg` (NetMsg 116/117) — reused unchanged; `PlayerItemUseResultMsg.TimedEffects` is an additive field. |
| 4 | Limb snapshot surface | `CharacterLimbMsg.Shrapnel` already rides the existing character snapshot; no new limb field. |
| 5 | Host authority | `PlayerItemUseService` validates both players, applies the immediate snapshot effect, consumes condition and publishes one result. Timed ticks are deliberately NOT simulated by the host — the target's local body runs them and reports back. |
| 6 | Local target apply | `PlayerInteractionApply.OnPlayerItemUseReceived` applies the post-use state; `TimedLimbEffectApply` schedules the exact native `CoUtils.DoTimedOp` on the target body. |

## 2. Design

- `RemoteLimbToolProfile` gains `RequiresShrapnel`, `TimedBleedPerSecond` and
  `TimedBleedDurationSeconds`.
- `RemoteLimbToolCatalog` adds:
  - `tweezers` — condition cost 0.01, shrapnel-only.
  - `medicalsuture` — condition cost 0.51, immediate pain/skin-heal, timed
    bleed `-4.5/s` for `10s`.
- `RemoteLimbToolApplication` picks the limb with the most shrapnel for
  tweezers (refused when the target has no shrapnel), clears it on full
  success, and leaves the timed bleed out of the immediate host snapshot.
- `PlayerItemUseService` builds one `TimedLimbEffectMsg` for a timed tool and
  includes it in the result payload; the acting player's local side ignores it,
  the target's local side runs it.
- `TimedLimbEffectApply` (GameAdapter, new top-level class so
  `PlayerInteractionApply` stays under the 600-line gate) calls
  `CoUtils.instance.DoTimedOp` with the native `"suture" + limb.name` id and
  the same tick lambda, so a cross-player suture behaves exactly like the
  native self-use path.
- **Scope limits** — supported tools: `tweezers`, `medicalsuture`.
  Timed/random liquid medicine branches remain a separate future slice.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Tool catalog | known ids accepted, unknown refused | `RemoteLimbToolApplicationTests.Catalog_ExposesKnownLimbToolsAndRefusesUnknown` |
| Tweezers | picks most-shrapnel limb, clears shrapnel | `ApplyTweezers_RemovesMostShrapnelLimb` |
| Tweezers | no shrapnel on target → refused | `ApplyTweezers_NoShrapnelLimb_IsRefused` |
| Medicalsuture | immediate pain/skin-heal, bleed untouched | `ApplyMedicalsuture_AppliesImmediateEffectsAndExposesTimedProfile` |
| Host operation | guest uses tweezers on host, shrapnel cleared and item condition updated | `PlayerInteractionServiceTests.Guest_UsesTweezersOnHost_RemovesShrapnelAndSendsResult` |
| Host operation | tweezers with no shrapnel refused before consume | `Tweezers_NoShrapnelOnTarget_IsRefused` |
| Host operation | guest uses medicalsuture on host, immediate state + timed effect carried | `PlayerInteractionServiceTests.Guest_UsesMedicalSutureOnHost_AppliesImmediateAndCarriesTimedEffect` |
| Wire | result carries timed limb effect additively | `PlayerItemUseResultMsg.TimedEffects` asserted in the host-service test |
| Local apply | target schedules native timed op | `TimedLimbEffectApply` calls `CoUtils.DoTimedOp` (static adapter surface; L0 evidence) |

## 4. Verification

- **L0 unit**: `RemoteLimbToolApplicationTests` +3,
  `PlayerInteractionServiceTests` +3; full suite 1466 green.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` full suite
  green, `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
