using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The game grants a fresh character's starting supplies inside
/// <c>WorldGeneration.WorldPlacePlayer</c> at depth 0 (<c>WorldGeneration.cs:2563-2602</c>), on the body its own
/// instance holds. A member that keeps its body across a world rebuild never runs that path, because the local body
/// is created once in <c>PlayerCamera.Awake</c> (<c>PlayerCamera.cs:795-803</c>). The member's bag then stays empty in
/// every world after the first. This grants the same table, at the same slots, to a member whose bag is empty in a
/// world it has not been granted in: the ids and slots are the game's own table, not a CUO rule (2026-11-28
/// <c>full-49</c>: four starting supplies dropped at the member's feet 70 ms after the checkpoint).
/// </summary>
internal sealed class LocalStartingSupplies(ISessionControl session, ILogger log)
{
	private int _worldId;
	private bool _granted;

	internal void Tick(Body? localBody)
	{
		if (localBody == null) // Unity object — ==
		{
			return;
		}

		if (session.Role != SessionRole.Guest)
		{
			return;
		}

		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		var worldId = world.GetInstanceID();
		if (worldId != _worldId)
		{
			_worldId = worldId;
			_granted = false;
		}

		if (_granted || world.biomeDepth != 0 || HasSupplies(localBody))
		{
			return;
		}

		_granted = true;
		var setting = WorldGeneration.GetRunSettingInt("startingsupplies");
		if (setting == 0)
		{
			return;
		}

		if (setting == 1)
		{
			Grant(localBody, "emergencylight", 3);
			return;
		}

		Grant(localBody, "lantern", 3);
		Grant(localBody, "dogfood", 4);
		Grant(localBody, "waterbottle", 5);
		Grant(localBody, "trashbag", 1);
	}

	private static bool HasSupplies(Body body) =>
		body.HoldingItem(1) || body.HoldingItem(3) || body.HoldingItem(4) || body.HoldingItem(5);

	private void Grant(Body body, string id, int slot)
	{
		if (body.HoldingItem(slot))
		{
			return;
		}

		var created = Utils.Create(id, body.transform.position, 0f);
		var item = created != null ? created.GetComponent<Item>() : null; // Unity object — ==
		if (item == null) // Unity object — ==
		{
			log.LogWarning("[StartingSupplies] the game did not create {Item} for the member's empty bag.", id);
			return;
		}

		body.PickUpItem(item, slot, true);
		log.LogInformation(
			"[StartingSupplies] granted the game's starting {Item} at slot {Slot} to the member's empty bag in world {World}.",
			id,
			slot,
			_worldId);
	}
}
