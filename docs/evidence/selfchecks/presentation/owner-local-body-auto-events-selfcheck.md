# Owner-Local Body Auto-Events — Clone Suppression Self-Check

## Mechanism inventory

| # | Mechanism | Vanilla behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Vomiter | `Vomiter.Update` accumulates vomit timers from the Body's own sickness/internal bleeding and starts vomiting coroutines | render clone: component disabled after creation | `Body.cs:1074` mounts `Vomiter` on the Body; `Vomiter.cs:15-42` runs in its own `Update` |
| 2 | SelfHarmer | `SelfHarmer.Update` watches happiness/time-still and may start self-harm/suicide/mood minigames | render clone: component disabled after creation | `Body.cs:1077` mounts `SelfHarmer` on the Body; `SelfHarmer.cs:21-85` |
| 3 | PantSound | `PantSound.Update` sets a looping pant/pain/yawn source from the Body's stamina/pain/energy; one-shot pain/yawn/growl/bark are now reported as `CharacterSoundMsg` events | render clone: component disabled after creation; the one-shot vocalizations replay on the clone from the dedicated event, not from a clone-side `PantSound` simulation | `Body.cs:3434` uses `GetComponent<PantSound>()`; `PantSound.cs:42-82`; `PantSoundPatches.cs` |
| 4 | MoodChangeSounds | `MoodChangeSounds.Update` reads `PlayerCamera.main.body` (the LOCAL body) and plays 2D mood-change sounds | render clone: component disabled after creation; a clone copy would duplicate the local player's mood sounds | `MoodChangeSounds.cs:9-50` |
| 5 | SleepingBagUse | `SleepingBagUse.Update` reads `PlayerCamera.main.body.usingSleepingBag` and can destroy its own GameObject | render clone: component disabled after creation; a clone copy could destroy the clone from the local player's sleeping-bag state | `SleepingBagUse.cs:9-30` |

## Change

`RemoteBodyFactory.CreateRemoteBody` is the single place that creates render
clones from the `"Experiment"` template. It already disables physics,
colliders, IK handles, and carries the `RemoteBodyDriver` marker. This slice
adds explicit disabling of every owner-local body auto-event component:

- `Vomiter`
- `SelfHarmer`
- `PantSound`
- `MoodChangeSounds`
- `SleepingBagUse`

These components have their own `Update` methods, so the existing
`Body.Update`/`Limb.Update` render-proxy patches do NOT skip them. Leaving them
enabled can make a frozen clone simulate owner-local effects and, for
`MoodChangeSounds`/`SleepingBagUse`, read the local player's body (duplicate
mood sounds / clone destruction). The continuous pant loop stays owner-local;
the sparse one-shot PantSound vocalizations (pain/yawn/growl/bark) are not
simulated on the clone and instead replay through the existing dedicated
`CharacterSoundMsg` event path.

## Protocol

The clone suppression itself is adapter-local. The later PantSound one-shot
vocalization work added new `CharacterSoundKind` values to the existing
`CharacterSoundMsg` event and bumped `ProtocolVersion` to 2; see
`speech-sound-frequency-selfcheck.md` and `character-sound-selfcheck.md`.

## Verification

| Mechanism | Change | Evidence |
|---|---|---|
| Clone suppression | `RemoteBodyFactory` disables the five component types | `RemoteBodyFactory.cs` (static source evidence) |
| Continuous pant owner-local | remains local; no per-frame stream added | `PantSound.cs:8-82`; speech-sound-frequency-selfcheck |
| One-shot PantSound vocalizations | dedicated `CharacterSoundMsg` path, no clone-side simulation | `PantSoundPatches.cs`; CharacterSoundKind.cs; protocol version 2 |
| Build/gates | solution + repo gates | `dotnet build`, architecture gate pass |
| Full suite | regression not affected | `dotnet test` (test suite does not instantiate Unity clones; static evidence only) |

**L0/static evidence, no manual acceptance**
(development-period no-manual-acceptance rule).
