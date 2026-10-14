using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The narrow item read model: an item's judged fact by instance id, and the
/// judged rows themselves. Every consumer reads and none writes — the admission
/// seam, where an item's judged location decides whether a member may report its
/// destruction at all; the protocol command handler, whose creation-before-
/// operation and unjudged-id verdicts ask the same rows by id; the mod-facing
/// game-state projection, which answers a mod's by-id query with the same row;
/// and the console's item-data verb, which lists the rows a body holds. The
/// host's kernel authority implements it.
/// </summary>
public interface IKernelItemFacts
{
	/// <summary>
	/// The item's judged state, or null when this host never judged a creation
	/// for the id — the caller must NOT read null as "not owned": the kernel owns
	/// the answer for an id it never saw.
	/// </summary>
	ItemState? FindItem(ulong instanceId);

	/// <summary>
	/// Every judged row, keyed by instance id — the same rows
	/// <see cref="FindItem"/> answers one at a time, for a consumer that has to
	/// enumerate instead of address one id (a listing filters on the location a
	/// row carries). A terminal row is included: the kernel records what an item
	/// became, and the caller reads the location to tell the live rows apart.
	/// </summary>
	IReadOnlyDictionary<ulong, ItemState> QueryItems();
}
