# The game behind the adapter

[Documentation](../README.md) > [Internals](README.md) > The game behind the adapter

---

**After this page** you can name the parts of the game CUO has to know about, and tell which of them a
game update is likely to move. Read [The adapter and a game update](adapter-and-updates.md) first: this
page is about the game, not about CUO's boundary.

## What the game is

*Casualties Unknown* (Demo) is a Unity 2022.3 game whose logic lives in `Assembly-CSharp.dll`. Two
scenes matter: `PreGen` is the menu and the pre-run setup, and the world is `SampleScene`, loaded from
`PreRunScript.cs:268` with `SceneManager.LoadScene("SampleScene")`. Everything CUO hooks lives in the
second one — world generation, the player, the world UI.

The findings below come from `reversing/`, the decompiled working tree. That tree is gitignored and
never edited, which is exactly why it may be cited by line number: the numbers cannot drift, and any
change to the game arrives as a new decompile rather than an edit.

## The player is a scene object, not a spawned one

There is no "spawn the player" call to intercept. The player is a scene prefab — one `Body` plus one
`PlayerCamera` per scene instance — and the camera follows whichever body is bound:

- `PlayerCamera.cs:3142` declares `public static PlayerCamera main;` and `:3130` declares
  `public Body body;`. The camera follows `PlayerCamera.main.body`.
- Movement is input-driven physics. `PlayerCamera.cs:843` is `public void HandleInput()`: it reads the
  key binds and the mouse, and writes `body.moveDir` and `body.targetLookPos`. The physics step —
  `Body.cs:2297`, `private void FixedUpdate()` — turns that into forces and velocity. Nothing else reads
  `moveDir`.
- The binds themselves come from `KeyBinds.GetBind("up"|"down"|"left"|"right"|"jump"|"attack"|…)`, a
  cached table of configurable keycodes, and `HandleInput()` is the single place the game collects
  input — it steps aside on a frame where the console or a UI panel is open. Everything the game does
  with input hangs off that one call, which is why CUO's own surfaces do not have to compete for it.
- The body's own state is a set of plain fields: `Body.cs:203` `public bool alive`, `:213`
  `public bool conscious`, `:3745` `public Limb[] limbs` (index 0 is the head).

That shape decides a lot of CUO's design: because the local body is simulated by the game's own
physics, a client never has to predict its own movement, and because another player's body exists only
as a clone, CUO has to furnish it with state rather than instructions.

## World generation is not deterministic on its own

Vanilla generation calls `Random.Range` throughout and uses an internal PRNG for block generation, so
two machines generating "the same" layer do not agree by default. CUO therefore isolates the generation
stream: `src/CasualtiesUnknownOnline.GameAdapter/WorldGen/WorldGenRandomIsolation.cs` exists to make
"the world-generation random stream deterministic across peers", so the host and a guest "start from the
same captured `Random.state`".

This is why the run baseline in the kernel carries the generation inputs at all — the seed and the two
rarity multipliers — and why a save taken at a layer boundary has to be taken **after** the layer
advance commits: the checkpoint has to describe the layer that is about to be generated, not the one
that just ended.

The fields that define a run in progress — `WorldGeneration.totalTraveled`, `biomeDepth` and
`biomeOverride` — are read and written through `HarmonyTraverse`, because the game exposes no setter
for any of them; `biomeOverride` is an enum of `None`/`Tutorial`/`Debug` that decides which generation
branch runs. `WorldStartParams.LoadedRun` is the exception worth remembering: it has no backing field
on `WorldGeneration` at all, so on the wire it stays false — a guest's layer still matches because what
drives generation is the restored `Random.state`, not that flag.

## The native save is not a world save

The game's own `SaveSystem.SaveGame` writes a gzip JSON blob to `save.sv`
(`SaveSystem.cs:186`), and it carries character and run state only — body and limb fields, carried and
worn items, recipes, run settings, the run clock. It carries no world at all: no blocks, no world items,
no entities, no enemies, no fluids. A native continue therefore regenerates the current layer from its
beginning, and `SaveSystem.TryLoadGame` deletes the file once it has applied it (`SaveSystem.cs:453`).

Two consequences shape CUO:

- **CUO keeps its own archive** — see [How a world is saved](save-archive.md) — because the native
  format cannot express a mid-run world at all.
- **CUO blocks the native load.** `src/CasualtiesUnknownOnline.GameAdapter/Patches/SaveSystemTryLoadGamePatch.cs`
  exists for one reason: "Decision 165: CUO never reads the native `save.sv`… the game's own
  `SaveSystem.TryLoadGame`… must therefore never run." The prefix leaves `SaveSystem.loadedRun` alone so
  the game still believes it is continuing, and every fact comes from the CUO restore instead.

## A remote player is a clone of the scene's own character

CUO does not build a character from parts. `RemoteBodyFactory` finds the scene's `"Experiment"` player
object and instantiates it — "same template KrokMP uses" — and turns the copy into a frozen render
proxy: physics stops, but `Body.Update` keeps running so limbs and poses animate, and the pieces that
would fight the frozen state (the IK aim handles, the limb hinge joints) are disabled on the clone. The
clone has to be created after the world exists, because the body's own `Awake` reaches into world
generation for its sound mixer.

The proxy recipe is a per-component decision, verified in the decompiled sources under `reversing/`:

- **A proxy's `Body.Update` is skipped; its visual path is called directly.** `HandleVisuals`
  (`Body.cs:3123+`) drives the limb poses from the body and arm animators and casts for `grounded`;
  a proxy that skipped it would keep uninitialized limb sprites and animate nothing. The adapter
  calls only that visual branch, so the native vital-sign stages never run on a guest's clone.
- **`Limb.Update` (`Limb.cs:498+`) is skipped on every remote clone.** A guest's clone never runs
  it; the host runs it explicitly inside its fixed-step vitals pass (below), because the host's
  clone is the bleeding model. It writes the limb's shader parameters and heals/infects, it never
  moves a limb.
- **`IKHandle.Update` (`IKHandle.cs:40-57`) is disabled.** It lerps its target toward
  `Camera.main.ScreenToWorldPoint(Input.mousePosition)` and draws a line renderer at it, so a clone
  drew its aim line at the *local* player's mouse — the "head looking at the mouse" symptom.
- **The cloned limbs' `HingeJoint2D` is disabled** with the rest of the physics
  (`FixedUpdate` skipped, `Rigidbody2D.simulated` false), and the root transform is written every frame
  from the peer's report.

The **host** has one additional duty, ruled by the user on 2026-10-18: on its clone of each remote
player it runs the game's own vital stages (`HandleVariableUpdates`, `HandleBody`,
`HandleBodyTemperature`, `HandleRadiationSickness`, `HandlePeriodicChecks` and `Limb.Update`) in a
fixed-step pass to compute the three continuous bleeding values the host owns, then pushes them to
that player's client. The clone is the same render clone, seeded from the stored character snapshot;
the host's view and its authority are one body. Events, terminal facts and random outcomes stay
with the affected client — its report/event is the only writer of those — and the pass restores the
client-owned fields after every step. A guest never runs the pass; it renders what it is told and
judges its own events. Details: `docs/backlog/todo/health-simulation-and-host-authority.md`.

The clone cannot produce all of the model's inputs itself: it skips `HandlePhysics` (so `inWater`,
`Body.cs:3088`, is never computed), its `moveDir` is zeroed for presentation, and `exercising` /
`currentClimbable` are the owner's local actions. Their verdicts therefore ride the owner's 20 Hz
state (in-water, moving, exercising — the walk verdict uses the game's own `|moveDir.x| < 0.1`
stamina threshold, `Body.cs:2994`), and the pass writes all four onto the clone for its duration,
resolving the climb state against the host's own `Climbable` list (the same lookup the native
climbing start uses) and restoring the presentation values afterwards. A climbable the host world
does not have is named once and the body is modelled as stationary.

What that means for CUO: a body's ACTIONS and its EVENTS belong to its owner's client; the host
owns the shared world and, for the continuous bleeding values, computes them from the owner's
inputs. The host never re-derives an injury, a terminal fact or a random outcome.

## KrokMP is a reference, not a model

The decompiled KrokMP mod in `reversing/` is read for facts about the game, not as a design to follow:
it broadcasts body state to clones whose physics still run, and it replaces generation coroutines
outright. CUO's answers to the same problems — one owner per body's actions and events, a
deterministic kernel, physics-frozen render proxies, and a host-side VITALS-ONLY model over the
clone's inputs — are the opposite where it matters, and that contrast is why this page exists.

## Related reading

- [The adapter and a game update](adapter-and-updates.md) — the boundary that absorbs this churn
- [The shape of CUO](architecture-overview.md) — why a clone is a projection and not a second simulation
- [How a world is saved](save-archive.md) — the archive that replaces the native save
- [Repository map and pitfalls](../contributing/repository-map-and-pitfalls.md) — where `reversing/` sits in the tree
- [Glossary](../reference/glossary.md) — native, adapter, projection, remote clone, world generation

---

[Documentation](../README.md) > [Internals](README.md) > The game behind the adapter
