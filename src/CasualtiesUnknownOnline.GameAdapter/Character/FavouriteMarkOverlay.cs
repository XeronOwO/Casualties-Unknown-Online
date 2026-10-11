namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The ONE writer of a display proxy's <c>favourited</c> field, and the only
/// thing that field means on a proxy: whether the LOCAL player marked this item
/// (user ruling 2026-10-11 — a favourite mark is the local player's own act, so
/// the owner's own favourite state is the owner's business and is never what this
/// view shows). Two callers, one rule: the clone inventory renderer writes it
/// wherever a proxy is given its authoritative identity, and the while-dragging
/// favourite gesture writes it once more for the item it just toggled, so the
/// star flips in the frame the player pressed the key instead of at the next
/// snapshot.
/// </summary>
internal static class FavouriteMarkOverlay
{
	/// <summary>
	/// Write the local mark onto a display proxy and answer what is now in force.
	/// A mark whose instance id now names a DIFFERENT item is DROPPED rather than
	/// applied, and its reason comes back through
	/// <paramref name="staleDrop"/> for the caller to report through its own seam
	/// — the write is this class's business, the log line is the caller's.
	/// </summary>
	internal static bool Apply(Item item, ulong instanceId, ulong ownerSteamId, out string? staleDrop)
	{
		var marked = LocalFavouriteMarkStore.Current.IsMarked(instanceId, ownerSteamId, item.id, out staleDrop);
		item.favourited = marked;
		return marked;
	}
}
