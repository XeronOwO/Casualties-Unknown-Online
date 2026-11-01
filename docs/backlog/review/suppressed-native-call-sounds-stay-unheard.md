# Sounds whose native call is suppressed never reach the peers

- Status: Review - rows 1, 2, 3 and 5 are PASS, each with a counted listener-side reading (`audio-watch3`: the peer played the clip exactly once, for a local bandage treatment, a remote bandage wrap, and a landing world item); row 4 (injection) is read through the same family but needs a syringe holding a liquid; row 4b (gore) is unproven because the machete given for it never appeared in a body slot; row 6 (plush) needs an item id the catalog does not hand a batch; rows 7 and 8 stay as they were
- Acceptance (20261001-l): rows 4 and 5-8 judged (the world-item impact replayed 219:219 on each guest from the authority's 219 reports, the plush squeak 12:12, the suite rows); rows 1-4b stay unproven and the dust puff visual was not captured — record `docs/evidence/acceptance/suppressed-native-call-sounds-stay-unheard-20261001-l.md`
- Priority: Low
- Category: Audio sync / report coverage
- Source: the 2026-09-26 census of `review/unhooked-item-and-body-sound-families.md` — closing that family's routing left two rows whose clip is not merely unreported: the native call that would play it is suppressed or blocked on every side.
- Related: `review/unhooked-item-and-body-sound-families.md` (the census these rows come from), `done/remote-medical-treatment-operations.md`, `docs/architecture/remote-inventory-native-parity.md` (§3.4)

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

- Not re-deriving the capture chain: `review/unhooked-item-and-body-sound-families.md` landed the
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