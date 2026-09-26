# The treatment family's gore presentation is not carried

- Status: Todo
- Priority: Low
- Category: Audio sync / report coverage
- Source: the independent adversarial review of the `review/suppressed-native-call-sounds-stay-unheard.md` cycle (2026-09-26) — its census read the delegates of the accepted medical items but not what those delegates CALL, so two groups were recorded as "natively silent" although they play a 3D gore clip.
- Related: `review/suppressed-native-call-sounds-stay-unheard.md` (the cycle that censused the accepted surface and recorded these rows as uncarried), `review/unhooked-item-and-body-sound-families.md`

## The gap

Two groups of the accepted remote-limb-treatment surface play a 3D clip that does NOT come from the
item's own limb action, so the treatment table (which mirrors those actions) carries nothing for them:

| Group | What plays | Where it is played |
|---|---|---|
| `medicalsuture` | `gore{1..5}` | the delegate's first statement calls `limb.body.DoGoreSound()` (Item.cs:378 → Body.cs:2443-2446: `Sound.Play(string.Format("gore{0}", Random.Range(1, 6)), base.transform.position, false, true, …)`) |
| the seven amputating tools (`machete`, `titaniummachete`, `crudecleaver`, `sickle`, `claws`, `titaniummultitool`, `flimsyknife`) | `gore` + `gore{N}` | the delegate starts the native `AmputationMinigame`; its completion calls `Limb.Dismember()` (AmputationMinigame.cs:69-85), which plays `Sound.Play("gore", …)` and then `body.DoGoreSound()` (Limb.cs:91-99) |
| (adjacent, same shape) `tweezers` | `tweezeruse` **and** `gore{N}` | the carried `tweezeruse` row is correct, but `ShrapnelMinigame.cs:71` also calls `limb.body.DoGoreSound()` — uncensused by either cycle |

## Why this is its own decision (not a table row)

- The clip is played by the LIMB and MINIGAME path, not by the item's limb action: a row in
  `RemoteMedicalTreatmentSoundCatalog` would invent a call the item never makes.
- The CUO remote amputation path starts that same native minigame on the OPERATOR's client, so the
  operator hears the gore while every peer hears nothing — but the dismemberment itself is applied on
  the PATIENT's client, which runs the game's own `Dismember` and therefore already plays the gore
  locally. Carrying the operator's copy as-is would make the patient hear it twice, so the carrier has
  to be decided with that census in hand (which client plays, which client replays, and how the two
  are deduplicated) — the same shape the treatment row's own decision had.
- The candidates for the carrier are a per-step capture scope on the minigame that plays it (the
  `BandageMinigameSoundPatches` shape) plus a policy row for `gore` / `gore{N}`, or a documented
  decision that a dismemberment's sound stays the acting and the affected client's own.

## What done looks like

1. The gore rows are either carried by a decided mechanism, or recorded as deliberately silent with
   the reason on this ticket (and then removed from the "uncarried, reason pending" group).
2. The double-play question (operator + patient) is answered with the census, not assumed.
3. The treatment table's `UncarriedItems` comments and the self-check's census are updated to cite this
   ticket's outcome instead of "carrying it is its own decision".

## Non-goals

- Not re-deriving the accepted surface or the item-action rows: the cycle that opened this ticket
  censused them item by item and the review re-derived that census independently.
