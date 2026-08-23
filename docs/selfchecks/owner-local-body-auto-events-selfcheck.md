# Owner-Local Body Auto-Events — Clone Suppression Self-Check

## Mechanism inventory

| # | Mechanism | Vanilla behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Vomiter | `Vomiter.Update` accumulates vomit timers from the Body's own sickness/internal bleeding and starts vomiting coroutines | render clone: component disabled after creation | `Body.cs:1074` mounts `Vomiter` on the Body; `Vomiter.cs:15-42` runs in its own `Update` |
| 2 | SelfHarmer | `SelfHarmer.Update` watches happiness/time-still and may start self-harm/suicide/mood minigames | render clone: component disabled after creation | `Body.cs:1077` mounts `SelfHarmer` on the Body; `SelfHarmer.cs:21-85` |
| 3 | PantSound | `PantSound.Update` sets a looping pant/pain/yawn source from the Body's stamina/pain/energy | render clone: component disabled after creation | `Body.cs:3434` uses `GetComponent<PantSound>()`; `PantSound.cs:42-82` |
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
mood sounds / clone destruction). The effects stay owner-local by design; if a
remote presentation is ever wanted, it belongs in a dedicated future event
channel.

## Protocol

No wire change, no `ProtocolVersion` bump: this is adapter-local clone
construction.

## Verification

| Mechanism | Change | Evidence |
|---|---|---|
| Clone suppression | `RemoteBodyFactory` disables the five component types | `RemoteBodyFactory.cs` (static source evidence) |
| Owner-local by design | no dedicated sync path added | no protocol/wire change; backlog + selfcheck updated |
| Build/gates | solution + repo gates | `dotnet build`, architecture gate pass |
| Full suite | regression not affected | `dotnet test` (test suite does not instantiate Unity clones; static evidence only) |

**L0/static evidence, no manual acceptance**
(development-period no-manual-acceptance rule).
