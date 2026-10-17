using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The game's own answer behind the console <c>/give</c> verb: resolve a definition
/// id to its prefab, capture the fresh save-shaped fact the item domain carries, and
/// allocate the instance id it will be known by.
///
/// <para>
/// Two facts only this layer can read decide whether the definition exists at all:
/// the game's item table (<c>Item.GlobalItems</c>, keyed by the id the operator
/// types) and the prefab the item domain instantiates on every peer
/// (<see cref="ItemPrefabResolver"/>). A definition in the table whose prefab cannot
/// be loaded is refused HERE rather than discovered by the receiving client's failed
/// creation — a round trip too late, and by then the item has already left the
/// giver's ledger.
/// </para>
///
/// <para>
/// The capture is the prefab's OWN state, the same shape <c>TraderRecruitCoordinator</c>
/// grants stock gifts from, and no temporary scene instance is created — so no
/// item-domain report can fire for an object that is not a real item. The id comes
/// from the one allocator the rest of the adapter uses
/// (<see cref="ItemIdAllocator.AllocateId"/>): ids are (counter, account id), and a
/// counter anywhere else would mint an identity the host's watermark, the peers'
/// snapshots and the save archive never saw.
/// </para>
/// </summary>
internal sealed class ItemGrantSource(ItemIdAllocator ids, ILogger<ItemGrantSource> log) : IItemGrantSource
{
	private readonly ItemIdAllocator _ids = ids;
	private readonly ILogger _log = log;

	public bool TryCreate(string definitionId, out CharacterItemMsg? item, out string? refusal)
	{
		item = null;
		refusal = null;

		if (string.IsNullOrWhiteSpace(definitionId))
		{
			refusal = "No item id was given — name the game's own item id, e.g. bandage.";
			return false;
		}

		if (Item.GlobalItems is not { } known || !known.ContainsKey(definitionId))
		{
			refusal = $"No item definition '{definitionId}' is known to this game — /give names a definition from the game's own item table.";
			return false;
		}

		var prefab = ItemPrefabResolver.Load(definitionId);
		if (prefab == null) // Unity object — ==
		{
			refusal = $"Item definition '{definitionId}' has no prefab this build can load, so it cannot be created.";
			return false;
		}

		var template = prefab.GetComponent<Item>();
		if (template == null) // Unity object — ==
		{
			refusal = $"Item definition '{definitionId}' has no Item component on its prefab, so it cannot be created.";
			return false;
		}

		item = ItemStateCodec.CaptureItem(template, slotIndex: -1);
		item.InstanceId = _ids.AllocateId();
		_log.LogInformation("[Give] prepared {Definition} (id {InstanceId}) from its prefab.", definitionId, item.InstanceId);
		return true;
	}
}
