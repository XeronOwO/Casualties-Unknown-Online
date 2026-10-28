using CasualtiesUnknownOnline.Runtime.Data;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The one scene capability the console <c>/summon</c> verb needs from the Game Adapter: where the native
/// command's position words point, and one prefab materialized in the world.
///
/// <para>
/// The verb itself is control plane: it parses the operator's words, judges the cheat rulings and writes the
/// sentence the operator reads. What it cannot do is name a prefab the game can materialize or create one — the
/// prefab resolution, the component classification and the creation call live in the game assembly, and only the
/// adapter may touch them.
/// </para>
///
/// <para>
/// <see cref="TrySummon"/> is deliberately ONE answer for three families, because the CLASSIFICATION is the
/// game's own fact: an id the game's item table knows is a loose item (created from the prefab capture, with the
/// operator's state bound and an instance id from the one allocator), anything else is a world-entity prefab
/// (<c>Utils.Create</c>, and the object must carry a <c>BuildingEntity</c> with a prefab id a peer could
/// recreate). The caller chooses the family with its TYPE WORD and the seam enforces that word against its own
/// tables — it never guesses the family from them, and a word that contradicts the id is refused by name. The
/// entity reports through its own <c>BuildingEntity.Start</c>; the item is reported explicitly by the shared
/// world-item creation (the pre-stamped id makes its own <c>Start</c> skip the report), so no second creation
/// path exists for either.
/// </para>
///
/// <para>
/// A build without the Game Adapter — a test host, a headless one — registers
/// <see cref="UnavailableWorldSummon"/> instead, so the verb answers "this build cannot create world objects"
/// rather than creating nothing and saying nothing.
/// </para>
/// </summary>
public interface IWorldSummon
{
	/// <summary>
	/// The world point a position keyword names, on THIS client's scene — the pointer it can see, the body it
	/// controls, or the layer it is in. False with <paramref name="refusal"/> naming the ground when the scene
	/// cannot answer (no world, no live body, no camera).
	/// </summary>
	bool TryResolveAnchor(WorldSummonAnchor anchor, out float x, out float y, out GrantRefusal? refusal);

	/// <summary>
	/// Summon <paramref name="prefabId"/> at the point and answer what was created. <paramref name="kind"/> is
	/// the operator's own type word (<see cref="WorldSummonKind"/>): an ITEM names a loose item entity — the id
	/// must be a definition in the game's own item table, the state is bound against its capture BEFORE anything
	/// is created, and the result is the same fact and the same world object the give ladder's drop rung makes;
	/// an ENTITY names a world-entity or animal prefab, refuses an item definition by name, and takes no state.
	/// The created object's own report is what the session sees, so this answer carries identity, not a second
	/// notification.
	/// </summary>
	WorldSummonOutcome TrySummon(string prefabId, WorldSummonKind kind, SnbtValue? state, float x, float y);
}
