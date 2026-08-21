using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Cross-player interaction forwarding (partial of <see cref="ItemService"/>):
/// the direct player-interaction domain moves ownership between guests through
/// these two host-only transfer-table seams. Kept in a partial so the main
/// ItemService file stays under the 600-line architecture gate.
/// </summary>
public sealed partial class ItemService
{
	public void AdoptTransferredItem(ulong guest, ulong itemId, CharacterItemMsg item) =>
		_arbitration.AdoptTransferredItem(guest, itemId, item);

	public void RemoveTransferredItem(ulong guest, ulong itemId) =>
		_arbitration.RemoveTransferredItem(guest, itemId);
}
