# The host's eating sound is not heard on the guest

- Status: Todo
- Priority: Medium
- Category: Character/item audio sync
- Source: User acceptance finding (2026-09-21): the host eats and the guest does not hear the eating sound at all.
- Related: `review/sync-player-pain-vocalizations-and-bark.md` (the one-shot character-sound relay this extends), `todo/guest-hears-only-some-block-break-sounds.md` (the same acceptance pass's other audio finding), `review/host-metal-scrap-block-place-sound-not-synced-to-guest.md`

## Evidence

- The native eating sound is played on the local body inside the consume path: `Item.cs` carries the
  whole family (`Sound.Play("eatCrunch", body.transform.position, ...)` for food,
  `Sound.Play("eatFlesh", ...)` for flesh), and `Body.cs` plays `Sound.Play("burp", ...)` at the end
  of a meal.
- `CharacterSoundPolicy.Origin` knows Attack, Throw, Exert, Footstep, LandingImpact, Pain, Bark,
  Growl, Yawn, LockpickPain and ItemPlacement only. The consume/eat path opens no scope, so nothing
  is reported, and a remote clone never runs the native call — unlike the pain/bark/placement
  family, which already rides the one-shot event.

## Goal and acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | Host eats | The guest hears the same eating sound at the same moments |
| 2 | Guest eats | The host hears it |
| 3 | A third peer | Hears it once, no double audio |
| 4 | The end of a meal | The closing sound joins the same reported family |
| 5 | Sibling one-shot consumable sounds (drinking/pouring, combine, hand switch) | Each rides the same path or is recorded as deliberately local with a reason |

## Non-goals

- Not the continuous pant loop and not per-frame physiological audio.
- No new wire family while the existing one-shot character-sound event can carry a clip identity and
  a position.
