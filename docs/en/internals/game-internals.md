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

The proxy recipe is verified in the decompiled sources and listed in
`docs/features/game-internals.md`; what it means for CUO is the same rule as everywhere else — one side
owns a body, the other side renders what it is told.

## KrokMP is a reference, not a model

The decompiled KrokMP mod in `reversing/` is read for facts about the game, not as a design to follow:
it broadcasts body state to clones whose physics still run, and it replaces generation coroutines
outright. CUO's answers to the same problems — one owner per body, a deterministic kernel, a render
proxy with frozen physics — are the opposite where it matters, and the contrast is recorded in
`docs/features/game-internals.md`.

## Related reading

- [The adapter and a game update](adapter-and-updates.md) — the boundary that absorbs this churn
- [The shape of CUO](architecture-overview.md) — why a clone is a projection and not a second simulation
- [How a world is saved](save-archive.md) — the archive that replaces the native save
- [Repository map and pitfalls](../contributing/repository-map-and-pitfalls.md) — where `reversing/` sits in the tree
- [Glossary](../reference/glossary.md) — native, adapter, projection, remote clone, world generation

---

[Documentation](../README.md) > [Internals](README.md) > The game behind the adapter
