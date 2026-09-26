# Sounds whose native call is suppressed never reach the peers

- Status: Review
- Priority: Low
- Category: Audio sync / report coverage
- Source: the 2026-09-26 census of `review/unhooked-item-and-body-sound-families.md` — closing that family's routing left two rows whose clip is not merely unreported: the native call that would play it is suppressed or blocked on every side.
- Related: `review/unhooked-item-and-body-sound-families.md` (the census these rows come from), `review/remote-medical-treatment-operations.md`, `docs/architecture/remote-inventory-native-parity.md` (§3.4)

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
| `tweezers` | `tweezeruse` (:1700) | remote-treatment table |
| `wrench` | `wrenchhit` (:4864) | remote-treatment table |
| `disinfectant` · `spraybottle` | `spray` (:2100 / :2124) | remote-treatment table |
| `paincream` (reliefcream) · `woundglue` (woundglue) | `cream` (Liquids.cs:1074 / :1100, reached through `WaterContainerItem.ApplyToLimb`) | remote-treatment table, keyed by the LIQUID the item holds |
| `bloodbag` · `bloodbaghuman` · `antiserum` · `streptokinase` · `bloodcoagulant` · `combatpen` | `syringe` (:1760 / :1926 / :1375 / :1734 / :1539 / :1565) | remote-treatment table |
| `saline` · `ringersolution` · `ceftriaxone` · `morphine` · `opium` · `heroin` · `fentanyl` · `naloxone` · `syringe` | none — the delegate starts `SyringeMinigame`, whose own cues are 2D screen feedback | deliberately silent, recorded |
| `aed` · `manualdefibrillator` | none — `AEDMinigame` / `ManualDefibMinigame` cues are 2D | deliberately silent, recorded |
| `tourniquet` · `icepack` · `makeshiftwrench` · `adhesivebandage` | none — the delegate plays no clip at all | deliberately silent, recorded |
| amputating tools (`machete` … `flimsyknife`) · `medicalsuture` · `tweezers`' SECOND clip | `gore` + `gore{N}` (amputation completion: `Limb.Dismember` → Limb.cs:91-99; `medicalsuture`'s delegate calls `Body.DoGoreSound`, Item.cs:378 → Body.cs:2443-2446; `ShrapnelMinigame.cs:71` for tweezers) | UNCARRIED, ticketed — `todo/treatment-gore-presentation-not-carried.md`. The clip comes from the LIMB/minigame path rather than the item's limb action, and the dismemberment is applied on the PATIENT's client, which already plays the game's own `Dismember` locally: the carrier has to be decided with that double-play census (the independent review of this cycle found these rows had been recorded as natively silent, which they are not) |

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
| 4b | An amputation, a medical suture, or the shrapnel removal | UNCARRIED this cycle: the gore clip the limb/minigame path plays stays where it is played — `todo/treatment-gore-presentation-not-carried.md` carries the row and its double-play question | that ticket's census (this cycle's acceptance does not claim it) |
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
  carrier decision is ticketed (`todo/treatment-gore-presentation-not-carried.md`) because the
  dismemberment is also applied on the patient's own client — the double-play census comes first.
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
