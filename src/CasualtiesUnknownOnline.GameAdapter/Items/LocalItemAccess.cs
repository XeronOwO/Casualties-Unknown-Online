using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The local scene behind the item-data write verb: the ONE live object an instance
/// id names on this client, its captured save-shaped state, and one write into it.
///
/// <para>
/// The lookup is the id's own O(1) index (<see cref="ItemInstanceId"/>), which is
/// what makes this a LOCAL answer: a remote body's clone has its id components
/// destroyed, so another player's item is not found here at all, and the display
/// proxies the index could still be handed are refused outright — this side's copy
/// of somebody else's item is not a fact, and saying otherwise would report THEIR
/// item as this client's own. A standing incarnation of another member's carried
/// item (the cross-player use's local form) is refused for the same reason.
/// </para>
///
/// <para>
/// The write itself is the save restore's own three steps, in the same order and for
/// the same reason: the condition is written DIRECTLY (the game's <c>SetCondition</c>
/// drains the container's water by ratio, which a property write must not do — the
/// same reason <c>RemoteItemSceneOps</c> writes the field), then the liquid stacks are
/// rebuilt whole (<c>RestoreLiquids</c>) and the captured component fields are written
/// back by reflection (<c>RestoreComponentStates</c>). The item's contents and its
/// location are deliberately untouched: a state write is about the item's own
/// properties, and where it sits is not one of them.
/// </para>
/// </summary>
internal sealed class LocalItemAccess(ILogger<LocalItemAccess> log) : ILocalItemAccess
{
	private readonly ILogger<LocalItemAccess> _log = log;

	/// <summary>The local item's captured state, or null when this client holds no object under that id.</summary>
	public CharacterItemMsg? ReadLocalItem(ulong instanceId)
	{
		var item = Resolve(instanceId);
		return item is null // Unity object — ==
			? null
			: ItemStateCodec.CaptureItem(item, ItemStateCodec.SlotOf(item));
	}

	/// <summary>Applies one save-shaped top-level state to the local object, or reports why it cannot.</summary>
	public LocalItemWriteResult WriteLocalItem(ulong instanceId, CharacterItemMsg desired)
	{
		var item = Resolve(instanceId);
		if (item is null) // Unity object — ==
		{
			_log.LogWarning("[ItemWrite] #{InstanceId} is not held locally — nothing written.", instanceId);
			return LocalItemWriteResult.NotHeld;
		}

		if (StandingItems.Is(item))
		{
			// Another member's carried item, incarnated here for the cross-player use:
			// its state belongs to that member's own report, not to this client.
			_log.LogWarning("[ItemWrite] #{InstanceId} ({Definition}) is a standing incarnation of another member's item — not written.", instanceId, item.id);
			return LocalItemWriteResult.NotWritable;
		}

		item.condition = desired.Condition; // direct write, never SetCondition: it drains water by ratio
		item.favourited = desired.Favourited;
		ItemStateCodec.RestoreLiquids(item, desired.Liquids);
		ItemStateCodec.RestoreComponentStates(item, desired.Components);
		_log.LogInformation("[ItemWrite] #{InstanceId} ({Definition}) written: condition {Condition}, favourited {Favourited}, {Liquids} liquid stack(s), {Components} component state(s).",
			instanceId, item.id, desired.Condition, desired.Favourited, desired.Liquids.Count, desired.Components.Count);
		return LocalItemWriteResult.Applied;
	}

	/// <summary>
	/// The one local object an id names, or null: the id's own live-scene index, with
	/// the display proxies refused (their id components are destroyed when the clone
	/// is built, so this is the belt to that braces).
	/// </summary>
	private static Item? Resolve(ulong instanceId)
	{
		if (instanceId == 0 || !ItemInstanceId.TryFindItem(instanceId, out var item))
		{
			return null;
		}

		return ItemWorldSync.IsDisplayProxy(item) ? null : item;
	}
}
