using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The game's own answer behind the console <c>/give</c> verb: resolve a definition
/// id to its prefab, capture the fresh save-shaped fact the item domain carries, overlay
/// the operator's state onto it, and allocate the instance id it will be known by.
///
/// <para>
/// The capture is the prefab's OWN state, the same shape <c>TraderRecruitCoordinator</c>
/// grants stock gifts from, and no temporary scene instance is created — so no
/// item-domain report can fire for an object that is not a real item. The overlay happens
/// BEFORE the id is allocated: a state the definition cannot carry refuses the whole give
/// and mints nothing.
/// </para>
///
/// <para>
/// The id comes from the one allocator the rest of the adapter uses
/// (<see cref="ItemIdAllocator.AllocateId"/>): ids are (counter, account id), and a
/// counter anywhere else would mint an identity the host's watermark, the peers'
/// snapshots and the save archive never saw.
/// </para>
/// </summary>
internal sealed class ItemGrantSource(ItemIdAllocator ids, ILogger<ItemGrantSource> log) : IItemGrantSource
{
	private readonly ItemIdAllocator _ids = ids;
	private readonly ILogger _log = log;

	public bool TryCreate(string definitionId, SnbtValue? state, out CharacterItemMsg? item, out string? refusal)
	{
		item = null;
		if (!ItemDefinitionTable.TryResolve(definitionId, out var template, out refusal) || template is null)
		{
			return false;
		}

		var capture = ItemStateCodec.CaptureItem(template, slotIndex: -1);
		var document = state is null
			? ItemStateDocument.Describe(capture)
			: SnbtTree.Overlay(ItemStateDocument.Describe(capture), state);

		if (!ItemStateDocument.TryBind(template, capture, document, out var bound, out refusal))
		{
			return false;
		}

		bound.InstanceId = _ids.AllocateId();
		item = bound;
		_log.LogInformation("[Give] prepared {Definition} (id {InstanceId}) from its prefab{State}.",
			definitionId, bound.InstanceId, state is null ? "" : " with the operator's state");
		return true;
	}
}
