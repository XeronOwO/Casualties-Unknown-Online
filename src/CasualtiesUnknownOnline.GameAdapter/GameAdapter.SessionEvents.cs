using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Session-led carry/render events (partial of <see cref="GameAdapter"/>): the
/// carried-fact, item-dropped, id-watermark and starting-supplies events are
/// thin one-line forwards into the character-data domain. Kept in a partial so
/// the coordinator file stays under the 600-line architecture gate.
/// </summary>
public sealed partial class GameAdapter
{
	/// <summary>Carried-fact event: the owner's fact-table entry updates and the clone re-renders immediately.</summary>
	private void OnItemCarriedSync(ulong owner, CharacterItemMsg item, bool slotKnown) =>
		_characterDataSync.ApplyCarriedSync(owner, item, slotKnown);

	/// <summary>The host granted the item-id counter (join/reconnect): resume from watermark + 1 — the crashed-and-rejoined counter must not reuse ids the host still holds.</summary>
	private void OnItemIdWatermark(ulong counter) => _itemIds.SetWatermark(counter);

	/// <summary>A guest's starting supplies with self-assigned ids arrived — seed its fact table so the clone renders them and the snapshot divergence check knows them.</summary>
	private void OnCarriedInventory(ulong owner, IReadOnlyList<CharacterItemMsg> items) =>
		_characterDataSync.ApplyCarriedInventory(owner, items);

	/// <summary>ItemDropped: a carried item left into the world — it leaves the owner's fact table (top-level or nested in a container's contents).</summary>
	private void OnCarriedItemDropped(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos) =>
		_characterDataSync.RemoveCarriedItem(itemId);
}
