using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.Character;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// The live game's starting-supplies implementation (S4.3): the two Unity acts the grant
/// needs — create the content id's item at the local body, and put it in a body slot. It is
/// deliberately the ONLY place in the feature that names a Unity type, and it holds no state
/// and makes no decision, which is what keeps the whole branch structure of
/// <see cref="StartingSupplyCoordinator"/> verifiable in the test host.
///
/// Both acts reproduce the game's own first-layer grant exactly
/// (<c>WorldGeneration.cs:1896-1912</c>): <c>Utils.Create(id, body.transform.position, 0f)</c>
/// and <c>Body.PickUpItem(item, slot, true)</c>. Reusing the native calls rather than a
/// private re-implementation is the point — a player supplied here and a player supplied by
/// the game itself end up with the same objects in the same slots, and the side effects the
/// native path has (the pickup cloud, the backpack sound, the container unload) happen for
/// both.
/// </summary>
internal sealed class GameStartingSupplyTarget : IStartingSupplyBehaviour
{
	/// <summary>
	/// The body this target hands out, or null. Unity objects use <c>==</c>: a
	/// scene-reload-destroyed body is not the null reference but must not be handed out.
	///
	/// The handle IS the <c>Body</c>, so the same body always yields the same handle — which
	/// is what the grant's once-per-body rule keys on. A wrapper object would have to be
	/// cached to give that guarantee, and caching a Unity object is exactly the lifetime bug
	/// this type does not need.
	/// </summary>
	public object? LocalBody
	{
		get
		{
			var body = PlayerCamera.main?.body; // Unity object — ==
			return body == null ? null : body;
		}
	}

	/// <summary>
	/// The native grant's own creation shape, at the live body's position (not a stale
	/// reference: a player who moved must not be handed their supplies at the landing spot).
	/// The id comes from the plan, which is <see cref="StartingSupplyPolicy"/>'s copy of the
	/// game's setting table — an id the game has no resource for yields no <c>Item</c>, which
	/// the caller accounts for instead of pretending the item exists.
	/// </summary>
	public object? Create(string itemId)
	{
		var body = PlayerCamera.main?.body; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			// No body, no grant: the coordinator only ever asks while it has one, and
			// creating an item in a scene that is going away would leak it into the next.
			return null;
		}

		var created = Utils.Create(itemId, body.transform.position, 0f);
		if (created == null) // Unity object — ==
		{
			return null;
		}

		var item = created.GetComponent<Item>();
		if (item == null) // Unity object — ==
		{
			// The id resolved to something that is not an item at all: destroy the orphan
			// rather than leaving a GameObject with no domain representation in the world.
			Object.Destroy(created);
			return null;
		}

		return item;
	}

	/// <summary>
	/// The game's own placement — <c>Body.PickUpItem(item, slot, force: true)</c>, the native
	/// grant's call. The slot's own precondition is checked first because <c>PickUpItem</c>
	/// refuses an unusable slot SILENTLY (<c>Body.cs:1390</c>): without the check the item
	/// would stay at the body's feet and the account would claim it landed.
	///
	/// No <c>FirstEmptySlot</c> fallback. The plan's slots are the game's own numbering for
	/// these very items, so a body that cannot take one has a reason (a limb out of
	/// commission, something already carried there), and placing it somewhere else would
	/// silently differ from what the game's own grant does with the same setting. The item is
	/// left on the ground at the body instead, and the account names it.
	/// </summary>
	public bool TryPlace(object body, object item, int slot)
	{
		if (body is not Body carrier || carrier == null) // Unity object — ==
		{
			return false;
		}

		if (item is not Item placed || placed == null) // Unity object — ==
		{
			return false;
		}

		if (slot < 0 || slot >= carrier.slots.Length)
		{
			return false;
		}

		var destination = carrier.slots[slot];
		if (destination == null || !destination.canPickUp) // Unity object — ==
		{
			return false;
		}

		if (carrier.HoldingItem(slot))
		{
			return false; // something is already carried there — PickUpItem would refuse it
		}

		// force: true — the native grant's own argument. The sight/distance check
		// (DoPickupCheck) is meaningless here: the item was created at the body, and a player
		// who is lying down or blind would otherwise be denied their supplies.
		carrier.PickUpItem(placed, slot, force: true);
		return carrier.HoldingItem(placed);
	}
}
