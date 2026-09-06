# Host metal-scrap block placement sound not heard on guest

- Status: Review
- Priority: Medium
- Category: Block placement / character audio sync
- Source: User report (2026-09-06) — when the host, while holding metal scrap, places a block into the world, the guest client does not hear the placement sound.

## Goal

Make the host's metal-scrap block placement sound audible to the guest, verify the reverse direction and third-party view during the implementation cycle, and check the same held-material placement family (climbingrope, scaffoldingpack) for the same defect.

## Reproduction / acceptance matrix

Reproduction: host and guest are in the same session; host holds metal scrap in hand; host places it into the world as a block; the guest did not hear the placement sound. The native sound is the `Sound.Play("scrapmetal", ...)` one-shot call at the placement point (`reversing/Assembly-CSharp/Assembly-CSharp/Item.cs:2203`).

| Scenario | Expected | Covered by |
|---|---|---|
| Host places scrapmetal -> guest view | guest hears the same one-shot `scrapmetal` placement sound | existing star relay + capture scope |
| Guest places scrapmetal -> host view | host hears the placement sound (guest report -> host apply) | existing star relay |
| Third party watches another player's placement | third guest hears it via the host relay | existing star relay |
| Host/guest places climbingrope | `ropeplace` placement sound is captured the same way | direct-placeable family scope |
| Host/guest places scaffoldingpack | `scrapmetal` placement sound is captured the same way | direct-placeable family scope |
| Failed/gated placement (cannot place, occupied, low condition) | no sound, no report | scope only emits on the real Sound.Play call; no report is generated before the native call |
| Remote apply / clone replay | no capture echo | `CallContext.Origin.RemoteApply` guard unchanged |
| Solo / no active session | no network report, local audio unchanged | `CharacterSoundSync.Report` session-active guard |

## Implementation

- Added `CharacterSoundKind.ItemPlacement` (11) to the existing `CharacterSoundMsg` dedicated one-shot event family.
- Added `CharacterSoundPolicy.Origin.ItemPlacement` and the `CallContext.Origin.CharacterItemPlacement` scope.
- `DirectPlaceableUseItemPatch` (`Body.UseItem`) and `DirectPlaceableUseItemInHandPatch` (`Body.UseItemInHand`) now carry a per-call `DirectPlaceableUseState`:
  - `ConditionBefore` preserves the existing success signal for the `ArmsSwing` report;
  - `SoundScope` opens `CharacterItemPlacement` only for local, non-carried, direct-placeable item use — never for unrelated item-use sounds, remote clones, or internal reorder scopes.
- `SoundPlayPatch` maps `CharacterItemPlacement` to the new policy origin and captures the exact real `Sound.Play` clip (`scrapmetal` / `ropeplace`) at the native placement point; no re-derived position or clip.
- `CharacterSoundPolicy` classifies only the exact placement clips (`scrapmetal` / `ropeplace`) under the new origin, and both direct placeable hooks open the scope only for a conscious local body — an unconscious `Body.UseItemInHand` fallback `Body.Attack` cannot be misreported as an item placement.
- Reuses the existing star-relay `CharacterSoundMsg` path: host broadcasts its own placement sound; guest reports to host and the host relays to the other guests. No per-frame audio stream, no new `NetMsg`.
- `ProtocolVersion.Current` bumped 14 -> 15 because the existing CharacterSound wire event gains a new `CharacterSoundKind`.

## Evidence

- `tests/CasualtiesUnknownOnline.Tests/Session/CharacterSoundPolicyTests.cs` — `ItemPlacement` origin/kind classification for `scrapmetal` and `ropeplace`, plus negative rejection of non-placement clips such as `BSSwing3`/`unlock`.
- `tests/CasualtiesUnknownOnline.Tests/Session/CharacterSoundSyncTests.cs` — `ItemPlacement` protobuf roundtrip, spatial facts (no follow owner, 3D), and a host-owned `ItemPlacement` broadcast reaching both guests.
- `tests/CasualtiesUnknownOnline.Tests/Patching/DirectPlaceableArmSwingPatchTests.cs` — `IsPlaceable` family filter, `ShouldOpenPlacementSoundScope` requiring conscious + direct-placeable family, `DirectPlaceableUseState` carries the placement-sound scope, and both patch surfaces preserve Harmony parameter shapes.
- Red->green: the new `ItemPlacementOrigin_ExistsAndClassifiesPlacementClips` test was observed failing on the pre-fix code (`Assert.NotNull() Failure: Value is null`) before implementation.
- Full solution: `dotnet build` 0 warnings/0 errors; `dotnet test` 2488 + 17 normative-gate tests passed; `dotnet format` passed.
- Independent adversarial review (fresh subagent) initially found a misclassification edge (an unconscious `UseItemInHand` fallback Attack could be reported as `ItemPlacement`); it was fixed with a clip whitelist plus a conscious-body scope gate, and the review then passed.
- Deployed latest DLLs to the real game directory and verified SHA-256 hashes match the build output for Runtime, GameAdapter, Protocol, and Plugin DLLs.
- Self-check docs updated:
  - `docs/evidence/selfchecks/presentation/direct-placeable-arm-swing-selfcheck.md`
  - `docs/evidence/selfchecks/players/character-sound-selfcheck.md`

## Acceptance status

Code-complete and in `review/` for the final unified acceptance pass. The user will confirm the actual two-client audio behavior during that pass.

## Non-goals

- Not adding voice chat or continuous audio streaming.
- Not adding a new wire protocol for this sound; the existing one-shot event family carries it.
- Not changing block hit/break sounds (they already replay through the native remote `DamageBlock` apply path).
