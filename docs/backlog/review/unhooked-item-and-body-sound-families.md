# Local-only item and body one-shot sounds outside the ingest family

- Status: Review
- Priority: Low-Medium
- Category: Character/item audio sync / report coverage
- Source: the whole-family audit of `review/host-eating-sound-not-heard-on-guest.md` (2026-09-26): fixing the reported meal meant giving two native paths their own capture scopes, and the same census showed every other local-only one-shot clip of that family still reports nothing.
- Related: `review/host-eating-sound-not-heard-on-guest.md` (the ingest half, landed), `review/sync-player-pain-vocalizations-and-bark.md` (the dedicated one-shot event family), `docs/architecture/remote-inventory-native-parity.md` (§3.4 and the stage-3 leave), `review/unhooked-damage-block-callers.md` (the same "the census found more callers than the fix covered" shape), `review/suppressed-native-call-sounds-stay-unheard.md` (the rows this cycle records as out of family), `docs/evidence/selfchecks/presentation/unhooked-item-and-body-sound-families-selfcheck.md`

## The gap

The character-sound capture is call-identity scoped, and the ingest cycle added the two scopes the
ingest sounds needed. Every other local-only one-shot clip still played on the client that ran the
native call and nowhere else. This cycle's census (decompiled sites; `reversing/` line numbers are
stable), with the decision each row got:

| Family | Clips and native sites | Who heard it before | Decision |
|---|---|---|---|
| Medical / limb item use | `syringe` (Item.cs:784/830/1375/1539/1565/1613/1734/1760/1926/7123, Liquids.cs:1494), `splint` (515/1483/1509), `goo` (616/1443/1464/1589/2553/2588/3947/3973), `boneweld` (696), `drainuse` (1658), `tweezeruse` (1700), `spray` (2100/2124), `laser` (4659), `wrenchhit` (4864), `cream` (Liquids.cs:1074/1100) — 31 sites, 28 of them in Item.cs | The acting client only | CARRIED as `Medical`, from TWO scopes: `PlayerCamera.ApplyWoundItem` is the choke point every LIMB action enters (`useLimbAction` has exactly one invocation in the assembly, PlayerCamera.cs:754; the container branch is :760 → `WaterContainerItem.ApplyToLimb` → the registry's `onHealthUse`), while four of the censused sites play from the item's WORLD use action instead — the rag's `splint` (:515), the rosepod's `goo` (:1443), the drainer's `drainuse` (:1658) and `Item.DrawBlood`'s `syringe` (:7123, reached from two liquid-container use actions) — which runs under `CharacterItemUse`. The medical clip set is therefore consulted from both scopes; an earlier cut of this cycle listed it under the limb scope alone and the independent review found exactly those four sites still silent. |
| World-liquid drink | `drink` (FluidManager.cs:314, the water branch's own play) and the registry's `onDrink` clips (`pills` at Liquids.cs:1133/1175/1190/1286/1298, `drink` at :1501) | The drinking client only | CARRIED as `Drink`, from one scope on `FluidManager.DrinkLiquid` — the entry the world-drink branches and their `onDrink` delegates run inside. `onDrink` has other entries (`WaterContainerItem.cs:210`, `NonDescriptCan.cs:78`); those are container uses, i.e. `CharacterItemUse`, where the same clips already report as the ingest family — no clip is lost, and the two paths carry different kinds. |
| Tool / utility item use | `combine` (Item.cs:4297/6700), `flashlighttoggle` (3998/4022), `error` (5672), `centrifuge` (5688), `drop` (the stonefruit use action, 2614) | The acting client only | CARRIED as `Utility` — every one of them plays inside the item's own `useAction` (all five verified against their enclosing delegate) and therefore already inside the item-use scope; only the classification was missing. |
| Inventory gestures | `switch` (Body.SwitchHands Body.cs:1131, Body.SwapSlots :1427), `combine` (Body.CombineItems :1284), `waterpour` (Body.CombineLiquids :1250) | The acting client only | CARRIED as `Gesture` — classified from the scopes CUO already opens for other reasons (`InternalReorder` around SwitchHands/SwapSlots, `Craft` around CombineItems) plus one new scope on `Body.CombineLiquids`, which had none. No nested second scope: nesting inside `Craft` would hide the origin its own guards read. |
| Body sickness / effort | `vomit1`/`vomit2` (Vomiter.cs:86/125), `stretch` (Body.cs:2510), `dogshake` (Body.cs:2553) | The acting client only | CARRIED as `BodySound` — each plays inside a coroutine body, so the scope is entered PER STEP through the coroutine wrapper (`ScopedCoroutine`). The nap clip is the ordinary routine: `Body.TakeANap` picks `AltNapCoroutine` (no sound) when sickness/happiness/temperature are out of band (Body.cs:2492-2497). |
| 2D screen feedback | `vomitwarning` / `bloodvomitwarning` (Vomiter.cs:144/155), the climb clips (Body.cs:477), and the syringe minigame's own cues (`bullethit` SyringeMinigame.cs:79, `syringe` :86 — both `Vector2.zero` with the 2D flag, from the minigame's Update) | The acting client only | DELIBERATELY LOCAL (user decision, 2026-09-26 session): the policy line is "every 3D world sound is carried; 2D screen feedback stays the acting player's own". These calls carry no world position, so a relay could not place them. |
| World-item ground impact | `drop` and the block step sound in `Item.OnCollisionEnter2D` (Item.cs:238-247) | The client that simulates the collision — and on a guest that copy is suppressed by `NonAuthoritativeItemImpactPolicy`, so a guest hears no world item land at all | OUT OF THIS FAMILY, ticketed as `review/suppressed-native-call-sounds-stay-unheard.md`: there is no acting player to attribute it to, and the item-impact domain owns who simulates the landing. |
| Remote-driven replay | an item use replayed by the item's owner through the inventory intent path (`RemoteIntentApplier.ApplyUseItem` → `Body.UseItem` under `RemoteApply`) | The owner's client only | UNCHANGED BY DESIGN: a remote-driven mutation must not report as the local player's action, so the capture stays local-action only. The consequence — the owner hears a sound the peers do not — is recorded on the new ticket. |

The census method matters: the first cut of this ticket enumerated literal `Sound.Play(` calls in the
file that DEFINES a use action, which missed every clip the called COMPONENTS play. This cycle's census
followed the delegates' callees (`useLimbAction` → `ApplyToLimb` → `onHealthUse`, `DrinkLiquid` →
`onDrink`) and then asked, per site, WHICH scope the call actually runs under.

## What landed (2026-09-26 cycle)

- Five new call identities, each named for what it captures: `CharacterMedicalUse`
  (`PlayerCamera.ApplyWoundItem`), `CharacterWorldDrink` (`FluidManager.DrinkLiquid`),
  `CharacterInventoryGesture` (`Body.CombineLiquids`), `CharacterBodySound` (the per-step coroutine
  scope) — plus the classification of the two origins that already existed (`InternalReorder`, `Craft`).
  Every scope opens only for a plain local action on this client's own body or camera
  (`CaptureScopeGuard`), so a render clone, a remote application and the remote-medical view (which
  blocks the native call) can never report as the local player's action.
- `CharacterSoundPolicy` classifies the five families by clip; the item-use row consults the ingest,
  MEDICAL and item-feedback sets, because a medical clip is reportable from the limb-action scope and
  from the item-use scope alike.
- `CharacterSoundKind` gains `Medical` / `Drink` / `Utility` / `Gesture` / `BodySound`;
  `ProtocolVersion.Current` is bumped in the same change with its per-number log entry.
- `ScopedCoroutine` wraps the four native coroutines step by step and never keeps a scope open across
  a `yield`.
- The census is gate-pinned (`ItemAndBodySoundCaptureGateTests`): the carried clip set per decision,
  the REACH of each set (which origins consult it), the deliberately-local 2D rows — including that
  no patch anchors a known 2D source — every anchor's DECLARING type with same-shaped wrong-type
  negative samples, and a census floor.
- Four new patch classes are registered in `AdapterCapabilityCatalog`'s Character capability.
- Independent adversarial review (fresh context, frozen tree) found four majors, all fixed in this
  same cycle: the four world-use medical sites, the missing 2D minigame row, the red number not
  describing the frozen gate, and the self-check's empty verification section.

## Acceptance criteria

| # | Scenario | Expected | Verified by |
|---|---|---|---|
| 1 | A player treats a limb (syringe / splint / goo / bandage) | every other client hears the exact clip once, at the treated limb | the scope + reach + census pins and the policy tests; the audible half is the user's dual-client pass |
| 2 | A player treats ANOTHER player's limb through the remote medical view | UNCHANGED: the native call is blocked there (`RemoteMedicalBlockApplyWoundItemPatch`), so no clip plays on any side and none is invented — recorded, with the carrier decision, on `review/suppressed-native-call-sounds-stay-unheard.md` | selfcheck §3 (the census row) + §8 (the limit) |
| 3 | A player uses a medical item through its WORLD use action (the rag, the rosepod, the drainer, drawing blood) | every other client hears the clip — the four sites ride the item-use scope, not the limb-action one | `MedicalClips_AreCarriedFromBOTHTheLimbActionAndTheItemUseScope` + `TheItemUseRow_ConsultsTheIngestMedicalAndFeedbackSets` |
| 4 | A player drinks from a world liquid (all five branches) | every other client hears the drink clip once | the scope + classification pins; the audible half is the user's dual-client pass |
| 5 | A player toggles a flashlight / opens a centrifuge / combines items / switches hands / pours between containers | every other client hears that one-shot once; the acting client does not hear it twice | the classification pins + the receiver's own-echo drop; the audible half is the user's dual-client pass |
| 6 | A player vomits, takes an ordinary nap, or shakes water off | the clip plays once per event — once, not once per coroutine step; the water shake arrives follow-based, the vomit and the nap position-based | the wrapper's per-step scope + `BodySoundFamily_CarriesTheFollowFactTheNativeCallHad` |
| 7 | Any of the above, on a third peer | hears it once, no double audio; the source never hears its own sound back | `HostLimbTreatmentSound_BroadcastsToBothGuests_AndNeverReturnsToTheHost` + the star relay |
| 8 | The 2D vomit prompts, the climb clips, the syringe minigame's cues | stay the acting player's own — no peer hears them | selfcheck §3 (the deliberate-local row) + `TheTwoDimensionalCues_OpenNoCaptureScope` |
| 9 | A remote-driven item use (peer-driven, owner's client replays it) | unchanged: the owner hears it, peers do not; no report is invented for a mutation this player did not make | selfcheck §2 decision 5 |

## Evidence

- Self-check: `docs/evidence/selfchecks/presentation/unhooked-item-and-body-sound-families-selfcheck.md`
- Gate: `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ItemAndBodySoundCaptureGateTests.cs` —
  red recorded against the pre-change `src/` with the FROZEN matcher (7 failed / 9 passed / 16), green
  after the change (16/16)
- Widened census gate: `ConsumeSoundCaptureGateTests` (the wire-kind pin carries the five new kinds)
- Independent review: `%TEMP%/cuo-review-unhooked-item-and-body-sound-families.md` (fresh context, no
  build/test access, findings M1-M5 and m6-m12/n13-n18 all dispositioned in the selfcheck §7)
- Code: `src/CasualtiesUnknownOnline.GameAdapter/Patches/{MedicalSoundPatches,WorldDrinkSoundPatches,InventoryGestureSoundPatches,BodySoundPatches,ScopedCoroutine,CaptureScopeGuard}.cs`,
  `SoundPlayPatch.cs`, `CallContext.cs`, `Capabilities/AdapterCapabilityCatalog.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/CharacterSoundPolicy.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/CharacterSoundKind.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs`
- Tests: `CharacterSoundPolicyTests` (per-family positive and negative clips, the two-scope medical
  reach), `CharacterSoundSyncTests` (kind/clip/spatial round-trip, the BodySound follow fact, the host
  broadcast with no self-echo), `CharacterSoundPatchTests` (the seven new contract rows against the
  game assembly)
- Native: `PlayerCamera.cs:739-763`, `Item.cs:238-247/515/1443/1658/2614/3998/4022/4297/5672/5688/6700/7114-7125`,
  `Liquids.cs:1074/1100/1133/1501`, `FluidManager.cs:286-329`, `Body.cs:477/1131/1231/1250/1284/1427/2492-2510/2553`,
  `Vomiter.cs:45-158`, `SyringeMinigame.cs:79/86`
- Gate inventory: `docs/evidence/normative-gates.md`

## Limits

- The gate reads SOURCE: a SPLIT anchor (the type in one attribute, the method name in another), the
  `nameof(...)` form and a manual `PatchProcessor` call are outside its reach (the game-assembly
  contract tests own that half), and the decompiled game tree is not in this repository, so no pin can
  notice a NEW native clip the game adds — what they notice is a clip added to, removed from or moved
  between the carried families without the census being reviewed, and a scope anchored on a known 2D
  source.
- The 2D decision is a user-facing boundary, not a technical one: the climb clips, the two vomit
  prompts and the syringe minigame's cues stay local because the acting player chose "3D world sounds
  only". They play at `Vector2.zero`, so carrying them would mean inventing a position.
- Whether the clips are AUDIBLE, at the right place, once, and without double audio is the user's
  dual-client acceptance run: the sounds are Unity icalls and the test host cannot play them.
- The medical clips' position is the treated limb's body. In every path reachable today that is THIS
  client's own body — the only writer of `selectedLimb` is the local wound view, and the remote
  medical view blocks the native call exactly while another player's limb is selected. Position-based
  reporting is still the right shape (no corrected native call passes a follow transform), but the
  "another player's limb" case is not exercised by any run.
- The remote-medical and world-item-impact rows are recorded as out of family with their reasons; the
  new ticket carries them rather than leaving them unstated.

## Non-goals

- Not re-deriving the ingest fix: that family landed in `review/host-eating-sound-not-heard-on-guest.md`'s
  cycle and its census is unchanged.
- Not a general "every `Sound.Play` becomes a message" sweep: the remaining rows are recorded as
  deliberately local or ticketed, and stating that with a reason is a valid outcome.
- Not inventing a clip for a native call that never played one (the blocked remote-medical apply), and
  not relaying a suppressed native impact through the character channel: both are the new ticket's
  question.
