# An earthquake's direct Time.timeScale write ends a session acceleration

- Status: Todo
- Priority: Medium
- Category: World / session (world-time)
- Source: The family audit of the movement-routing fix (decision 223, 2026-09-25): every `PlayerCamera.SetTimeScale` call is now classified, but the game also writes `Time.timeScale` DIRECTLY, and the host pump adopts such a write as the new shared speed by design (`WorldTimeSync.AdoptDirectTimeScaleWrite`).
- Related: `review/world-acceleration-survives-movement.md` (the routing fix this is the remaining sibling of), `docs/backlog/todo/world-time-local-initiation.md`, `resolved/sleep-behavior-policy.md`, `docs/decisions/active.md` #223

## Evidence (what ends an acceleration through the direct-write path)

- Native: an earthquake start resets the clock in `WorldGeneration.Update` —
  `this.earthquakeDelay = Random.Range(600f, 1750f) * WorldGeneration.GetRunSettingFloat("timebetweenearthquakes"); this.earthquakeTime = Random.Range(3f, 25f); Time.timeScale = 1f;`
  (reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:866-871). The other direct write
  (`:1036`, `ReloadScene`) runs at a scene reload, where the start gate owns the clock, and
  `PreRunScript.cs:64` is the run start for the same reason.
- CUO: the host pump reads the live clock back and adopts any change as the standing request —
  `WorldTimeSync.AdoptDirectTimeScaleWrite`: "The host's actual Time.timeScale moved to another
  domain speed without a SetTimeScale call (e.g. the quake start resets 1×, WorldGeneration.cs:870,
  or a console write) — adopt it as the request so the broadcast keeps guests on the same clock."
  So an earthquake during a standing acceleration drops the whole session to 1×, which is the
  user-visible outcome the movement rule produced (user report 2026-09-21).
- The console's write (`ConsoleScript.cs:815`) is a deliberate admin action and must keep its
  meaning. The two cannot be told apart by the VALUE — only by the writer.

## Why it is a separate cycle

The routing fix attributes a call from the native flags Harmony hands to the patch; a field write has
no call to take and is read back a frame later, so attributing it needs a mechanism of its own (a
scope or an observation around the writer, or a rule inside the adoption step). Two things must be
decided with evidence first: what a suppression does to the host's own `_appliedSpeed` bookkeeping
(an ignored write leaves the domain believing a speed the clock is not at), and the gameplay answer,
which is the user's — may an earthquake interrupt an acceleration the player started?

## Acceptance (freeze with the user before implementing)

- An earthquake during a standing acceleration: the acceleration stands (or the ruling says it does
  not) on every screen, and the HUD speed indicator and its sound follow the actual clock.
- The console's direct write and the scene-reload write keep their current meaning.
