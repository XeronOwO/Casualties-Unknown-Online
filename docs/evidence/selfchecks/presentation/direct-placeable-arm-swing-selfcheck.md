# Direct placeable-item ArmsSwing + placement sound sync — self-check (2026-08-23, extended 2026-09-06)

The animation-audit row for `scrapmetal`, `climbingrope` and `scaffoldingpack`
was open: their `ItemInfo.useAction` delegates play `ArmsSwing` directly
(`Item.cs:2165/2208/2249`) instead of going through `Body.Attack` /
`Body.ThrowItem`, so the existing `OnArmSwing` report never fired and peers'
clones did not replay the swing.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Existing swing stream | `Body.Attack` / `Body.ThrowItem` call `OnArmSwing`; `EntitySyncService.MarkLocalAttackSwing` increments `SwingSeq`, the peer clone replays `ArmsSwing` on sequence changes (`SessionStatePump.cs:149-160`). |
| Direct placeable use actions | `Item.cs:2143-2250`: these delegates play `body.armsAnimator.Play("ArmsSwing")` and write an item-condition cost only after their gates pass (`scrapmetal` 0.25, `climbingrope` 0.501, `scaffoldingpack` 0.01). |
| Common entry | `Body.UseItem(Item)` is the single call path for `useAction` (`Body.cs:2475-2480`), also used by the radial-menu drag; crafting goes through `Recipe.TryMake`. |
| Call identity | `CallContext.Origin.Craft` / `RemoteApply` / `InternalReorder` exist to keep non-local-action invocations quiet. |

## 2. Changes

- `DirectPlaceableArmSwingPolicy` — pure success rule: report only when the
  item id is one of the three direct placeables and the condition actually
  dropped, so gated/failed placements do not mark a swing.
- `DirectPlaceableUseItemPatch` / `DirectPlaceableUseItemInHandPatch` — the
  `Body.UseItem` drag path and the `Body.UseItemInHand` LMB hand path both
  capture condition and call `OnArmSwing` for a successful local-action
  placement only (not remote/carried/craft/internal scopes); for the
  direct-placeable family they also open
  `CallContext.Origin.CharacterItemPlacement` around the native use.
- `CharacterSoundKind.ItemPlacement` +
  `CharacterSoundPolicy.Origin.ItemPlacement` +
  `CallContext.Origin.CharacterItemPlacement` — the real `"scrapmetal"` /
  `"ropeplace"` string sound is captured by `SoundPlayPatch` and rides the
  existing dedicated reliable `CharacterSoundMsg` path (host broadcast,
  guest→host report, third-party host relay), no per-frame audio stream.
  The classifier is a clip whitelist (`scrapmetal` / `ropeplace`), so an
  unconscious `UseItemInHand` fallback Attack can never be misreported as an
  item placement; both direct placeable scopes also require a conscious local
  body.
- No new `NetMsg`; `ProtocolVersion.Current` bumped 14→15 because the
  existing CharacterSound event gains a new `CharacterSoundKind`.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `DirectPlaceableArmSwingPatchTests` | passed: pure success rule, no-op/unknown/item increases, `IsPlaceable` family filter, placement-sound scope state, both patch surfaces, PatchInventory contracts |
| `CharacterSoundPolicyTests` / `CharacterSoundSyncTests` / `CharacterSoundPatchTests` | passed: `ItemPlacement` origin/kind classification, protobuf roundtrip and spatial facts, existing character-sound patch contracts |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | passed |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` / `tools/check-entity-event-dispatch.ps1` | no event mechanism touched |
| Manual acceptance | Not required by the developer-cycle rule; L0 + static evidence, no manual acceptance. |

## 4. L0 proof

- `DirectPlaceableArmSwingPatchTests.Policy_*` exercises the exact success
  rule used by the patch (three items report, unknown/no-op/negative do not).
- The patch-surface test locks the Harmony parameter names/shapes so a game
  update cannot silently detach the hook; `PatchInventory` auto-contracts the
  new `Body.UseItem` target.

## 5. Structure review

- `DirectPlaceableArmSwingPolicy` is a pure one-concern helper (no Unity, no
  state, no I/O).
- `DirectPlaceableUseItemPatch` is a thin adapter with per-call `__state` only.
- The existing `OnArmSwing` path already existed for Attack/Throw; no duplicate
  swing channel was introduced.
