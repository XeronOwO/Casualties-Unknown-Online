# Self-check — the guest hears the host's eating sound (`host-eating-sound-not-heard-on-guest`)

Ticket: `docs/backlog/review/host-eating-sound-not-heard-on-guest.md` (Medium).
Cycle: 2026-09-26. Wire change: `CharacterSoundKind.Consume` (+ `ProtocolVersion.Current` bumped 40 → 41 in
the same change — its doc comment in `ProtocolVersion.cs` is the per-number log).

## 1. Mechanism inventory (every claim from source)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The eat sounds are played by the item's OWN `ItemInfo.useAction` delegate (`"eatFlesh"` / `"eatCrunch"` / `"glass"` / `"crystalenemylaugh"` at the eater's body position, 3D, pitch-shift on, no follow transform) | `reversing/Assembly-CSharp/Assembly-CSharp/Item.cs:1789/2387/2463/3588-3589` and the other edible definitions |
| 2 | That delegate has exactly TWO invocations in the whole game assembly: `Body.UseItemInHand` (the LMB path) and `Body.UseItem` (the radial/drag use branch) | `Body.cs:2454`, `Body.cs:2479`; a census of `useAction(` and `useAction.Invoke` over `reversing/Assembly-CSharp/Assembly-CSharp` found no other caller |
| 3 | CUO already hooked both methods, but opened only the placement capture scope, and only for the three direct placeable ids — an edible item's use ran with no scope at all | `BodyItemPatches.DirectPlaceableUseItemPatch` / `DirectPlaceableUseItemInHandPatch`, `DirectPlaceableArmSwingPolicy.IsPlaceable` (`"scrapmetal"`, `"climbingrope"`, `"scaffoldingpack"`) |
| 4 | The meal-end burp is played by the body's own burp timer inside `Body.HandleVisuals` — 5-10 s after the meal that armed it — and it is that method's ONLY `Sound.Play` call | `Body.cs:3137-3142` (the play), `Body.cs:2253-2284` (`Body.Burp` / `Body.Eat` arm `burpTimer`); a `Sound.Play(` census of `Body.cs` places no other call between `HandleVisuals` and the next method |
| 5 | Capture is call-identity: the string `Sound.Play` patch classifies only inside a known `CallContext` scope and reads the REAL call's arguments (clip, position, volume, whether `follow` was null, the spatial mode) | `SoundPlayPatch.cs`; `CallContext.cs` (`Current` is the innermost scope) |
| 6 | The wire already carries every fact this family needs — exact clip, position, volume, follow-owner, 2D mode — so no member had to be added | `CharacterSoundMsg.cs` (`Clip`, `Position`, `Volume`, `FollowOwner`, `TwoDimensional`) |
| 7 | The receiver replays under `RemoteApply` (the capture can never echo the replay) and drops an owner's own echo; the star relay answers a guest's report and broadcasts the host's own | `CharacterSoundSync.cs` (`OnReceived`), `CharacterDataHandler`/`CharacterDataStore.SendCharacterSound` |
| 8 | The native burp call follows the body (`follow` = `base.transform`) while the eat calls pass no follow transform — the capture reports per call, so the same kind can arrive either way | `Body.cs:3142` vs `Item.cs:2387`; `SoundPlayPatch.cs` (`follow != null`) |
| 9 | The container drink is the same ingest path: `WaterContainerItem.Drink(Body, amount, sound)` plays its clip at the drinker's body from inside the container's own `useAction`, with exactly two clip arguments in the item table (`"drink"` ×26, `"pills"` ×7) | `WaterContainerItem.cs:198` (the method, the play at :214), its call sites `Item.cs:962/984/1006/1028/1109/1133/1156/1298/1321/1351/1399/1422/1869/1901/3171-3522/6735/6754`; `Item.cs:1214` plays `"pills"` directly in a pill use action; `Liquids.cs` `onDrink` callbacks play at the body position (a variable clip argument exists nowhere) |
| 10 | The WORLD-liquid drink is a different trigger: `FluidManager.DrinkLiquid` plays `"drink"` itself in its water branch and calls `Liquids.*.onDrink`, all reached from `Body.HandlePhysics` — outside every capture scope, and only a state report (`FluidDrinkPatch`) wraps it today | `FluidManager.cs:314` (the play), `FluidManager.cs:286` (the method), `Body.cs:3112` (the caller); `Liquids.cs:1501` (`onDrink` drink sound) |

## 2. Whole-family audit

- **Both native invocations of the use action are covered**: the shared `BodyItemPatches.OpenUseSoundScope`
  is the one decision both hooks now make (placement scope for a direct placeable, item-use scope
  otherwise), so the LMB use and the drag/radial use cannot drift apart.
- **The scope is broad on purpose; the CLASSIFICATION is the filter.** Every usable item's action runs
  inside the item-use scope, and only the ingest clips are classified (the four eat clips plus the
  container drink's `"drink"` / `"pills"`) — the syringe / splint / goo / combine / flashlight / tool uses
  that share the scope stay silent instead of being invented on the peers. The policy row documents exactly
  that.
- **The first cut of this census had a blind spot, and the independent review caught it**: it enumerated
  the literal `Sound.Play(` calls in `Item.cs`, so a clip a use action plays through a COMPONENT it calls
  (`WaterContainerItem.Drink`) was invisible — the whole drinking half of ticket row 5 was missing, in both
  directions (not carried, not censused). The fix whitelists `"drink"` / `"pills"`; the census now names
  each family by the components the delegates call, and the world-liquid drink that runs outside every hook
  is ticketed.
- **The meal-end sound needed its own scope** because it is not inside any use call (5-10 s later), and it
  lives in a per-frame method: `BurpSoundPatches` wraps the local body's `Body.HandleVisuals` the way
  `LockpingSoundPatches` wraps the lockpick update, and `Origin.Burp` classifies that one clip.
- **Silent siblings were named, not papered over** (ticket row 5): the placement clips already ride
  `ItemPlacement`; the inventory-gesture clips (`switch` / `combine` / `waterpour`), the medical/limb item
  clips, the tool clips and the `Vomiter` sickness clips are censused with their native sites and their
  reasons in `docs/backlog/todo/unhooked-item-and-body-sound-families.md` — the medical/limb row needs a
  subject (patient) decision, and the gesture row is deliberate local feedback whose state already syncs.
- **The remote-driven replay is out of reach by design**: an ingest use that the item's owner replays for
  another player's inventory gesture (`RemoteIntentApplier.ApplyUseItem` → `Body.UseItem` under
  `RemoteApply`) is not reported, because the capture scope is local-action only. Recorded in the ticket
  and in §6; the architecture page already carries the operator-side half of that question
  (`docs/architecture/remote-inventory-native-parity.md` §3.4 and its stage-3 leave).

## 3. Self-check table (mechanism × change × evidence)

| # | Rule | Where | Pinned by |
|---|---|---|---|
| 1 | Both local item-use entry points route through the shared capture-scope decision | `BodyItemPatches.OpenUseSoundScope` | `ConsumeSoundCaptureGateTests.BothItemUseHooks_RouteThroughTheSharedCaptureScope` (each hook's own Prefix body must call it) |
| 2 | The meal-end burp is captured by its own scope around `Body.HandleVisuals` | `BurpSoundPatches` | `ConsumeSoundCaptureGateTests.TheMealEndSound_IsCapturedByItsOwnScope` + `CharacterSoundPatchTests.BurpSoundPatches_OpenAndCloseTheMealEndScope` (reflective, against the game assembly) |
| 3 | The policy classifies the ingest clips and the burp, and nothing else in those scopes | `CharacterSoundPolicy.Classify` | `CharacterSoundPolicyTests.ItemUseScope_ClassifiesTheIngestFamilyAsConsume`, `..._LeavesTheOtherItemSoundsSilent`, `BurpScope_ClassifiesTheMealEndOnly` |
| 4 | The wire kind census is a reviewed surface | `CharacterSoundKind` | `ConsumeSoundCaptureGateTests.CharacterSoundKind_DeclaresExactlyThePinnedKinds` (12 members pinned) |
| 5 | Every character capture scope states how its sounds are classified | the two `Sound.Play` patches | `ConsumeSoundCaptureGateTests.EveryCharacterCaptureOrigin_IsClassifiedByASoundPlayPatch` (census floor 10, matcher self-test) |
| 6 | The protocol bump carries its own per-number log entry | `ProtocolVersion.cs` | `ConsumeSoundCaptureGateTests.EveryProtocolBump_CarriesItsPerNumberLogEntry` (derived from `Current`, no constant to maintain) |
| 7 | The new kind round-trips with its clip and spatial facts, and the burp's follow fact rides the call | `CharacterSoundMsg` | `CharacterSoundSyncTests.Consume_RoundTripsTheIngestClipAndItsSpatialFacts`, `...BurpConsume_CarriesTheFollowFactTheNativeCallHad` |
| 8 | The star relay carries the new kind to every other member | `CharacterDataStore.SendCharacterSound` | `CharacterSoundSyncTests.HostConsumeSound_BroadcastsToBothGuests` (+ the existing relay rows) |
| 9 | The game-assembly contract for the new patch resolves | `PatchInventory.BuildContracts` | `CharacterSoundPatchTests.PatchInventory_DeclaresEveryCharacterSoundTarget` (`Body.HandleVisuals`) |
| 10 | The new patch class is claimed by exactly one capability | `AdapterCapabilityCatalog.cs` (Character) | the main suite's capability gate (`EveryPatchClass_IsClaimedByExactlyOneCapability`, `ContractRows_AllJoinToACapability`, `Probe_AccountsForEveryContractRow`) — a `[Trait("Category","Integration")]` class the focused + gate ladder cannot see, which is why the full suite is a rung of this ladder and not a formality |
| 11 | The full suite WITH build is green on the frozen tree | the whole change | `%TEMP%/cuo-full-verify.txt` (main 3979/3979, normative gates 175/175) |

## 4. Verification design, and its honest boundary

- **Reachable red**: the routing pins in `ConsumeSoundCaptureGateTests` were written first and observed
  failing on the pre-fix tree (`src/` restored to HEAD: the kind absent, no item-use scope, no meal-end
  patch, no whitelist) — see `%TEMP%/cuo-red.txt`. They pin the routing surface the defect lives on (which
  native calls open a scope, which scope classifies which clips, which kind reaches the wire), not a patch
  parameter list.
- **Behavioural core**: `CharacterSoundPolicyTests` locks the classification table (positive and negative
  rows) and `CharacterSoundSyncTests` locks the wire round-trip and the star relay for the new kind.
- **NOT reachable in this host**: the audible result. `Sound.Play` (the `Resources.Load` + `PlayOneShot`
  call), the mixer group and the 3D falloff are Unity icalls, and the production capture reads
  `CallContext.Current` from a real native call. No test in this cycle proves that a sound was heard — it
  proves that the side which played it reports it through the existing chain, and that the sounds which
  must stay local are not classified.
- **Environment**: `dotnet build` + `dotnet test` (focused → normative gates → full suite with build)
  + `dotnet format`.
- **Process lesson from this cycle's own review**: the first cut ran focused + gates only, and the missing
  capability-catalog row was therefore invisible until the independent review ran the full suite — an
  integration-traited gate lives outside the fast ladder, so the full suite is what closes it. The full run
  is required BEFORE the review, not only before the commit.

## 5. What only a real two-client session can confirm

1. Host eats a piece of food → the guest hears the same crunch at the same moment (the reported defect).
2. Guest eats → the host hears it (the reverse direction).
3. A third peer hears each meal once — no double audio in the frame the relay and the report overlap.
4. The closing burp arrives a few seconds after the meal, at the eater's position, on every other screen.
5. A food whose call uses the `"glass"` clip (frigiantfruit / the energy drink) and a crystal shard
   (`"crystalenemylaugh"`) are both heard remotely — the whitelist covers the whole ingest family, not one
   clip.
6. A container drink (water bottle / canteen, and the pill-taking clip) is heard remotely at the same
   cadence, while a drink from a WORLD liquid tile stays silent on the peers (that path is outside every
   capture scope today — see §6).
7. A NON-ingest item use (syringe / splint / combine) stays silent on the other screens — the peers must
   not invent sounds whose source played nothing they classified.

## 6. Limits recorded for the next reader

- The item-use scope is opened for every local, non-carried, non-clone body use, but the capture is
  **local-action only** (`CallContext.Current == LocalAction`): an ingest use replayed by the item's owner
  for another player's inventory gesture (`RemoteIntentApplier.ApplyUseItem` under `RemoteApply`) plays for
  the owner alone and is not reported. Carrying it needs an echo decision (whose sound it is, and whether
  the operator's client would double-play) and is recorded in
  `todo/unhooked-item-and-body-sound-families.md` with the architecture page's own §3.4 note.
- The whitelist is a clip list: a future game item whose ingest plays a NEW clip name needs its row added
  to `CharacterSoundPolicy.Classify` (the gate pins the kind census and the scope routing, not the game's
  item data — that data lives in the decompiled assembly and is not our source surface).
- The burp's pitch is the native call's `1f` with pitch-shift enabled; the wire deliberately carries
  volume (the native random 0.15-0.3) but not pitch, so each receiver rolls its own shift — the same
  presentation-not-state rule the family already follows.
- The `Vomiter`'s sickness sounds and the medical/limb item sounds are the same defect pattern in other
  components; they are censused and ticketed rather than folded in, because their position semantics (a
  patient's limb, a 2D warning) differ from the actor-body sounds this cycle carries.
- A drink from a WORLD liquid tile (`FluidManager.DrinkLiquid` → the water branch's own `Sound.Play("drink")`
  at FluidManager.cs:314, reached from `Body.HandlePhysics`) stays silent on the peers: the only hook on
  that path is a state report (`FluidDrinkPatch`), and the sound runs outside both capture scopes. It needs
  its own scope/decision and is censused in `todo/unhooked-item-and-body-sound-families.md`.
- The census method matters as much as the census result: enumerating literal `Sound.Play(` calls in the
  file that DEFINES a use action misses every clip the called COMPONENTS play (this cycle's own miss, found
  by the review). A future ingest clip hunt must follow the delegate's callees, not just its text.
