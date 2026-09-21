using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The local body's carried heal-profile items, read by the Online UI's explicit
/// item selector. Read-only: the host remains the authority for the cross-player
/// heal operation and re-validates the id the UI picked.
/// </summary>
public interface ILocalHealItemQuery
{
	/// <summary>True when the local body currently carries at least one item from the cross-player heal profile set (Online UI only — the host re-checks authority).</summary>
	bool HasLocalHealItem();

	/// <summary>
	/// The local carried heal-profile items with wire instance ids, for the
	/// Online UI's explicit item selector. Empty when no body / no usable
	/// slot item / no instance ids are available. The host remains the
	/// authority and re-validates the requested id.
	/// </summary>
	IReadOnlyList<LocalHealItem> GetLocalHealItems();
}
