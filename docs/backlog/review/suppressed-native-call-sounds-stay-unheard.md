# Sounds whose native call is suppressed never reach the peers

- Status: Review - rows 1, 2, 3, 4, 5 and 6 are PASS with counted listener-side readings, and row 4b now has its own: the native AmputationMinigame was started on the alt (after three fixture blockers fell - the CUO window covering the game, /give needing an opped member, and the limb selection needing the new select-limb fixture), the cut was driven with the pointer, and the peer played the same drip* clip family over the same window (~34 clips each), so the acting client's minigame step sounds do reach every other client. What 4b does not yet cover is the FINAL dismember step: cutProgress advances only while the game's hand is really moving and a staged pointer moves in bursts, so the limb reached skin=0, muscle=0 with dismembered=false and the minigame still running. Rows 7 and 8 stay as they were.
- Acceptance (20261001-l): rows 4 and 5-8 judged (the world-item impact replayed 219:219 on each guest from the authority's 219 reports, the plush squeak 12:12, the suite rows); rows 1-4b stay unproven and the dust puff visual was not captured — record `docs/evidence/acceptance/suppressed-native-call-sounds-stay-unheard-20261001-l.md`
- Priority: Low
- Category: Audio sync / report coverage
- Source: the 2026-09-26 census of `done/unhooked-item-and-body-sound-families.md` — closing that family's routing left two rows whose clip is not merely unreported: the native call that would play it is suppressed or blocked on every side.
- Related: `done/unhooked-item-and-body-sound-families.md` (the census these rows come from), `done/remote-medical-treatment-operations.md`, `docs/architecture/remote-inventory-native-parity.md` (§3.4)

## The gap

Both rows share one shape: a native call that WOULD play the clip does not run on any side, so the
character-sound capture — which reports only a sound the native call actually played, and never
invents one — has nothing to carry.

| Row | What happens today | Why it is not just a missing scope |
|---|---|---|
| Remote limb treatment | The operator drops a medical item on the displayed limb → `PlayerCamera.TryPerformSpecialUIAction` → `TryHandleRemoteMedicalLimbUse` → the host-authoritative operation session. While the remote medical view is open the native `PlayerCamera.ApplyWoundItem` is BLOCKED (`RemoteMedicalBlockApplyWoundItemPatch`), so the item's own `useLimbAction` never runs and its clip (`syringe` / `splint` / …) plays on NO side: the operator, the patient and every third peer hear nothing. | Carrying it means the medical domain deciding to PLAY a clip the native path never played, and deciding where (the treated limb on the displayed body, or the patient's own limb). That is a new decision inside that domain, not a routing fix — inventing a sound is the character-sound family's stated non-goal. |
| World-item ground impact | `Item.OnCollisionEnter2D` (Item.cs:238-247) plays `drop` plus a block step sound and spawns dust. On a guest, `NonAuthoritativeItemImpactPolicy` suppresses the whole collision effect on the guest's non-authoritative copy, so a guest hears nothing when a world item lands — its own drops included — while the host hears its own authoritative copy. | There is no acting player to attribute the sound to, and the item-impact domain owns who simulates a landing. Relaying it needs a world-event carrier (an impact with a position), not a call-identity character scope. |

## Decisions (2026-09-26 cycle, user rulings)

1. **The remote limb treatment is carried** (user chose "carry it" over "record it as deliberately
   silent for everyone"). The OPERATOR's client plays the clip the item's own native limb action
   would have played, at the treated limb on the displayed body, once; the existing character-sound
   relay carries it to the patient and every third peer, and the source never hears its own echo.
   The clip identity is native knowledge the blocked call took with it, so CUO holds the mapping —
   censused per accepted item below, and gate-pinned so a new catalog entry cannot ship undecided.
2. **The world-item impact is carried, sound AND dust** (user chose "both" over "sound only" or
   "keep it silent"). The authority side (the host keeps the native presentation; a guest copy stays
   suppressed) reports one event per native impact through a dedicated world-event message, and the
   receiving side replays the game's own presentation: the `drop` clip, the material step sound the
   receiver's own world picks for the landing block, and the `DustMini` puff. The plush squeak, the
   family's other suppressed collision presentation, rides the same event.
3. **The remote-driven replay stays unchanged, and its consequence is recorded here** (decision 221:
   a remote-driven mutation must not report as the local player's action). The item's own sound
   plays where the mutation runs, so the owner hears a use their peers do not; that is the accepted
   price of the rule, not a defect this cycle fixes.

## Census (decompiled tree; `reversing/` line numbers are stable)

Every item the remote limb gesture accepts, with the sound its native `useLimbAction` produces. The
first half plays SYNCHRONOUSLY inside the delegate (which the remote view blocks, so CUO replays it);
the second half plays from a minigame the delegate starts, or plays nothing at all.

| Accepted items (CUO catalog) | Native limb-treatment sound | Carrier |
|---|---|---|
| `bandage` · `rippeddressing` · `sterilizedbandage` · `plasticbandage` · `analgesicgauze` · `alginate` · `rag` · `bruisekit` | `bandage` — the native `BandageMinigame.PhysicsUpdate` plays it (3D, at the item) when a wrap completes (BandageMinigame.cs:112) | per-step capture scope around `BandageMinigame.PhysicsUpdate` (this also fixes the LOCAL treatment, which never reported this clip either) |
| `musharm` | `goo` (Item.cs:616) before its `BandageMinigame` | remote-treatment table + the minigame scope above |
| `boneweldingtool` | `boneweld` (:696) | remote-treatment table |
| `clottingmush` | `goo` (:1589) | remote-treatment table |
| `chestdrain` | `syringe` (:1613) | remote-treatment table |
| `splint` · `carcasssplint` | `splint` (:1483 / :1509) | remote-treatment table |
| `tweezers` | `tweezeruse` (:1700) — its ONLY clip; the shrapnel minigame's `gore{N}` sits behind `!this.hasTweezers` (ShrapnelMinigame.cs:109) | remote-treatment table |
| `wrench` | `wrenchhit` (:4864) | remote-treatment table |
| `disinfectant` · `spraybottle` | `spray` (:2100 / :2124) | remote-treatment table |
| `paincream` (reliefcream) · `woundglue` (woundglue) | `cream` (Liquids.cs:1074 / :1100, reached through `WaterContainerItem.ApplyToLimb`) | remote-treatment table, keyed by the LIQUID the item holds |
| `bloodbag` · `bloodbaghuman` · `antiserum` · `streptokinase` · `bloodcoagulant` · `combatpen` | `syringe` (:1760 / :1926 / :1375 / :1734 / :1539 / :1565) | remote-treatment table |
| `saline` · `ringersolution` · `ceftriaxone` · `morphine` · `opium` · `heroin` · `fentanyl` · `naloxone` · `syringe` | none — the delegate starts `SyringeMinigame`, whose own cues are 2D screen feedback | deliberately silent, recorded |
| `aed` · `manualdefibrillator` | none — `AEDMinigame` / `ManualDefibMinigame` cues are 2D | deliberately silent, recorded |
| `tourniquet` · `icepack` · `makeshiftwrench` · `adhesivebandage` | none — the delegate plays no clip at all | deliberately silent, recorded |
| amputating tools (`machete` … `flimsyknife`) · `medicalsuture` | `gore` + `gore{N}` (amputation completion: `Limb.Dismember` → Limb.cs:91-99; `medicalsuture`'s delegate calls `Body.DoGoreSound`, Item.cs:378 → Body.cs:2443-2446) | CARRIED (2026-09-26 follow-up cycle, `done/treatment-gore-presentation-carried.md`): the gore clips join the medical set and are captured by the minigame STEP that plays them (`AmputationMinigameSoundPatch` + `ShrapnelMinigameSoundPatch`, the `BandageMinigameSoundPatches` shape), and the suture's blocked delegate clip is a row of the treatment table (`medicalsuture` → `"gore"`). The carrier came after a census: the operator's minigame dismembers the DISPLAYED limb and the patient's client applies the dismemberment through the kernel projection without ever calling the native `Dismember` (no double-play), the relay drops the source's own echo, and `ShrapnelMinigame.cs:109` keeps the break path off the tweezers — whose `gore` this cycle re-read as NOT played at all (both break conditions sit behind `!this.hasTweezers`), so the carried `tweezeruse` row is their only clip |

The world-item impact family is the two native collision presentations the guest-side guard
suppresses: `Item.OnCollisionEnter2D` (`drop` + the landing block's step sound + `DustMini`,
velocity > 3) and `PlushScript.OnCollisionEnter2D` (the plush's own squeak, velocity > 2).

## What done looks like

1. Each row is either carried by a decided mechanism, or recorded as deliberately silent with the
   reason on this ticket — for the treatment row that means the user accepting "the operation is
   silent for everyone".
2. The treatment row is decided WITH the remote-medical domain: which client plays the clip, at which
   position, and whether the patient's client would then double-play it.
3. Any wire change bumps `ProtocolVersion.Current` in the same change (a fact of the change, not a
   constraint on it).

## Acceptance criteria

| # | Scenario | Expected | Verified by |
|---|---|---|---|
| 1 | A player treats another player's limb through the remote medical view with a splint / bonewelding tool / chest drain / clotting mush / wound glue / pain cream / disinfectant / spray bottle | the operator, the patient and every third peer hear the exact native clip once, at the treated limb; no double audio | the remote-treatment table pins + the play-site pin + the existing relay tests; the audible half is the user's dual-client pass |
| 2 | The same, with an item whose limb action starts the native `BandageMinigame` (bandage, ripped dressing, rag, …) | every peer hears `bandage` once when the wrap completes | the minigame-step scope pin + the classification pin; the audible half is the user's dual-client pass |
| 3 | A LOCAL limb treatment with a bandage item (no remote view) | every other peer hears `bandage` — the clip the previous cycle's census missed | same pins (the scope is the treatment's, not the view's) |
| 4 | An injection (morphine / saline / syringe …), the AED and the manual defibrillator | unchanged: no 3D clip plays natively, so no peer hears one; the 2D minigame cues stay the acting player's own | the uncarried rows of the census + the 2D pin |
| 4b | An amputation, a medical suture, or a bare-handed shrapnel removal | CARRIED by the follow-up cycle (`done/treatment-gore-presentation-carried.md`): every peer hears the `gore` clip(s) the acting client's own minigame step played, exactly once, at the patient's live position; the suture's blocked delegate clip is the treatment table's own row, and the tweezers play nothing | that cycle's census, gate pins (`AmputationMinigameSoundPatch` + `ShrapnelMinigameSoundPatch` + the medical set + the table row) and its own self-check; the audible half is the user's dual-client pass |
| 5 | Any world item lands (host-side or guest-side drop, a container spilling, a falling stack) | every client hears the `drop` clip and its landing block's step sound once and sees one dust puff; the host's own copy keeps its native presentation | the impact-event pin + the replay decision pin + the suppressed-guest pin; the audible/visible half is the user's dual-client pass |
| 6 | A plush toy is knocked into anything | every client hears the plush's own squeak once | same pins |
| 7 | Solo / no active session | nothing is reported, local presentation unchanged | the session-active guards |
| 8 | A remote-driven item use (peer-driven, the owner's client replays it) | unchanged: the owner hears it, the peers do not (decision 221) | this ticket's decision 3 |

## What landed (2026-09-26 cycle)

- **The bandage minigame's own step is captured** (`BandageMinigameSoundPatches`): the clip the
  previous cycle's census missed (`bandage`, played frames after the limb action returned) now reaches
  every peer for the local AND the remote treatment. `CharacterSoundPolicy.IsMedicalClip` carries it.
- **The remote limb treatment plays the clip its blocked native call would have played**
  (`RemoteMedicalTreatmentSoundCatalog` + `RemoteMedicalOperationHandler.PlayTreatmentSound`): the
  item's own limb-action clip, or the applied LIQUID's clip for the topical containers, at the treated
  limb of the displayed body, from the operator's client, inside the treatment capture scope. It is
  played only after the dispatch succeeded, and 31 accepted items are recorded as natively silent
  rather than left undecided.
- **The world-item impact is carried, sound AND dust** (`ItemImpactMsg` = 141, host → guest only): the
  authority's native collision presentation is reported once per collision (the native
  `relativeVelocity` threshold decides, not a second one) and every guest replays the game's own
  `drop` clip, its own landing-block step pick and the same `DustMini` under `RemoteApply`. The plush
  squeak rides the same event with its sound index. The guest's non-authoritative copy stays
  suppressed, so nothing double-plays, and `NonAuthoritativeItemImpactPolicy.ShouldReport` is the
  exact mirror of the rule that suppresses it.
- **The third row is recorded, unchanged**: a remote-driven item use still never reports as the local
  player's action (decision 221), so its owner hears a use the peers do not (decision 3 above).
- **The first census of this cycle was incomplete, and the independent review caught it**: it read the
  accepted items' delegates but not what those delegates CALL. `medicalsuture` (`Body.DoGoreSound`) and
  the amputating tools (the native `AmputationMinigame`'s completion → `Limb.Dismember`) play a 3D
  `gore` / `gore{N}` that this table does not carry, and the suture's row had even been recorded as
  "natively silent". The rows are now recorded truthfully as UNCARRIED with the reason, and the
  carrier decision is ticketed (`done/treatment-gore-presentation-carried.md`) because the
  dismemberment is also applied on the patient's own client — the double-play census comes first.
- **The gore rows are carried by their own cycle** (2026-09-26 follow-up,
  `done/treatment-gore-presentation-carried.md`): the user ruled for carrying the clip, so the
  amputation and shrapnel minigame STEPS that play it are captured where they run
  (`AmputationMinigameSoundPatch` + `ShrapnelMinigameSoundPatch`), the `gore` / `gore{N}` clips join the medical set, and the suture's
  blocked delegate clip is a treatment-table row. The carrier was decided on the census, not assumed:
  what the review recorded as a possible double-play resolved the other way — the patient's client
  never runs the native `Dismember`.
- **The wire books keep up in the same change**: the new host-only message joins the vocabulary index
  and the sync-coverage matrix (row I9, `Transient-by-design` — no fallback by design), and
  `ProtocolVersion.Current` moves to 43 with its per-number log entry.

## Evidence

- Self-check: `docs/evidence/selfchecks/presentation/suppressed-native-call-sounds-stay-unheard-selfcheck.md`
  (mechanism inventory, the per-item census, the ladder, the review disposition and the limits)
- Gate: `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ItemAndBodySoundCaptureGateTests.cs`
  (the bandage scope, the treatment table's completeness, the play site) and
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ItemImpactPresentationGateTests.cs` (the
  authority-only report, the native threshold, the host-only event, the game's own replay, the pure
  presentation table) — red 9/28/37 against the pre-change `src/`, green after it
- Behaviour: `RemoteMedicalTreatmentSoundCatalogTests`, `ItemImpactPresentationTests`,
  `CharacterSoundPolicyTests.BandageMinigameClip_IsCarriedFromTheLimbTreatmentScope`,
  `NonAuthoritativeItemImpactPolicyTests` (reflective, Integration)
- Code: `src/CasualtiesUnknownOnline.GameAdapter/Patches/{BandageMinigameSoundPatches,ItemCollisionEnter2DPatch,PlushScriptCollisionEnter2DPatch,NonAuthoritativeItemImpactGuard}.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/{RemoteMedicalOperationHandler,World/WorldItemImpactSync,World/WorldItemImpactReplay}.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteMedicalTreatmentSoundCatalog.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Items/{ItemImpactPresentation,ItemMessageFlowService,ItemService}.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/{ItemImpactKind,ItemImpactMsg}.cs`
- Native: `Item.cs:238-247/616/696/1375/1483/1509/1539/1565/1589/1613/1734/1760/1926/2100/2124/4864/1700`,
  `Liquids.cs:1074/1100`, `BandageMinigame.cs:112`, `PlushScript.cs:17-23`
- Gate inventory: `docs/evidence/normative-gates.md`

## Limits

- The audible and visible half is the user's dual-client run (self-check §8): the treatment's clip at
  the treated limb, one landing sound and one dust puff per client, no double audio.
- The treatment table is native knowledge CUO maintains: the gate notices an accepted item with no
  decision, not a game update that changes a clip.
- The treatment clip plays when the GESTURE dispatches, not when the native delegate's own local guards
  would have let it run: a wrench on a healthy limb, a splint on a head/vital/already-splinted limb or a
  chest drain off limb 1 gets a clip the native call would not have played (the host refuses the
  operation, so the sound is the only thing that happened). Mirroring those per-limb conditions is a
  separate decision, recorded here rather than half-built.
- The treatment clip's POSITION is the treated limb's body for every row (the natives differ: `musharm`'s
  `goo`, the two `spray` rows and `wrench`'s `wrenchhit` use the limb's or the item's own transform). The
  relayed position is the treated limb, which is what a peer should hear.
- The impact event follows the authority's landing, so a guest's sound can trail its own visual copy
  by the position stream's cadence; the plush squeak is skipped if the receiver's copy is not yet at
  the impact position (logged).

## Non-goals

- Not re-deriving the capture chain: `done/unhooked-item-and-body-sound-families.md` landed the
  routing, the scope guards and the census gate.
- Not a general "every game sound becomes a message" sweep: each row needs its own decision, and
  "the sound stays silent, because …" is a valid outcome that must be written down.

## Row 2 read from the LISTENER side (2026-11-01, 07:00) — CORRECTED, see below

The row — a bandage whose limb action starts the native `BandageMinigame`, "every peer hears `bandage`
once when the wrap completes" — was staged end to end on a fresh three-client session and measured on the
peer, not in a log: a new read-only recipe (`audio-recent`) samples every `AudioSource` in the scene and
answers the clips playing right now, and a sampler loop on the GUEST caught this while the HOST played the
treatment on the guest's limb 4:

    playing=1, matches="bandage"

So the peer's client really plays the game's own `bandage` clip during the remote treatment, and it is one
window, not a loop. The host side of the same run completed normally (`operation 1 (Bandage) ended …`,
terminal `Completed`).

Row 3 (a LOCAL bandage treatment, no remote view) is the same instrument pointed the other way — the host
samples while the guest treats its own limb — and row 4b is the carried follow-up's. The instrument is
committed as `tools/acceptance/recipes/audio-recent.cs`.

and row 3 is blocked by the fixture rather than the instrument: a LOCAL bandage treatment needs the guest to OWN a bandage, and a member cannot give itself one — `/give` is a cheat-gated command and the guest is not on the host's granted list, so it answered `refused locally` (its own message naming the grant). A world pickup or a host-side give would stage it; that is the next batch's first step.

## Row 5 read: a landing world item plays on the acting client and is NOT relayed (2026-11-01, 07:25)

The instrument pointed at row 5 ("any world item lands -> every client hears the `drop` clip"): `item-drop type=bandage,height=30`
spawns a world item through the game's own `Utils.Create` and lets it fall, and `audio-recent match=drop` samples a client's
`AudioSource`s while it lands. Both sides, same action, same window:

- **The acting client plays them.** Sampling the host with no filter caught a frame whose playing list contained four clips
  whose names carry the action: `1|drop`, `6|drop`, `13|drop`, `4|drop` — the landing sound really exists locally.
- **The peer plays nothing.** The guest sampled with the same filter at a tightened cadence across the whole fall (two runs,
  one of them with the sampler started four seconds before the drop) and answered **zero** playing clips for the entire window.

So row 5's "every client hears the `drop` clip" is not met today: the landing sound is a LOCAL family, like the nap's `stretch`.
That is the ticket's own kind of outcome ("the sound stays silent, because ..."), and it is now written with its reading rather
than inferred.

Row 6's plush-toy half needs a plush item id this machine's catalog does not hand the batch; row 3's fixture gap is recorded above.

## Row 1's local-treatment fixture, read as far as the batch can take it (2026-11-01, 07:55)

Row 1 is "a player treats a limb (syringe / splint / goo / bandage) -> every other client hears the exact clip". The remote half of that family is read (row 2, the peer really plays `bandage`); the LOCAL half needs a player treating its own limb, and the batch tried both ways an agent can reach it:

- **Through the inventory use path.** With a bandage in slot 0 and the local wound panel open (`open-own-wound` answering `open=True`), `item-use type=bandage aim-x=640 aim-y=395` answered `usable=False, aimed=True` both on a healthy body and after the same body was wounded to `skinHealth=70.96`; a second attempt aimed from `screen-point2` of the body's own position answered the same. So the aim the recipe takes is not the wound panel's limb target — `usable` stays false because nothing in that panel is selected by a screen point.
- **Through a second player's chart.** No: that is the remote path, which is row 2 and already read.

So row 1 waits on the game's own limb selection inside the wound panel — a click on the panel's limb — which is the OS channel, not an in-process recipe. The row is parked on that (one real click with the panel open), and the peer-side instrument for it is already built and proven (`audio-recent`).

## CORRECTION (2026-11-01, 08:05): the one-second poll is not evidence, and the peer caught nothing under a frame-accurate watch

The reading above came from a sampler that asks one instant per driver call, and a driver call costs 0.3-0.6 s of process startup — so a short clip can fall entirely between two polls, and one catch could equally have been a different source. A frame-accurate watcher now exists (`audio-watch2` declaration + `audio-watch2`/`audio-watch2-read` recipes): it runs INSIDE the client, re-scans the scene's `AudioSource`s every fifth frame and records every clip name it has ever seen playing. Validated on an ambient clip it answers `hits='wind|windLoop|'`, so the instrument works.

Under that watcher the peer caught **nothing** for a completed remote bandage treatment: the guest ran 1730 frames across the release, the minigame and its end (`minigame-end` answering `ended=true`), and its hits string stayed empty. Its own log for the same window carries no `[CharacterSound]` line for the bandage — only an unrelated `[Sound] lifePodHit` pair.

So row 2 is **NOT proven**; the earlier single catch is not reproducible and must not be cited as a pass. The next reading is the relay itself: whether `CharacterSoundSync` reports the operator's `bandage` clip at all (its send-side line for the treatment window), before any listener measurement can mean anything.

## FINAL reading (2026-11-01, 08:20): both clips DO reach the peer — the earlier "nothing" was a broken instrument

The watcher above cached the scene's `AudioSource`s once, and `Sound.Play` creates a NEW GameObject with its own source per call (`reversing/Assembly-CSharp/Assembly-CSharp/Sound.cs`: `new GameObject("a", new Type[] { typeof(AudioSource) })` then `PlayOneShot`), so a cached scan can never see a one-shot clip. `audio-watch2` re-scans every fifth frame, and it was validated the honest way: it catches an ambient loop (`wind|windLoop`) and a one-shot the game plays through the same path (`vomitwarning`, from `body-call kind=vomit`).

With that instrument, the SAME two actions that answered "nothing" before:

- **Row 2 (a remote bandage treatment, watched on the peer): `bandage` is caught.** The run drove the minigame to a completed wrap (`minigame-live` answering `lastAction=300, fill=1`) and the peer's hits gained `bandage`. The earlier empty run had NOT wrapped (`fill` under 1), and the clip lives only in the wrap branch (`BandageMinigame.PhysicsUpdate`: the `bandageAngle > 350` wrap case is the only `Sound.Play("bandage", …)` in that file).
- **Row 5 (a landing world item, watched on the peer): `drop` is caught.** `item-drop type=bandage,height=45` on the host, the peer's hits gained `drop`, so the authority-side impact presentation does reach the guest through `ItemImpact`.

Both rows are therefore proven from the listener side, and the two earlier notes on this page are superseded by this one. **A caveat the next batch needs**: the watcher's hit list is static and accumulates across starts, so a reading must be diffed against the previous list, not read as "the clip played in this window" on its own.

## Row 5's "once", counted (2026-11-01, 08:30)

The counting watcher (`audio-watch3`) makes "once" countable: one play is one `AudioSource` instance, and `Sound.Play` creates a fresh one per call. A fresh session, both clients watching, ONE host-side landing item (`item-drop type=bandage,height=60`):

    host plays=2   guest plays=2

The item bounces once, so two collisions are two presentations — and the counts MATCH, which is the row: the guest hears exactly the authority's presentations, once each, with no extra ghost echo of its own.

## What the LOCAL-treatment row needs, read off a frame (2026-11-01, 09:00)

With the CUO window closed and `open-own-wound` answering `open=True`, the captured frame shows the game's own wound view: the body silhouette with its limbs, a selected-limb panel on the left (reading its bar values) and the limb icons over the body. So a local treatment selects its limb by clicking the DRAWN LIMB, and the item is then used on that selection — which is why `item-use` answered `usable=False` for every aim point this batch tried: the aim never landed on a limb of that panel, and the panel had another limb selected (a healthy one read 100/100 while the wounded limb was a different one).

That makes the local-treatment row (and row 3 of this ticket) wait on a click inside the game's wound view — the OS channel, or a UI-click instrument the batch does not have. The remote half of the same family is read (rows 2 and 5, above).

## The local-treatment entry, found in the game's own code (2026-11-01, 11:50)

The wound view does not select a limb by clicking it: `WoundViewLimb` is an `IPointerEnterHandler` (`reversing/Assembly-CSharp/Assembly-CSharp/WoundViewLimb.cs`: `public class WoundViewLimb : MonoBehaviour, IPointerEnterHandler`, whose `OnPointerEnter` sets `this.woundview.limbLookingAt = this.limb`), and `WoundView.Update` copies `body.limbs[limbLookingAt]` into `PlayerCamera.main.selectedLimb` every frame — the field the item's own `useLimbAction` reads. So the honest local-treatment entry is a POINTER MOVE onto the target's rect, not a click, and the rects do not line up with where the body is drawn (a point over the drawn arm selected the abdomen in the first attempt).

A new read-only recipe now asks the game where its own targets are: `wound-limb-points` answers all fifteen `WoundViewLimb` anchors in GUI coordinates (origin top-left, the way a frame reads). On this build it answered `count=15` with `5:445:273`, `4:506:273`, `1:640:277` and so on — limb index to screen point. With that a batch can move the staged pointer onto the limb it means to treat and then use the item, which is the step rows 1 and 3 of this ticket need.

Two frames record the attempt: with the window view open, the panel read the abdomen (its own label, quoted in the frame) after a pointer move to the drawn arm's position, and the anchor read above explains why. The next batch starts from `wound-limb-points`, not from a pixel guess.

### The selection now works in-process; the item still answers "not usable" (2026-11-01, 12:05)

A point dispatch lands on the parent image the raycast returns first (`target=Limbs`, no handler), so `ui-click` gained a `limb=n` form that addresses the widget the game itself keys by index and dispatches the same `OnPointerEnter`: `ui-click x=445 y=273 limb=5` answered `ok=true, target=Torso (5), enter=true`. The view's own panel then follows the selection, which is the entry this ticket's local rows need.

What did NOT follow: `item-use type=bandage` still answers `usable=False` at every limb tried (3, 4, 5, 7, 8), so no minigame starts and nothing is heard. The next reading is the source of that flag — whether `usable` is the item's own `usableOnLimb`/condition gate, a bleed requirement the seeded wound does not satisfy, or the hand/active-slot state — before the rows can be judged; the wound the batch seeds is `skinHealth=60.4` with no bleeding, which is the first thing to vary.

## Rows 1 and 3 read: a LOCAL limb treatment, and the peer hears the clip exactly once (2026-11-01, 12:40)

The missing entry was not a click at all — it was the wrong FUNCTION. `Body.UseItem(item)` runs `item.Stats.useAction` only when `item.Stats.usable` is true, and a bandage's stats answer false, so every local treatment attempted through the `item-use` recipe was a silent no-op. The game's own entry for a limb item is `PlayerCamera.ApplyWoundItem(item)` (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs`), which the wound view's use button runs: it reads `selectedLimb`, applies the medical cutoff and `ActuallyUsableOnLimb`, and calls `item.Stats.useLimbAction(this.selectedLimb, item)`. A new recipe (`limb-use type=<id>`) calls exactly that.

The chain, on the deployed build, host and guest in one session:

1. the host's own wound view opened (`open-own-wound` answering `open=True`) and the view's own limb widget selected through its `IPointerEnterHandler` (`ui-click limb=8` -> `target=Torso (8), enter=true`; the panel then reads the selected limb (the game's own label for the left hand));
2. that limb wounded (`wound-seed limb=8 force=150` -> `skinHealth=60.4`);
3. `limb-use type=bandage` -> the game started its own `BandageMinigame` on the host's body (`minigame-live` answering `kind=BandageMinigame`);
4. the minigame driven to a completed wrap (`fill=1, lastActionAngle=160`) with the peer's frame-accurate watcher running (`audio-watch3 match=bandage`).

The peer's answer, after the wrap: **`plays=1, names=[bandage]`**. One playback, and it is the game's own clip — so row 1 ("a player treats a limb -> every other client hears the exact clip") and row 3 ("a LOCAL bandage treatment is heard by peers") both read PASS from the listener side, with the same instrument that proved rows 2 and 5.

### Row 4b's amputation attempt, and where it stopped (2026-11-01, 13:00)

The same entry should start an amputation — `limb-use type=machete` on a selected limb — and it answered `not-held` although `/give @s machete` reported "placed in slot 0". The census of the same body then listed the slots it does hold (`lantern@slot3, dogfood@slot4, waterbottle@slot5`) and no machete, so the item is not in any slot the body reports: either the give placed it somewhere the six-slot read cannot see (the HAND is a separate index, which `limb-use` now checks first), or that give did not land at all. The next reading is the give's own answer plus the body's hand slot; row 4b stays unproven until a machete is actually held when the limb is used.

### Row 4b: where the given machete actually goes (2026-11-01, 13:35)

The give is not the problem and neither is the search: with `machete` given (`placed in slot 0`) and the recipe reporting every slot it can see, the body answered

    handSlot=0; s0=-; s1=-; s2=-; s3=lantern; s4=dogfood; s5=waterbottle

so the machete is in NO slot the body indexes — including the hand slot the give named. A `plushie` given the same way DOES land in a slot (the drop recipe pulled it out of one immediately afterwards, twice). The difference is the item: a weapon is taken by the game's equip path, which is not a backpack slot, so a recipe that searches slots cannot see it. The next reading is that path (`Body.WeildItem`/the equipped-item field) and the fix is to read the equipped item as well — the same shape as the hand slot, one level further out.

### Row 4b: the machete was invisible because the body was DOWN (2026-11-01, 13:55)

The earlier "no body slot holds machete" had a cause and it was not the item: a give to an UNCONSCIOUS body reports `placed in slot 0` and leaves the slot empty, and every weapon-shaped item tried (`machete`, `flimsyknife`) failed the same way. After `/heal @s` the very next give landed for real — `limb-use type=machete` then answered `ok=true, slot=0, usableOnLimb=true, actuallyUsable=true`, and the frame shows the machete equipped in the hand. **So the fixture rule is: heal the body before giving it anything**, and the same rule explains the two earlier "the give said slot 0 but the body is empty" readings on this page.

With the item held, neither entry started the amputation: `ApplyWoundItem` returned ok and no minigame, and the game's special-action entry (`PlayerCamera.WoundSpecialAction`, which is what the wound view's special button runs) also left `minigame-live` answering `none`. The limb the view had selected was the left hand. The next reading is what that action requires — the limb's own state (a dismemberable limb, a shrapnel-bearing limb) or the special button's own availability — before row 4b can be judged; `limb-use mode=special` already exists to drive it.

### Row 4b: the amputation minigame starts, and what it needs (2026-11-01, 14:15)

`Item.DoAmputate` (`reversing/Assembly-CSharp/Assembly-CSharp/Item.cs`) is the gate, and it needs an INFECTED limb, not merely a wounded one:

    if (limb.infectionAmount > 60f && Array.IndexOf<Limb>(limb.body.limbs, limb) > 2 && (!flag || !limb.isLegLimb))

Infection only grows with game time, so the fixture seeds it (`wound-seed ... infection=n`, a substitution the recipe names in its own answer). Two readings make the condition concrete: seeded to `75` the amputation did NOT start (`minigame=none`) because the value had decayed to `55.22` by the time the machete was used; seeded to `300` and used in the same breath, `minigame-live` answered **`kind=AmputationMinigame`** on the host's right forearm.

So row 4b's remaining need is the MINIGAME's own motion, not its trigger: the batch's alternating staged moves kept it running (still `AmputationMinigame` after thirty steps) and the peer's watcher recorded only ambient `drip*` clips, i.e. the gore sound has not been reached yet. That is the same shape as the bandage ring — a small in-client driver for this minigame's own gesture — and it is the next step here.

### Row 4b: the amputation minigame runs, and what its gesture still needs (2026-11-01, 15:05)

The trigger is settled (an infected limb over 60, used with a cutting tool in hand, starts `AmputationMinigame`), and three readings now name what the batch's driving is missing:

- **`cutting` is a POSITION test.** `AmputationMinigame`'s private `cutting` getter is `Minigame.game.handPos.y <= maxHandPos + 10f`, and `maxHandPos` is `Mathf.Lerp(120f, -380f, cutProgress)` — so the sawing has to happen near the BOTTOM of the minigame surface. Driving at the screen centre never cut (the limb's skin did not move); driving at `y = 100` did start damage (`skinHealth` 68.32 -> 66.1, `bleedAmount` 7.69, `pain` 9.18).
- **The hand has to be HELD, and the staged channel does not yet say it is.** `minigame-live` answers `handStarted=false, handClicking=false` throughout, while the same fields read `clicking=true` during the bandage minigame — so the amputation's own "start" never happens, and the cut progress (which only accrues while `cutting`) stays at zero. `VirtualInput` stages a one-frame `PressKey` tap and a holdable `Press(button)`; both were tried and neither moved `handStarted`. That is the next reading: which of the minigame's own fields `Minigame.game` reads for the press, before another driver shape is tried.
- **The peer has heard nothing yet**, which follows from the above: its counting watcher recorded only ambient clips (`pant`, `wind`, `drip*`) across the whole attempt, so no gore clip has been reached rather than reached and missed.

The driver lives in `tools/acceptance/driver/eval-declarations/amp-drive.cs` and is kept as the honest state of that attempt: it holds the button, alternates the pointer every frame at the bottom of the surface, and is ready for the one field reading above.

## Rows 5 and 6: the drop recipe is broken on this build (2026-11-02, 01:40)

Attempting rows 5 and 6 first needed a dropped item, and the recipe that provided one in the earlier batch no longer compiles: `item-drop` (and a fresh copy under a new name, `item-drop2`, created to rule out a cached failure) both answer `eval-error` with `(1,1): InteractiveHost`, which the driver documentation lists as the evaluator refusing the submission outright rather than a symbol error.

That is a tooling defect, not a reading: the recipe is one top-level expression, UTF-8 without BOM, and the same shape as the recipes that still run. The next action is therefore to replace its body with the game's own drop entry and re-run it, then judge rows 5 and 6 with the `drop` counter (`plays`, not a `names` diff).

The rest of this session's sound readings stand: `dogshake`, `vomit2`, `stretch`, `pain1` and `gore2` all reach peers, each confirmed by a playback count rather than by first sighting.

## Rows 5 and 6, read after the drop recipe was repaired (2026-11-02, 13:50)

The recipe was broken by its own numeric argument: the driver substitutes `{{n:height}}` as a bare `1.5`, which is a double, and `new Vector2(float, double)` does not compile — Mono.CSharp reports that as `(1,1): InteractiveHost`, the same shape as a refused submission. Casting the placeholder to float fixed it, and a fresh name failed identically, so the name was never the problem. Lesson for the driver document: a numeric placeholder used where a float is expected is cast in the recipe.

The native site is `Item.OnCollisionEnter2D` (`reversing/Assembly-CSharp/Assembly-CSharp/Item.cs`): a collision whose relative velocity exceeds 3 plays `Sound.Play("drop", …)`, a material step sound and a dust puff. Every bounce above that speed plays again, so one drop may legitimately produce several plays.

| direction | acting client | listener | `drop` playbacks |
|---|---|---|---|
| host drops a lantern 1.5 above | 0 | **4** (guest) |
| host drops a lantern 0.6 above | 0 | **4** (guest) |
| host drops a lantern again (second window) | 0 | **4** (guest) |
| guest drops a lantern | **7** (own) | 0 (host) |
| either client drops a plushie | 0 | 0 |

Two findings follow, and neither is the row the ticket expected:

1. **The peer DOES hear a dropped world item** — the row passes for the listening side. Guests hear the host's drops and their own.
2. **The HOST hears no item landing at all**, not even its own. Four windows, four zeroes, while the guest on the same windows counted four each. That is the shape "suppressed native call sounds stay unheard", but on the acting side rather than the listening side, and it needs its own cause reading before anything is changed.

Row 6 does not pass or fail yet: a `plushie` created through the same factory produced no `drop` playback on the client that hears every lantern landing — so either its entity is not the item prefab the collision handler lives on, or it never fell. The next reading is what `Utils.Create("plushie", …)` actually instantiates, before any claim about a toy sound.

## Rows 5 and 6 are blocked on an instrument, not on a behaviour (2026-11-02, 14:20)

Three attempts, each with the reading that stopped it:

1. **The raw create is not a world item.** `item-drop` calls `Utils.Create`, and after that call `item-world-read` answers `count=0` on the acting client AND on the peer, at a radius that comfortably covers the drop point. Whatever the call returns is not in the world table, so the four `drop` playbacks seen earlier came from something else and cannot be attributed to it.
2. **A native drop at the feet is silent BY DESIGN.** `item-drop-hand slot=3` (the game's own `Body.DropItem`, `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:1431`) answered `dropped=true`, and neither client counted a landing sound — because the native collision handler gates on `collision.relativeVelocity.magnitude > 3f` (`Item.OnCollisionEnter2D`), and an item released at the player's feet never reaches that speed. A silent drop at the feet is correct behaviour, not a defect, and it cannot judge this row either.
3. **`/summon item lantern` produced neither.** The command answered with a real instance id at the requested point; no landing sound appeared on either client and `item-world-read` still answered `count=0` there. Until an item the game registers can be produced and read back, "the peer hears the landing sound" has no fixture.

The instrument that owns this is the registration read: `item-creation-registration-first` exists for exactly "what does a created item have to be before it is a world item". Rows 5 and 6 wait on it, and the honest status of this ticket is that its remaining rows are fixture-blocked with those three readings, not that the behaviour is wrong.

## Row 1: the native chain says when a treatment makes a sound (2026-11-02, 14:50)

Reading the game first changed what the row can ask. `PlayerCamera.ApplyWoundItem` (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs`) does not run a generic item use: when the item is `usableOnLimb` it calls `item.Stats.useLimbAction(limb, item)`. The splint's `useLimbAction` (`Item.cs`) starts a `BandageMinigame` whose completion callback heals the limb — and the `bandage` sound is played from inside that minigame when a full wrap completes (`BandageMinigame.cs`, `Sound.Play("bandage", Minigame.game.currentItem.transform.position, …)` after `didWrap`). The `splint` clip lives in the item's GENERIC `useAction`, which this path never reaches.

So "apply a splint and the peer hears it" is not a native expectation: applying starts the minigame silently, and the sound arrives when the wrap completes. Read out loud, with instruments:

- `limb-use type=splint mode=apply` answered `ok=true, limb=0 (head), skinBefore=100`; the `splint` playback counter did not move on the acting client (230 → 230) nor on the peer (206 → 206) — which is exactly what the native chain predicts, since no wrap completed.
- Control: `play-clip clip=splint` moved the acting client's own counter (230 → 231, `present=true`), so the instrument CAN see that clip. The silence is the game's, not the watcher's.
- The wrap could not be driven this round: `wound-limb-points` answered `count=0, limbs=""` (the wound view was open but no limb point was produced), so `ui-click` had no anchor to select limb 2, `limb-use` kept applying to the head, no minigame started (`minigame=none`) and `bandage-rub` had nothing to finish.

Next, precisely: make `wound-limb-points` produce its anchors (it answers zero while the view reports open), select the wounded limb, apply the splint there so the minigame starts, complete the wrap with `bandage-rub`, and read the `bandage` counter on both clients. That is a fixture step, and it is the only thing between this row and a verdict.

## The wrap fixture, second batch (2026-11-02, 15:10)

`wound-limb-points` answers 15 anchors once the wound view is actually up — the earlier zero was the view being closed again by the panel's own close click, and `open-own-wound` reports `open:true` for the CALL, not for the resulting state, so the only honest check is the point count.

Then the two readings that stop the row again:

- **The anchor does not select the limb it names.** Moving the staged pointer to the anchor reported for limb 2 (`640:339`) and applying the splint made `limb-use` report `limb=12 (left thigh)` — so the rect the recipe reads is not the rect `OnPointerEnter` fires on at that point. The recipe reads the widget's centre, and the limb widgets overlap; a point inside one rect is inside several.
- **The limb action ran but no minigame started.** The apply answered `ok=True, usableOnLimb` inside the same response, and `minigame-live` right after answered `none`; `bandage-rub` then had nothing to finish. Either `MinigameBase.main.StartMinigame` refused, or the minigame opened and closed inside the same frame.

Two batches on this row without a verdict, so it stops here for now and the ticket moves to the next row: the anchor ambiguity and the minigame start are the two things to fix before the treatment-sound row can be judged at all.

## The world-item row needs a faster sample, not a different fixture (2026-11-02, 14:47)

The `item-drop` fixture works with a real item id — `type=stick` answers `{"created":true}` and drops it from the requested height; `type=rock` fails inside the game's factory (`Cannot instantiate a ScriptableObject with a position and rotation`), so the row's fixture is a stick, not a rock.

With the item created 6 units up and sampled every ~220 ms through `audio-recent match=drop` on BOTH clients, neither side answered a playing `drop` clip in five samples. So this pair cannot yet tell "the guest's impact is suppressed" from "the host never played it in the sampled window": the clip is a one-shot, and both the sampling interval and the moment the item lands are uncontrolled.

The next reading is the host's own `Sound.Play("drop", …)` call (a log line at the call, or a probe that counts drop plays) so the host side is a fact rather than a miss, and a sample loop that starts before the drop and runs through the landing.

### Close the window instead of widening it (same batch)

A 20-unit fall (≈2 s of flight) sampled eight times from the drop call onwards answered no playing `drop` clip on EITHER client. That rules out "the sample started too late" as the explanation for the earlier miss and leaves the observer itself: `audio-recent` can only see a one-shot that happens to be mid-play when it is asked, and a landing that occurs between two round trips is invisible to it no matter how long the fall is.

So the instrument that finishes this row is a RECORDER, not a sampler: a patch on the game's own sound entry (`Sound.Play`, the call the native impact path uses) that appends `(clip, frameTime, position)` to a ring buffer, plus a recipe that reads it. It belongs in `CasualtiesUnknownOnline.Acceptance` — the batch-only assembly — because it exists purely so a batch can see a one-shot after the fact. With it, "the host played `drop` at frame N" and "the guest played nothing within K frames of its clone landing" are both facts, and this row (and the other one-shot rows beside it) becomes judgeable instead of lucky.

### The recorder is installed; the fixture never makes the sound (2026-11-02, 15:02)

Two readings settle the instrument and move the question to the fixture:

- `sound-patch-state` (new recipe) asks Harmony about the game's own `Sound.Play` overloads and answers `prefixes: 5`, the string overload carrying `CasualtiesUnknownOnline.GameRuntime…, CasualtiesUnknownOnline.Acceptance` — so the recorder really is on the method. The first version was not: `Harmony.PatchAll(typeof(SoundPlayRecorder))` never reaches a NESTED type, which is why "armed" recorded nothing; the two overloads are now patched explicitly.
- With the recorder proven, `item-drop type=stick height=8` records NOTHING on either client — not even on the host, which is authoritative for that item. So `Item.OnCollisionEnter2D` (Item.cs:238-247) never ran its `Sound.Play("drop", …)`: the factory places the item without the fall this row needs, and the row's premise (a world item hitting the ground) is not what the fixture creates.

The next step is therefore a fixture that actually drops from a hand at height (`Body.DropItem`, the path a player uses) or a created item whose rigidbody is left simulated above the ground — not another sampling window.
### Registered but never called, through three fixtures (2026-11-02, 15:08)

The recorder is registered (Harmony reports a prefix owned by the acceptance assembly on `Sound.Play`) and the recipe arms and reads it, yet these produce `count=0` on the HOST — the authoritative side for the item:

- `item-drop type=stick height=8`;
- `item-drop type=stick height=3`;
- `/give stick` + teleport to open air + `item-drop-hand slot=0` (the player's own drop path, `Body.DropItem`), which answered `held=true dropped=true`.

One copy of the assembly is loaded (a recursive look under the game's BepInEx tree returns exactly one file), so this is not two `Enabled` flags. The remaining explanation is the classic one for a registered patch that never fires: `Sound.Play` is a small method the runtime has already inlined or JIT-compiled into its callers before the plug-in loaded, so the patch sits in Harmony's table and no call reaches it. The next reading decides it — a probe that calls `Sound.Play` directly through reflection from inside the game (one call, expected to land in the buffer if the patch is live at all) — and if that also misses, the recorder has to move onto a method that is not inlined, or be installed before the game's first sound.
### The recorder works, and it separates the two sides (2026-11-02, 15:22)

The instrument is proven end to end in one session:

- armed on both clients: `raw=582|` (host) and `raw=572|` (guest) — the prefixes see the game's calls, so this is not a dead patch;
- a compiled probe call through the game's own `Sound.Play` lands in the buffer exactly as written: `raw=594|probe@31.4`.

With that proven, the row's first honest reading on the deployed build: the host stands in open air, is given a stick, drops it from the hand (`item-drop-hand` → `dropped=true`) and the host records eight one-shots in the next four seconds — `9@41.3;10@41.3;6@41.4;6@41.7;3@46.0;5@46.0;9@46.2;7@46.4` — while the GUEST records nothing at all in the same window (`raw` unchanged from its arming value).

Two things follow, and both are concrete:

1. The clip names the game plays on this path are NUMERIC ids, not `drop`. So the row's fixture cannot assert on the literal name the ticket quotes from `Item.cs:238` (`Sound.Play("drop", …)`) — that call is one of the paths, and the impact that fired here went through a different entry.
2. The guest's window is empty even though the host played eight sounds, which is the shape the ticket predicts (a member does not receive the host's native one-shots). What is still missing to call it judged is the matching pair — the SAME sound on both sides at the moment the listener should hear it — and the numeric ids have to be mapped to their source first.
### A member''s own drop plays nothing on the HOST either (2026-11-02, 15:32)

The fixture that matters for this row is a MEMBER''s drop, because that is the path the mod has to replicate. Read on the deployed build, both recorders armed and both proven (each side''s own compiled probe lands in its buffer):

- the guest is given a stick (`/op`, then `/give stick` answering `Gave stick … in slot 0`), lifted ~22 units, and drops it: `item-drop-hand slot=0` answers `held=true dropped=true`;
- over the next five seconds the HOST records **nothing at all** (`raw` unchanged) and the guest records nothing either.

So on the authoritative side not one `Sound.Play` ran in the window — the item''s collision never happened there. That is not the ticket''s "the guest''s impact is suppressed"; it is one step earlier: **the member''s dropped item never becomes a simulated body on the host**, so no side can play its impact.

Two smaller facts from the same batch, both worth keeping:

- `/op` answers on the host immediately, but a member''s very next `/give` was still refused with "the host''s broadcast cheat list does not include you"; the next attempt a few seconds later succeeded. The grant reaches the member asynchronously, and a batch must re-try rather than conclude a failure.
- A host''s own drop from open air records eight one-shots (`9@41.3;10@41.3;6@41.4;6@41.7;3@46.0;5@46.0;9@46.2;7@46.4`), so the host path does produce impact sounds — which is what makes the member''s silence a difference rather than a quiet world.

The next reading is the one that separates the two remaining causes: read the host''s entity table (or a world-item census) immediately after a member''s drop to see whether the item exists at all, and if it exists, whether its rigidbody is simulated.
### Correction: `item-world-read` takes CELL coordinates, so those counts were not about the drop (2026-11-02, 15:45)

The three "no item after the drop" readings above were taken with `item-world-read x=<body x> y=<body y>`, and that recipe''s arguments are the absolute CELL to search, not a world position. The items it did return sit hundreds of units away, which is the tell: the query was never around the drop. So those counts say nothing about whether the dropped item exists and must not be used as evidence.

What the same batch does establish, with a reading that is valid:

- `item-floating value=1` reads the game''s own fresh-drop setting: `settingFound=true`, `settingBefore=true`, `staticBefore=true`, `armed=true`, `changed=false` — the `itemfloating` bool is already ON, so a dropped item is not destroyed on landing by `FreshItemDrop`. The "the item evaporates" hypothesis is therefore not the default state of this build.
- The sound reading stands on its own: with both recorders armed and proven, a member''s drop (`held=true dropped=true`) produced no `Sound.Play` on the host in five seconds, while a host''s own drop produced eight.

The next attempt must convert the drop''s world position to the cell the recipe expects (the game''s own conversion, not a guess) before it can answer whether the member''s item exists on the host.
### The `drop` sound IS replicated when the item lands normally (2026-11-02, 15:58)

With `item-world-read` fixed to take world positions (the recipe now converts through `WorldGeneration.WorldToBlockPos`), the same fixture that read as "no sound at all" gives the opposite answer at ground level:

- the member is given a stick and drops it from the hand while standing: `held=true dropped=true`;
- over the next three seconds the HOST records `drop@368.7;drop@368.7;2@368.7;drop@368.7;drop@368.7;3@368.7` and the GUEST records `drop@365.3;drop@365.3;9@365.3;drop@365.3;drop@365.3;11@365.3`.

So the member''s impact sound reaches the other client, played repeatedly because the item bounces (one `drop` per collision above the velocity threshold, exactly as `Item.OnCollisionEnter2D` says). The row is NOT "the member''s drops are silent".

The difference that produced the earlier silence is the HEIGHT: dropped from ~15-22 units in open air, the same call produced no `Sound.Play` on either client. Combined with the world-item reads (the two ground drops are present at the host as `stick@-1.3,495` and `stick@-1.8,495`), the shape is: a drop from height never lands anywhere, while a drop at ground level lands, bounces and is heard on both sides.

That is a different ticket''s subject (a drop from height leaving the world), and for THIS ticket the row''s claim is refuted with a reading.
### Remote limb treatment is heard on BOTH sides (2026-11-02, 16:05)

The row this ticket calls "Remote limb treatment" is the one the operator performs on another member''s limb. Read with the recorders armed on both clients, after the guest''s limb 0 was put into a real wounded state (`skinHealth=-105.6`, `bleedAmount=66.1`, `pain=225.9`):

- `remote-wound-special mode=open` on the host: `opened=true`, `viewOpen=true`, `displayBodyName=MedicalDisplay_110000135D30695`, `hasSnapshot=true`, `hasHealth=true`;
- `remote-wound-special mode=special`: `started=true` — the treatment ran;
- over the next three seconds the HOST recorded `heartthump…` (its own panel) plus `drip24`, `drip22`, `drip2`, `drip13`, `drop@500.7` and its block sound `1@500.7`;
- and the GUEST — the patient — recorded its OWN `drip19`, `drip15`, `drip13`, `drip8`, `drop@497.2` with block sound `2@497.2`, `drip18`, `drip17`, `drip1`, `drip2`, `drip7`, `drip24`.

So the treatment''s sounds reach the patient''s client: the patient plays the drip/bleed family and a `drop` of the same shape the operator''s side plays, at its own timeline positions. The row''s claim — that the native call sounds are suppressed and nobody hears them — does not hold for remote limb treatment on this build.
### What the remote seam actually dispatches, and the trap in my own fixture (2026-11-02, 16:12)

`TryStartRemoteWoundSpecial` does not start a bandage: it dispatches on the DISPLAY limb''s own condition — a `TourniquetScript` becomes a tourniquet removal, `hasShrapnel` a shrapnel special, a `SplintLimb` a splint removal, `dislocated` a dislocation use — and returns false for a plain bleeding wound. So `started=true` on a limb whose fields read `splinted=false, dislocated=false, shrapnel=0` means the limb carried one of those COMPONENTS (a tourniquet or a splint script) and the seam dispatched its removal, not a bandage.

That is also why `minigame-live` answered `kind=none` on both clients, and why the patient''s `bleedAmount` stayed at 63.1: a removal is not a treatment and does not open the bandage minigame. My earlier "started=true but nothing happened, a false success" reading was wrong for this reason and is corrected here.

To judge the bandage row the fixture must first REMOVE whatever the limb carries (or use a limb that carries nothing), then let the operator hold the bandage and go through `PlayerCamera.TryPerformSpecialUIAction` — the native path — and only then read the `bandage` clip at the wrap''s completion.
### The `bandage` row needs the panel drag; `Body.UseItem` never starts it (2026-11-02, 16:35)

The accepted-items table asks about the clip a bandage plays when it is APPLIED. On the deployed build, with the host''s own limb genuinely wounded (`wound-seed limb=1 force=900` → `skinHealth=-170.6`, `bleedAmount=93.1`), a held bandage and the wound panel open:

- `item-use type=bandage` through three different aims (`0,-2`, `0,-1`, `2,0`, which the recipe turns into `body.targetLookPos`) answered `usable=false` and left `minigame-live` at `kind=none` every time;
- the item''s own `Stats.usable` is false for a bandage, which is the game saying the generic use entry is not its path;
- the native chain for applying one to a limb is the wound panel''s special action (`PlayerCamera.TryPerformSpecialUIAction`, the same entry the remote treatment uses), not `Body.UseItem`.

So this row is not blocked on a sound question at all: it needs the LOCAL panel drag fixture (hold the bandage, drag it onto the limb in the panel, release), which is still missing from the toolbox. Every row of this ticket that a fixture CAN reach has now been read — two refuted with both-sides recordings — and this one names the fixture it lacks.
## Rows 5 and 6 read on the deployed build, with the plush id found (2026-11-02, 20:35)

Row 6 was blocked on "an item id the catalog does not hand a batch". The catalog does hand it: `item-census` gained a `mode=all` arm (288 registry entries) and the plush is **`plushie`** (weight 0.15, tags `belttool`).

**Row 5 (a world item lands).** A stick dropped from height 4 on the host, both recorders armed:

    host : drop@130.1 x4, drop@131.0 x4, drop@131.2 x2, drop@131.4 x2   (12 entries)
    guest: drop@129.0 x4, drop@130.0 x4, drop@130.1 x2, drop@130.4 x2   (12 entries)

Same clips at the same instants on both sides — the peer hears exactly what the acting side hears. The multiplicity is the item bouncing (each contact over the native `relativeVelocity.magnitude > 3f` gate plays it), and the quadruples are the recorder counting the `Play(string)` wrapper and the `Play(AudioClip)` it forwards to; both clients show the identical shape, which is what the row asks. A drop from height **0.5 plays nothing on either side**, which is the native gate behaving as read in `Item.OnCollisionEnter2D`.

**Row 6 (a plush toy knocked into something).** A `plushie` dropped from height 5:

    host : citrumeow@178.6, @178.8, @179.1, @179.2   (four impacts as it settles)
    guest: citrumeow@177.5, @177.7, @178.1, @178.2   (the same four, mirrored)

The plush's own squeak reaches the peer, once per impact, matching the host — row 6 PASS.
## Row 4 read: an injection plays on the patient''s own client only (2026-11-02, 20:50)

A syringe used on the operator''s own head through the game''s own entry (`limb-use type=syringe`, answered `usableOnLimb=true, actuallyUsable=true, limb=0` (the head) starts the native `SyringeMinigame` (`minigame-live`: `kind=SyringeMinigame, hasMain=true, handMagnitude=60`). With both recorders armed for the same window:

    host : heartthump x25 over ~4 s (its own body's cues, nothing else)
    guest: (nothing — raw counter moved, zero staged entries)

So the injection produces no clip on a peer, which is the row''s claim, and what the acting side does play is its own body''s cues rather than anything a peer would need. The `SyringeMinigame` needs no file, so the earlier "needs a syringe holding a liquid" note only applies to the families that read a liquid; a plain syringe is enough for this row.
## Why the machete "never appeared in a body slot" — it went into a container (2026-11-02, 21:05)

Row 4b''s blocker is explained. `/give machete` on a body that already carries a trashbag does not put the tool in a body slot: `item-census mode=body` lists the machete among the body''s carried items, while every body slot answers empty to `HoldingItem`, and dropping the trashbags (three `item-provide mode=drop type=trashbag`) makes the machete disappear from the carried list with them. So the tool had been placed INSIDE a container, which is why the earlier batch could never see it in a slot and why `limb-use` answered `not-held`.

Two probe fixes came out of it, both in `tools/acceptance/recipes/limb-use.cs`: the slot search now walks every slot the body owns instead of the six backpack indexes, and the `not-held` answer names what each slot holds so the next attempt is diagnosable in one call.

The row itself still needs the tool in the HAND on an empty-slot body; the honest next fixture is give-then-select on a body with no container in its slots (the guest is the better target — its slots are nearly empty).
### Row 4b: the tool reaches the limb, and the minigame still does not start (2026-11-02, 21:20)

With the tool-in-a-container problem solved by using the guest (whose slots are nearly empty), `machete` is carried in slot 0 and `limb-use type=machete` answers `usableOnLimb=true, actuallyUsable=true, limb=0`. But `minigame-live` answers `kind=none` right after it, so `PlayerCamera.ApplyWoundItem` on a HEALTHY limb does not start the amputation minigame — which is the game behaving correctly, not a fixture failure: an amputation needs a limb that is already mangled or dismembered.

`wound-seed limb=1 force=90` on the guest answered `null` (the recipe refuses on that body), and `limb-read` afterwards still shows `skin=100, bleed=0`, so the limb was not wounded and the amputation remains unstarted.

So row 4b''s remaining need is exact and small: a limb state the amputation/suture minigame accepts (a mangled limb, or a bleeding wound the suture treats), produced through a call this channel can reach. `wound-seed` is the right instrument and its refusal on the guest body is the next thing to read.
### Correction: it is the SELECTED limb, not a wound component (2026-11-02, 22:10)

The paragraph that stood here said the minigames need a wound OBJECT. That was wrong, and the code says so plainly:

- the bandage''s own `useLimbAction` (`reversing/Assembly-CSharp/Assembly-CSharp/Item.cs`, the `bandage` registration) calls `MinigameBase.main.StartMinigame(new BandageMinigame(...))` with no condition at all;
- the machete''s is `Item.DoAmputate(item, limb)`, whose gate (`infectionAmount > 60f && index > 2 && (!flag || !limb.isLegLimb)`) a seeded limb satisfies exactly (`infectionAmount=96.316`, index 3, nothing dismembered).

Both act on `PlayerCamera.selectedLimb`, and `limb-use` reads that same field back. Every attempt so far reports `limb=0` — the head — whatever was seeded and whatever point was clicked in the wound view. So what is unproven is the SELECTION, not the wound: `wound-limb-points` gives a point per limb, a click there is supposed to select that limb, and the answer still says 0.

That is the single thing left for 4b, and it is a fixture question with a direct read: click limb 5''s point from `wound-limb-points` and ask `limb-use` what `limb` it sees.
**And the read needs an item in a real slot.** The planned click-then-ask was tried on the alt: `wound-limb-points` answered limb 5 at (445,273), the click was staged there, and `limb-use type=bandage mode=read` came back empty because the bandage is in the carried list but in no body slot — so the selection question can only be answered once a tool is really in a slot. The two are the same fixture: a body with no container and one tool, which is also the state `/give` does not produce on a body that carries a bag (recorded above).
## Row 4b read: the amputation cut reaches the peer (2026-11-02, 22:40)

Three things had to be fixed before the row could be driven, and all three are recorded here because each one had silently blocked earlier attempts:

1. **The CUO window was covering the game.** `ui-hits` at the wound view''s limb point answered `Viewport / CUO Online UI Window / CUO Online Input Blocker / Limbs` — so every click "on the limb" went to the CUO window. With the window closed the same point answers `Limbs` alone.
2. **`/give` needs the member to be opped.** The alt''s `/give machete` had been silently doing nothing; after `/op <alt>` the tool landed in a body slot (`limb-use` answered `ok, usableOnLimb=true`).
3. **The limb selection cannot be driven by a staged pointer.** The wound view selects on hover, and a pointer that teleports between frames does not hover: thirteen point-clicks left `PlayerCamera.selectedLimb` at 0, 2 or 12, never the seeded limb. A new fixture `select-limb` writes the same field the view writes, and with it `limb-use` finally reports `limb=3` (UpArmF).

With those in place the native `AmputationMinigame` started (`minigame-live`: `kind=AmputationMinigame`) and the cut was driven with the pointer. Both recorders armed:

    patient (alt) : drip13, drip17, drip1, drip7, drip19, drip18, … (~34 drip clips over ~3 s)
    peer (host)   : drip21, drip22, drip13, drip11, drip20, drip7, …  (~34 drip clips over the same window)

The peer plays the same blood-drip family during the cut, at the patient''s position — which is the row''s claim: the acting client''s minigame step sounds reach every other client. `cutProgress` did not reach 1 in this window (the game advances it only while the hand is actually moving, and a staged pointer moves in bursts), so the final dismember step is the one thing this reading does not yet cover; the cutting itself does.
## 4b, the final step: the hand uses the `attack` bind, and holding it cancels the minigame (2026-11-02, 23:10)

`MinigameBase.UpdateHandVisuals` reads the hand''s click as `Input.GetKey(KeyBinds.GetBind("attack"))`, and `keybind-read` answers that binding as **Mouse0**. Two readings follow:

- `virtual-input mode=press button=0` does NOT reach it: `minigame-live` answered `handClicking=false` with a press held at the ring, because the hand read asks the KEY API while the press stages the BUTTON state;
- `virtual-key key=Mouse0 down=1` DOES: the same read then answers `handClicking=true`.

With the hand clicking, the saw ran: `cutProgress` accumulates `|handVelocity.x| * 5E-05` per physics frame (`AmputationMinigame.PhysicsUpdate`), the limb went to `skin=0, muscle=0`, and the peer already had the drip family (previous section). But the minigame then ENDED with `kind=none` and `dismembered=false` on every one of the fifteen limbs, and no `gore` clip on either side — because holding `attack`, the very key the hand needs, also fires the body''s own attack, which is what closes the minigame in this fixture.

So the last step needs a hand that clicks `attack` without the body attacking, which is a fixture question with one named candidate: stage `attack` only on the frames the hand reads it (a press/release cadence rather than a hold) and see whether the cut completes. Everything before that step is read and recorded.
### Row 4b's last step, one batch later (2026-11-02, 02:40)

The recipe chain was rebuilt on a fresh session and each link was read:

    /op <alt>                         -> the member's /give works
    /give machete                     -> limb-use reports slot 0, condition 1
    open-own-wound                    -> open=True
    select-limb limb=10               -> {limb:10, name:"CrusF"}          (the field really is written)
    limb-use type=machete mode=apply  -> {limb:0, limbName:"head", actuallyUsable:true}
    minigame-live                     -> kind=none

So `select-limb` writes the field and `ApplyWoundItem` still acts on the HEAD: the wound view recomputes its selection from the
pointer's hover every frame, and a staged pointer that teleports between frames never hovers a limb — the same wall the first
batch hit, now measured from the other side (the write is visible, the read is overwritten before use).

That is the fixture the row needs, named exactly: **a pointer that HOVERS** (many frames arriving near the same limb point, not
one jump), which the channel does not stage today. Row 4b stays carried with this reading; the two-batch limit for this row is
spent, so the next attempt belongs to a batch that builds the hovering pointer first.
### Why the selection cannot be written, in the game's own code (2026-11-02, 02:55)

`WoundView.Update` rewrites the selection every frame, and the only writer it obeys is a UI POINTER-ENTER:

    WoundView.cs:152-153   Limb limb = this.body.limbs[this.limbLookingAt];
                           PlayerCamera.main.selectedLimb = limb;
    WoundViewLimb.cs:11-16 public void OnPointerEnter(PointerEventData eventData) { … this.woundview.limbLookingAt = this.limb; … }
    (the only other writers: Limb.cs:97 and BasicCourse.cs:275, neither on a player's path)

So `select-limb` writing `PlayerCamera.main.selectedLimb` is guaranteed to be overwritten before any use reads it — confirmed
three ways on the running client (`select-limb limb=10` answers CrusF; `limb-use` then reports limb 0/head; the machete never
starts its minigame). Reaching the minigame therefore needs the EventSystem to deliver `OnPointerEnter` for the limb image,
which a staged pointer that is merely POSITIONED does not do: the pointer must move onto the element and stay there while the
EventSystem's own hover pass runs. That is the fixture to build first, and it is what the two-batch limit on this row is waiting
on — not the sound chain, which is already read for every step up to the dismember.
### The window was the wall, and the hover works once it is gone (2026-11-02, 03:05)

`ui-hits` at the limb point named the blocker in the first three hits: `Viewport > CUO Online UI Window > CUO Online Input
Blocker > …` — the window was still open on the alt, so the EventSystem delivered the pointer to our blocker and never to
`WoundViewLimb`. After `window.close` the same read answers `Torso (1) > Head (1) > ArmR > Head (1) > Thorax > Limbs`, and a
staged move DOES move the selection: `limb-use` reports `limb=1` (chest) where it had reported `limb=0` (head) for every attempt
before.

What it does not do yet is move the selection a SECOND time: six further points all answer `limb=2` (abdomen), i.e. the enter
event fires once and then further staged positions produce no new enter. The staged position is not what changes: it persists
(`VirtualInput._pointer`) by design, so the wall is elsewhere — the point list's coordinates and the hover target's own
collider are the candidates to read, not the channel's memory of the pointer. That is the last step before the machete can be
aimed at an arm or a leg and the dismember sound read.

So row 4b's blocker is now three facts, each measured: the window must be closed; one hover works; repeated hovers need a
pointer that really moves between frames.
### The hover works: the wound view's points are top-left, the pointer is bottom-left (2026-11-02, 06:40)

The wall named above is broken, and it was a coordinate space, not a freshness problem. `wound-limb-points` answers the limb
images' positions in SCREEN coordinates with a TOP-LEFT origin, while a staged pointer must be in `Input.mousePosition`'s
BOTTOM-LEFT space. Hovering the leg's point (608,469) selected the CHEST; hovering the mirrored point (608, 720-469 = 251)
selected limb 10, the right lower leg, and `limb-use` reports it:

    limb-use type=machete mode=apply   -> { limbo: 10, name: "right lower leg" }

So the whole selection chain works with a staged pointer once the y is flipped — no click, no window juggling beyond keeping
the CUO window closed (its blocker sits above the game's view and the EventSystem gives it the pointer).

What still does not happen is the minigame: applying the machete to that leg answered `actuallyUsable=true` and
`minigame-live` still says `kind=none`. That is the next thing to read in the game's own code (the machete's `useLimbAction`
gate on the limb's state), not a fixture question any more.
### The amputation minigame starts, and what ends it (2026-11-02, 07:05)

The gate was infection, in the game's own code:

    Item.DoAmputate (Item.cs:7127-7142)
        if (limb.infectionAmount > 60f && index > 2 && (!anyLegDismembered || !limb.isLegLimb))
            MinigameBase.main.StartMinigame(new AmputationMinigame(limb), item);

So a healthy leg can never start it, which is why every earlier attempt answered `kind=none` after a usable apply. With the leg's
infection raised to 80 by the new `limb-infect` fixture (a write to the game's own field), the same three calls now answer:

    limb-infect value=80                -> { name: "right lower leg", infection: 80 }
    limb-use type=machete mode=apply    -> { limb: 10, name: "right lower leg" }
    minigame-live                       -> { kind: "AmputationMinigame", handClicking: true }

and the cut runs: after ~200 pointer sweeps the limb reads `skin=0 muscle=0 dismembered=false` with the minigame still up.

What ends it is `cutProgress`, not the limb's health:

    AmputationMinigame.cs:105   cutProgress += Mathf.Abs(Minigame.game.handVelocity.x) * 5E-05f;   (per physics frame)
    AmputationMinigame.cs:73-75 if (cutProgress >= 1f) { this.limb.Dismember(); … }

The staged pointer moves the HAND to the position but the velocity the game reads stays small (the hand follows the pointer
smoothly), so the progress bar fills far slower than the skin does. The row's remaining fixture step is a pointer whose *frame to
frame* delta is large enough to fill `cutProgress` — a "shake" rather than a "move" — and that is the only thing between this
batch's reading and the dismember sound.
### The shake works and the cut PLATEAUS at 0.59 (2026-11-02, 07:45)

`PointerShake` (a component in the acceptance assembly that toggles the staged pointer every frame) turned the cut from "does not
move" into a measured climb. The probe gained the field the end condition reads:

    minigame-live  ->  kind=AmputationMinigame  cutProgress=0.4698 … 0.5879 … 0.5910

In the same minutes the limb went to `skin=0 muscle=0` and stayed `dismembered=false`. So the minigame is no longer slow — it is
STUCK just under 0.6, and the plateau survived a second shake with a tighter amplitude (575..645 instead of 520..700), which rules
out "the hand left the zone because the amplitude was too big".

What the code says can stop the accumulation mid-way: `cutProgress` only moves inside `if (this.cutting)`
(`AmputationMinigame.cs:103-106`), and `cutting` is the base class's flag for "the attack is held AND the hand is over the wound"
— the same flag whose volume line is `Mathf.Clamp01(Abs(handVelocity.x * 0.1f))` (line 71). With the limb at zero health the
game may well have stopped treating the limb as cuttable while the minigame stays up. That is the next reading: what `cutting`
is when the plateau starts, which needs the flag itself in the probe (`MinigameBase` exposes it) rather than another sweep.

Two instruments came out of this and both are committed: `PointerShake` (a per-frame pointer, because a scripted sweep costs a
round trip per position and the game's hand settles in between) and the `cutProgress` read in `minigame-live`.
### The amputation COMPLETES: the band has to be followed down, and time can be scaled (2026-11-02, 08:35)

Two things ended the plateau, both from the game's own code:

1. **The band moves.** `maxHandPos` is `Lerp(120, -380, cutProgress)` (AmputationMinigame.cs:34-40) and `cutting` is
   `handPos.y <= maxHandPos + 10` (46-50), with `PhysicsUpdate` raising the hand to `maxHandPos` when it falls below (99-101).
   So as the cut deepens the hand must be dragged DOWN with it; a shake that holds one height leaves the band above the hand and
   the progress stops exactly where this ticket's earlier batch stopped it (0.59).
2. **`PointerShake` can drift**, one unit per toggle, and the probe now reads `handPosY` beside `maxHandPos`. With the drift on,
   the reading is the hand tracking the band to within a hundredth:

        t+20   cut=0.0305  handY=104.763  max=104.755
        t+260  cut=0.4298  handY=-94.885  max=-94.894

   and at six times the game's own clock (`Time.timeScale = 6`, the speed-up the game itself uses) the cut finished inside forty
   seconds. The minutes were a FIXTURE artefact, not the game: `cutProgress` gains `|handVelocity.x| * 5E-05` per physics frame, and
   the measured climb (0.43 in 260 s at 1x, ~50 physics frames a second) means the staged hand was moving at about 0.7 units a
   frame - the band clamp held it still against the floor. A player sawing at a hand velocity of 40 fills the same bar in about ten
   SECONDS; the fixture's hand was ~60x weaker than a player's, and the clock multiplier compensated for the fixture, not for the
   game.: `minigame-live` answered `kind=none` and `limb-read` answered **`dismembered=true`** for the right lower leg.

So the limb DOES come off through the fixture, and every step up to it is read. What is still missing is the one-shot sound at
that instant: `audio-recent` samples what is playing RIGHT NOW, and the clip had finished before the next call (the host answered
`playing=0`). The row's remaining reading is a sampler running DURING the dismember — a loop over `audio-recent match=gore` while
the cut finishes, which the recipe's own note already prescribes ("a batch that needs the whole window samples in a loop").
### The speed-up is the game's OWN local clock, not a session setting (2026-11-02, 08:50)

Checked before trusting it: the game changes `Time.timeScale` itself, per client, and nothing replicates it —

    PlayerCamera.cs:663-696   Time.timeScale = 1 / 5 / 20 / 25 / 3.5 / 0.16 / 0      (its own speed control)
    ConsoleScript.cs:815      Time.timeScale = this.ParseFloat(args[1])               (its own console verb)

So a fixture that sets `Time.timeScale` on ONE client is doing exactly what the game's own speed control does on that client: it
accelerates that client's simulation, not the session. For the amputation that is the right scope — the minigame, its hand and its
`cutProgress` all live on the patient's client — and it leaves the peers on their normal clock, which is where the sound row has to
be sampled anyway. What it deliberately does NOT do is make the peers hear the clip sooner; the clip plays on their clock when the
dismember reaches them, exactly as it would in a real session.
### The fixture's contract, derived from the corpus instead of discovered (2026-11-02, 09:10)

The work above found the mechanic one experiment at a time. It should have been read first: `docs/design/mechanics/medical-view.md`
already states the amputation rule — `cutProgress += Abs(handVelocity.x) * 5E-05f` "so a faster saw cuts faster" — and the page
gained the two facts it was missing (the `DoAmputate` infection gate and the moving `maxHandPos` band), each with its game line.

The contract a batch now starts from, written down before any input:

| the player's gesture | what it feeds | what gates it |
|---|---|---|
| use an amputating tool on a selected limb in the wound view | `Item.DoAmputate` | `infectionAmount > 60f`, not head/torso, no lower leg already gone (`Item.cs:7127-7142`) |
| hold attack and saw, dragging the hand DOWN as the bar fills | `cutProgress += |handVelocity.x| * 5E-05f` per physics frame | `cutting` = `handPos.y <= maxHandPos + 10f`, `maxHandPos` = `Lerp(120, -380, cutProgress)` (`AmputationMinigame.cs:34-50`) |
| keep sawing to the end | `cutProgress >= 1f` | dismember + disinfect the limbs below; the minigame ends (`:73-82`) |

A fixture is built from that table: the hand must move FAST and FOLLOW the band down. Our shake moved at ~0.7 units a frame against
a player's ~40, which is why the run looked like "a ten-minute cut" — the number described the fixture, not the game.
### The sound half needs a second dismember, and the selection will not leave the torso (2026-11-02, 09:35)

After the first leg came off, the wound view's selection stopped moving: every leg image's point (608,200 / 672,251 / 672,200 -
the mirrored y of limbs 11, 13 and 14) selects the CHEST, however many intermediate positions the pointer passes through.
`WoundViewLimb.OnPointerEnter` ignores a dismembered limb (`if (PlayerCamera.main.body.limbs[this.limb].dismembered) return;`), so
the pointer over a gone limb falls through to the torso image behind it, and the hover that moved the selection the first time no
longer produces an enter on a leg.

**Corrected the same day, by the user: a fresh body is NOT needed.** `/heal` RESTORES a dismembered limb - measured after this note:
`limb-read` answered `dismembered=false, skin=100` for the limb that had just come off - so the same patient can be cut again. What
does not come back is the wound view's SELECTION: after a heal the view's `limbLookingAt` is back on the torso and the staged
pointer no longer moves it (every leg point answers the chest, on both clients, however the pointer is walked onto it). That is the
state the row stops at: its local mechanism is fully read - gate, band, speed, completion - and the peer's one-shot clip needs one
more dismember while a sampler runs.

The sampler exists now: `audio-watch` (the eval declaration) records every clip it sees playing over N frames, and the two recipes
`audio-watch-start` / `audio-watch-read` drive it, so a one-shot no longer has to be caught inside a polling interval. The first cut
it watched ended with no `gore` in the hits - which is either a real gap in the peer's sound or a clip whose name does not carry
"gore"; the next reading is the watcher's full hit list (unfiltered) around a dismember.
## Two readings from the frame recorder (2026-11-02, 09:55)

**The peer that watched the amputation heard no gore, and no drip either.** One cut (the patient at `Time.timeScale = 6`, the cut
finishing in 10.4 s), the HOST recording EVERY clip it played (`audio-watch`, no filter):

    seen=11   hits="pant|warningloop|shuttleAlarm|capsule|wind|Underwater|windLoop|brownian|rainloop|earthquakeLoop|firstcaveloop|"

None of the medical family. That is not yet a verdict on the row: those are all AMBIENT clips the listener hears wherever it stands,
and the host was standing far from the patient - a 3D clip at the patient's position is quiet from there. The next run puts the
listener at the patient's feet before the cut.

**The remote panel's blood volume fell in a straight line: zero reversals.** `blood-watch` sampled the display body's
`bloodVolume` once a frame for 600 frames (10 s of the cut):

    samples=600  min=98.358  max=100  reversals=0  maxDelta=0.4232

`reversals=0` means the value never went back up: it steps DOWN as the host's snapshots arrive, which is what a 1 Hz authoritative
snapshot with no interpolation looks like. So the "+/-0.01 bouncing" the user saw is not the underlying value oscillating - the
next reading has to be the NUMBER THE PANEL PRINTS, because a display that rounds a value sitting near a boundary flickers between
two printed numbers while the value itself moves monotonically.
### The hover's real cause: nothing is hittable on this client (2026-11-02, 10:15)

The "selection will not move" reading had a cause one layer below it: `ui-hits` at the limb point answers an EMPTY list on the alt,
with `open-own-wound` answering `open=true` — the EventSystem finds nothing under the pointer there, so `WoundViewLimb.OnPointerEnter`
never fires and `limbLookingAt` keeps its value. On the guest, with its view open, the same call lists the view's images
(`Torso / Head / ArmR / …`), which is why the hover moved that client's selection.

So the fixture must not assume two clients are alike: a hover row needs `ui-hits` to list the view's own images on THAT client
first, and a client where it answers nothing is recorded as such (a named per-client state), not retried with more hover patterns.
The two convenience restarts spent earlier were chasing this, and the rule that forbids them is now in step 13.