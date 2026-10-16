# A dead player can watch the run by spectating a teammate

- Status: Todo
- Priority: Medium
- Category: Player interaction / presentation / KrokMP parity
- Source: the user's 2026-10-13 backlog request, in their own words: "操控的角色死亡后，能观战他人，参考
  KrokMP 的功能" — after the character you control dies, you should be able to spectate the others, with
  KrokMP's feature as the reference.
- Related: `reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs` (the native follow target, the native
  free camera and the death screen), `reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesMP/UIInGame.cs`
  (KrokMP's spectator mode), `src/CasualtiesUnknownOnline.Runtime/Session/HostRules/IHostRules.cs` (the host
  rules surface, where KrokMP's three spectator switches belong), `src/CasualtiesUnknownOnline.GameAdapter/World/RespawnCoordinator.cs`
  and `src/CasualtiesUnknownOnline.Runtime/Session/World/RespawnPolicy.cs` (the revive/respawn paths that
  must put the camera back), `docs/backlog/review/middle-click-location-marker.md` (its recorded
  "Dead/spectating" clause), `docs/backlog/review/enter-key-opens-plain-chat.md` (its input-context list
  already names a death/spectator panel)

## What the game already gives us

The demo ships both cameras this feature needs, and a death screen to step aside from:

- **A follow camera**: `PlayerCamera.following` is a `Transform` field (`PlayerCamera.cs:3124`) and the
  camera reads `following.position` whenever it is set (`PlayerCamera.cs:1947-1949`), so "watch this body"
  is one assignment, not a camera of our own.
- **A free camera**: `PlayerCamera.isFreecam` (`PlayerCamera.cs:3127`) drives the camera from
  `ConsoleScript.instance.freecamPos` (`PlayerCamera.cs:1963-1965`).
- **The death screen**: `endScreen` is activated and filled with the run's stats
  (`PlayerCamera.cs:2298-2328`, `:2365`), and `didDeathScreen` is latched once the body is no longer alive
  and the fade has finished (`PlayerCamera.cs:2392-2394`).

**Today CUO does nothing with any of it**: there is no spectator code in `src/`, so a dead player sits at
that screen until the group revives them at a trader, the next layer respawns them, or they leave
(decision 165's Continue path owns the last one).

## What KrokMP does (reference, not a model)

KrokMP replaces the in-game UI class with its own and keeps a spectator state in it
(`reversing/KrokMP/KrokoshaCasualtiesMP/KrokoshaCasualtiesMP/UIInGame.cs`):

- `StartSpectatorMode()` (`UIInGame.cs:355-379`) hides `PlayerCamera.main.endScreen`, then points the
  camera at a body — the local one when it still has one, otherwise it reports `NOBODY` — and remembers
  which member is being followed by name and index.
- `StopSpectatorMode()` (`UIInGame.cs:382-401`) clears `PlayerCamera.main.following` and re-shows
  `endScreen` only when `didDeathScreen` is latched, so a revived player gets their own view back.
- Target cycling walks the member list and re-points the camera (`UIInGame.cs:194-206`), and a `spectate`
  console command toggles the mode (`Con_.cs:1268-1284`). A server can also order one client into it
  (`ClientMain.cs:657-662`, `ClientReceiver__forcespectator`).
- Three of its game rules govern it (`KrokoshaMultiplayerGameRules.cs`): `AllowSpectatorFreecam`
  (`:194`, default true), `SpectateWhileUnconscious` (`:199`, default false) and `LateJoinSpectate`
  (`:211`, default false). `Con_.CanFreecam()` (`Con_.cs:348-370`) allows the free camera for anyone the
  mode applies to — dead by default, unconscious only when that rule is on.

Read it as behaviour, never as a design to copy (the same rule `docs/en/internals/game-internals.md` §"KrokMP
is a reference, not a model" states): KrokMP broadcasts body state and replaces the game's own coroutines,
while CUO keeps one owner per body and a frozen render proxy.

## What is asked

After the local character dies, its player should be able to keep watching the run — **follow a living
teammate, and free-fly if the host allows it** — until they are revived, respawn on the next layer, or
leave. The death screen stays the entry point and the place the player returns to; spectating is something
they do from it, not a screen that replaces the run's rules.

This is a **presentation-only** feature and that is what makes it cheap: the camera belongs to the client
that is watching, the body being watched is already replicated as that client's own display clone, and a
dead player's state is already synchronized. **Nothing here needs a wire change, a new message or a new
judgment** — which also means no protocol number moves (decision 241).

Three switches do belong on the host, because they decide what the group tolerates rather than what one
client renders. Their KrokMP-aligned defaults are the starting proposal, and the cycle confirms them where
it starts:

| Rule | Default | Why |
|---|---|---|
| Free-fly while spectating | On | A free camera can scout the layer ahead, so it is a group decision; KrokMP ships it on. |
| Spectate while unconscious | Off | An unconscious player is still playing in CUO's model (their own body is theirs to act on), so this changes a live player's client, not a dead one's. |
| Spectate while waiting to join | Off | That is the late-join case, and it is a separate promise about what a not-yet-in-world client may see. |

## Required work

1. Put the three switches on the host rules surface beside the existing respawn options
   (`IHostRules.cs`), each with the default named above, so a session decides once and every client reads
   the same answer.
2. Reuse the native mechanisms and nothing else: the follow target is `PlayerCamera.following`, the free
   camera is `PlayerCamera.isFreecam`, and the death screen is hidden and restored through `endScreen`
   with `didDeathScreen` respected — no CUO camera, no CUO death screen, no new UI window (the redo would
   have to pass the "why is the native surface not enough" test in `AGENTS.md` first).
3. Pick the target from what CUO already shows: the Online UI's member list is the natural chooser
   (`OnlineUiMemberListDrawer`), with the console as the keyboard path. A followed teammate who dies,
   leaves the layer or disconnects must move the camera on — the one case KrokMP leaves open and this
   feature cannot.
4. Keep every existing death-path intact: the revive at a trader, the next-layer respawn and the
   menu-return cut (decision 167's seam) each end the spectator view and clear `following`, and the
   spectator's own dead body keeps being simulated on its own client exactly as today (decision 216's
   rule is about the body, not the camera).
5. Verify on two clients in one session: kill one character, follow the survivor, cycle targets, free-fly
   with the rule on and off, then take each of the three exits above and confirm the local view returns —
   including the case where the followed teammate dies first.

## Non-goals

- Not a respawn or revive redesign: the per-layer respawn and the trader revive stay as they are, and this
  ticket only decides what the dead player sees while waiting for one of them.
- Not spectating the enemy or the world through the kernel: the followed subject is a member's body, and a
  free camera moves a client's own view rather than reading state it was not sent.
- Not a change to what a dead player may *do*: a spectator places no ping, performs no action and drives no
  possession, which is the clause `review/middle-click-location-marker.md` recorded for the dead case and
  this ticket keeps.
