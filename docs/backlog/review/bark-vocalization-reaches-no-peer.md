# The bark vocalization reaches no peer

- Status: Review — found 2026-11-01 on the deployed build: the acting client plays its bark clip and no other client ever does. The ticket's own family (`sync-player-pain-vocalizations-and-bark`) is marked code-complete, so this is either a hole in its capture or in its relay; the three candidate points are named below.
- Priority: High — the feature the family exists for is inaudible to every other player.
- Related: `sync-player-pain-vocalizations-and-bark` (the family this belongs to), `unhooked-item-and-body-sound-families` (the listener-side instrument used here).

## How it was read

Three clients in one session, the peer running the frame-accurate counting watcher (`audio-watch3`, which counts one playback per distinct `AudioSource`).

1. `body-call kind=bark` — a new kind that calls the game's own `PantSound.Bark()` — answered `applied=true` on the host.
2. Watching the HOST with no filter across that call, its clip list gained **`pain1`** — the game's bark pick (`PantSound.Bark` plays `Sound.Play(this.barkSounds.PickRandom<AudioClip>(), …)`), so the bark really happened on the acting client.
3. Watching the PEER with `match=bark` and then reading its whole list across the same window: **`pain1` never appears**. Its list carries every other family this session proved (`dogshake`, `vomit2`, `stretch`, `pain1` absent).
4. A CONTROL window on the host with no bark call at all (six seconds) gained only ambient clips (`drip12`, `drip7`, `speech`) — so `pain1` belongs to the bark call and not to the body's ongoing pant/pain loop.

## Candidate causes (to be read, not guessed)

- **The capture's clip name is empty.** `CharacterSoundPolicy.Classify` returns null for an empty clip, and the bark plays an `AudioClip` picked from an array — if the `Sound.Play(AudioClip, …)` patch reads a name the runtime clip does not carry, the call is classified as nothing and never reported.
- **The scope never opened.** `PantSoundBarkPatch` enters `CallContext.Origin.CharacterBark` only when `IsLocalVocalizer` holds (`GetComponent<Body>()` present and no `RemoteBodyDriver` in the parents). The host's own body should satisfy both; the reading above cannot see whether the scope was entered.
- **The relay drops the kind.** `CharacterSoundKind.Bark` is a newer member of the family; a receive-side filter or a replay that resolves the clip by name could drop it while the older kinds (`dogshake`, `vomit2`, `stretch`) go through.

## Next reading

The host-side send path at the moment of the bark, then the peer's receive path for the same window — one log line each at a level that is on (the current ones are `LogDebug`). That names the failing layer before any change, which is this project's rule for a cause: read it, do not add a log and re-run the whole batch.

## Design

Read before touching this: [`docs/design/model/authority-and-judgement.md`](../../design/model/authority-and-judgement.md) (who judges, and what a one-shot presentation is) and
[`docs/design/mechanics/README.md`](../../design/mechanics/README.md) (the mechanic taxonomy this family sits in). The rule the defect breaks: a one-shot presentation the acting client
runs is reported through the character-sound family and replayed by every other member — a member left out of it is not a cosmetic gap.