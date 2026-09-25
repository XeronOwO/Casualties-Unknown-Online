# The host's eating sound is not heard on the guest

- Status: Review
- Priority: Medium
- Category: Character/item audio sync
- Source: User acceptance finding (2026-09-21): the host eats and the guest does not hear the eating sound at all.
- Related: `review/sync-player-pain-vocalizations-and-bark.md` (the one-shot character-sound relay this extends), `review/guest-hears-only-some-block-break-sounds.md` (the same acceptance pass's other audio finding), `review/host-metal-scrap-block-place-sound-not-synced-to-guest.md`, `todo/unhooked-item-and-body-sound-families.md` (the sibling sounds this cycle deliberately leaves alone, with the census), `docs/architecture/remote-inventory-native-parity.md` §3.4 (the operator-side item sounds that family already records)

## Root cause (from source, not from the report's narration)

The character-sound capture is **call-identity scoped**: the string `Sound.Play` patch classifies a call
only when a `CallContext` scope opened around a known native method is active, and the pure
`CharacterSoundPolicy` maps that scope + the clip to a `CharacterSoundKind`.

Every ingest sound plays inside a native method that carries **no scope**:

1. the eat clips are played by the item's own `ItemInfo.useAction` delegate — `Sound.Play("eatFlesh" /
   "eatCrunch" / "glass" / "crystalenemylaugh", body.transform.position, …)` at Item.cs:1789, 2387, 2463,
   3588-3589 and their siblings — and the delegate is invoked from `Body.UseItemInHand` (Body.cs:2454) or
   `Body.UseItem` (Body.cs:2479). Those two hooks existed, but they opened the
   `CharacterItemPlacement` scope only for the three direct placeable ids
   (`DirectPlaceableArmSwingPolicy.IsPlaceable`), so an edible item's use ran with
   `CallContext.Current == LocalAction` and the policy's `_ => null` swallowed the clip. The container
   drink is the same call: `WaterContainerItem.Drink` plays `"drink"` / `"pills"` at the drinker's body
   (WaterContainerItem.cs:214) from inside the container's own `useAction` (Item.cs:962/984/1006/…), so
   that ingest half was silent for exactly the same reason;
2. the meal-end burp is played by `Body.HandleVisuals`' own burp timer
   (`Sound.Play("burp", …, base.transform, Random.Range(0.15f, 0.3f), …)`, Body.cs:3137-3142), 5-10 s
   after the meal that armed it (`Body.Burp` / `Body.Eat`, Body.cs:2253-2284) — outside every scope, and
   outside the item-use call that had long returned.

The side that ate therefore played the sounds for itself, no message was ever sent, and the remote clones
never ran the native calls. `CallContext.Enter` semantics make the scope the only evidence of "this client
played it" — there is no clip-name-only capture — so the fix is to give the two paths their own scopes,
not to widen an existing one.

## Implementation

- `CharacterSoundKind.Consume` (12) joins the existing dedicated one-shot event; `ProtocolVersion.Current`
  is bumped 40 → 41 in the same change, whose doc comment carries the per-number log.
- Two new capture scopes, both named for what they capture (the `CharacterLockpickPain` precedent):
  - `CallContext.Origin.CharacterItemUse` — opened by `BodyItemPatches.OpenUseSoundScope`, the single
    decision both use hooks now share: the placement scope for a direct placeable, this scope for every
    other usable item. `CharacterSoundPolicy.Origin.ItemUse` classifies exactly the ingest clips — the four
    eat clips plus the container drink's `"drink"` / `"pills"` — and nothing else, so a syringe / splint /
    combine use inside the same scope stays silent.
  - `CallContext.Origin.CharacterBurp` — opened by the new `BurpSoundPatches.BodyHandleVisualsBurpPatch`
    around the local body's `Body.HandleVisuals`; `Origin.Burp` classifies exactly `"burp"`.
- The wire carries what the native call had, because the capture already reads the real call's arguments:
  the exact clip, its position, its volume and whether it followed the body — which is why the eat clips
  arrive position-based (`follow` is null there) while the burp arrives following the eater's body
  (`follow` is `base.transform`).
- No new message, no new member, no second sound path: the ingest family rides the existing
  `CharacterSoundMsg` star relay (guest → host report, host broadcast + relay, owner echo dropped), and the
  receiver replays under `RemoteApply`, where the capture patch can never echo it back.

## Evidence

- `tests/CasualtiesUnknownOnline.Tests/Session/CharacterSoundPolicyTests.cs` — `ItemUse` classifies the
  ingest clips (the four eat clips plus the container drink's `"drink"` / `"pills"`) and rejects the other
  item sounds; `Burp` classifies `"burp"` only; `Consume` is defined on the wire enum.
- `tests/.../Session/CharacterSoundSyncTests.cs` — the kind round-trips with its clip and position facts, the
  burp's `FollowOwner` rides the per-call fact, and the host's meal sound reaches both guests.
- `tests/.../Patching/CharacterSoundPatchTests.cs` — `PatchInventory` declares the `Body.HandleVisuals`
  contract and the new patch's prefix/postfix shape resolves in the game assembly.
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ConsumeSoundCaptureGateTests.cs` — the routing pins:
  the wire-kind census, both use hooks routing through the shared scope, the meal-end patch existing, the
  policy's clip whitelist, every `CallContext.Origin.Character*` mapped by a `Sound.Play` patch, and the
  protocol constant's own log entry for its current value.
- Red→green: the pins were observed failing on the pre-fix tree (`src/` restored to HEAD) before the
  implementation — `%TEMP%/cuo-red.txt`.
- The new patch class is registered in `AdapterCapabilityCatalog.cs`'s Character capability — the one
  manual surface a `[HarmonyPatch]` class needs, since `PatchInventory` and the contract-row censuses are
  reflection-derived. The independent review caught the missing row as a full-suite red (exactly what the
  focused + gate ladder cannot see) and it was fixed in this same change.
- The full suite with build is green — `%TEMP%/cuo-full-verify.txt` (main 3979/3979, normative gates
  175/175).
- Self-check: `docs/evidence/selfchecks/presentation/host-eating-sound-not-heard-on-guest-selfcheck.md`
  (§1 mechanism inventory, §2 whole-family audit, §3 self-check table, §4 the reachable red and the
  Unity-bound limit, §5 what a real session must show, §6 limits).

## Goal and acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | Host eats | The guest hears the same eating sound at the same moments |
| 2 | Guest eats | The host hears it |
| 3 | A third peer | Hears it once, no double audio |
| 4 | The end of a meal | The closing burp joins the same reported family |
| 5 | Sibling one-shot consumable sounds (drinking/pouring, combine, hand switch) | Each rides the same path or is recorded as deliberately local with a reason |

## Acceptance status

Code-complete and in `review/` for the final unified acceptance pass. Rows 1-5 are **not** proven by this
cycle's tests: the sounds themselves are Unity icalls (`Sound.Play` → `Resources.Load` + `PlayOneShot`) and
the test host cannot run them, and the production capture reads `CallContext` from the real call. What the
machine checks is the routing the user's report depended on — the scope that makes an ingest call
reportable exists on both use hooks and on the meal-end path, the policy classifies those clips and only
those, and the existing star relay carries the new kind. The audible result needs the user's two-client
session (self-check §5).

Row 5's audit is recorded in self-check §2 with the census: the placement clips already ride
`ItemPlacement`; the container drink (`"drink"` / `"pills"`, WaterContainerItem.cs:214) rides this cycle's
item-use scope together with the eat clips — the independent review found that half missing from the first
cut and it was fixed here rather than ticketed; the inventory-gesture sounds (`switch` / `combine` /
`waterpour` / `batteryinsert`) are deliberately local actor feedback whose state changes already sync (and
whose operator-side absence the remote-inventory page records); the world-liquid drink
(`FluidManager.DrinkLiquid`, FluidManager.cs:314, reached from `Body.HandlePhysics`) plays outside every
capture scope and is ticketed; and the medical/limb, tool and sickness clips are ticketed as
`todo/unhooked-item-and-body-sound-families.md` because they need a carrier decision (their positions
follow the operated limb, not the actor).

Not deployed this cycle (user instruction 2026-09-25: no deployment, the machine is in use for a game
session) — the deployed artifact stays at the previous build, and the deployment plus the acceptance run
remain the user's release-cycle actions.

## Non-goals

- Not re-implementing the sounds: the receiver replays the exact clip the source's own `Sound.Play` call
  used, and nothing classifies a sound the source did not play.
- Not a general "sync every local sound" sweep: the census of what stays local and what is ticketed is
  recorded, not silently widened into this cycle.
- Not changing the item-use fact path, the inventory intents, or who owns an item's state.
