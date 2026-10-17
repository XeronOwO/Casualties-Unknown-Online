using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The local scene behind the item-data family: the ONE live object an instance id
/// names on this client, its captured save-shaped state, one write into it, and the
/// item the pointer is over.
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
	/// <summary>The layer the game's own pickup query reads; the pointer target asks the same question.</summary>
	private const string CursorItemLayer = "Item";

	private readonly ILogger<LocalItemAccess> _log = log;

	/// <summary>The local item's captured state, or null when this client holds no object under that id.</summary>
	public CharacterItemMsg? ReadLocalItem(ulong instanceId)
	{
		var item = Resolve(instanceId);
		return item is null // Unity object — ==
			? null
			: ItemStateCodec.CaptureItem(item, ItemStateCodec.SlotOf(item));
	}

	/// <summary>Applies one state document to the local object, or reports why it cannot. The document is bound first, so a refusal writes nothing.</summary>
	public LocalItemWriteOutcome WriteLocalItem(ulong instanceId, SnbtValue desired)
	{
		var item = Resolve(instanceId);
		if (item is null) // Unity object — ==
		{
			_log.LogWarning("[ItemWrite] #{InstanceId} is not held locally — nothing written.", instanceId);
			return new LocalItemWriteOutcome(LocalItemWriteResult.NotHeld, null, null);
		}

		if (StandingItems.Is(item))
		{
			// Another member's carried item, incarnated here for the cross-player use:
			// its state belongs to that member's own report, not to this client.
			_log.LogWarning("[ItemWrite] #{InstanceId} ({Definition}) is a standing incarnation of another member's item — not written.", instanceId, item.id);
			return new LocalItemWriteOutcome(LocalItemWriteResult.NotWritable, null, null);
		}

		var capture = ItemStateCodec.CaptureItem(item, ItemStateCodec.SlotOf(item));
		if (!ItemStateDocument.TryBind(item, capture, desired, out var bound, out var refusal))
		{
			_log.LogWarning("[ItemWrite] #{InstanceId} ({Definition}) refused: {Reason}", instanceId, item.id, refusal);
			return new LocalItemWriteOutcome(LocalItemWriteResult.Refused, refusal, null);
		}

		item.condition = bound.Condition; // direct write, never SetCondition: it drains water by ratio
		item.favourited = bound.Favourited;
		ItemStateCodec.RestoreLiquids(item, bound.Liquids);
		ItemStateCodec.RestoreComponentStates(item, bound.Components);

		// The answer carries what the object NOW says, not what was asked: a hook or a
		// game rule that swallowed part of the write cannot be reported as applied.
		var written = ItemStateCodec.CaptureItem(item, ItemStateCodec.SlotOf(item));
		_log.LogInformation("[ItemWrite] #{InstanceId} ({Definition}) written: condition {Condition}, favourited {Favourited}, {Liquids} liquid stack(s), {Components} component state(s).",
			instanceId, item.id, written.Condition, written.Favourited, written.Liquids.Count, written.Components.Count);
		return new LocalItemWriteOutcome(
			LocalItemWriteResult.Applied,
			null,
			written,
			written.Condition == 0f
				? "At 0 the item's own rule may destroy it (destroyAtZeroCondition), so it can be gone a moment from now."
				: null);
	}

	/// <summary>
	/// The item the pointer is over, resolved by the game's own query: the single
	/// <c>Item</c>-layer collider under
	/// <c>Camera.main.ScreenToWorldPoint(Input.mousePosition)</c> — the call
	/// <c>PlayerCamera.TryPickupFromWorld</c> makes for its world half, so the pointer
	/// selects what the game's own pickup query selects rather than a selection CUO
	/// invented.
	///
	/// <para>
	/// Two local forms of an item are deliberately NOT selectable this way, and that is
	/// what keeps this answer honest without a classification for them: another
	/// member's item rendered here is a display proxy (physics off, its collider
	/// disabled — <c>CloneInventoryRenderer</c>) and the cross-player use's standing
	/// incarnation is inert by construction (<c>StandingItemRecipe.MakeInert</c>
	/// disables every collider), so the pointer query cannot return either. Both DO
	/// carry an id in their own holder's shape (<c>RemoteInventoryItemId</c>, or the
	/// authoritative <see cref="ItemInstanceId"/>), which is why they are reached by
	/// <c>id</c>/slot and never by pointing.
	/// </para>
	///
	/// <para>
	/// The one id-less answer left is real: a world item this client has not yet been
	/// given an id for — the host assigns one to every generation-time item when the
	/// generation finishes, its item snapshot carries them to a guest, and a runtime
	/// item gets one as it enters the item domain. Until then the pointer names the
	/// object but nothing the record can address.
	/// </para>
	/// </summary>
	public LocalCursorItemKind ResolveCursorItem(out ulong instanceId)
	{
		instanceId = 0;
		var camera = Camera.main;
		if (camera == null) // Unity object — ==
		{
			return LocalCursorItemKind.None;
		}

		var collider = Physics2D.OverlapPoint(camera.ScreenToWorldPoint(Input.mousePosition), LayerMask.GetMask(CursorItemLayer));
		if (collider == null || !collider.TryGetComponent<Item>(out var item) || item == null) // Unity objects — ==
		{
			return LocalCursorItemKind.None;
		}

		var id = item.GetComponent<ItemInstanceId>();
		if (id == null || id.Id == 0) // Unity object — ==
		{
			_log.LogInformation("[ItemCursor] the pointer is over an item with no id yet ({Definition}) — this client has not been given one.", item.id);
			return LocalCursorItemKind.NoInstanceId;
		}

		instanceId = id.Id;
		_log.LogInformation("[ItemCursor] the pointer names #{InstanceId} ({Definition}).", instanceId, item.id);
		return LocalCursorItemKind.Resolved;
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
