using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The narrow item read model: one item's judged fact, by instance id. Two
/// consumers read it and neither writes — the admission seam, where an item's
/// judged location decides whether a member may report its destruction at all,
/// and the mod-facing game-state projection, which answers a mod's by-id query
/// with the same row. The host's kernel authority implements it.
/// </summary>
public interface IKernelItemFacts
{
	/// <summary>
	/// The item's judged state, or null when this host never judged a creation
	/// for the id — the caller must NOT read null as "not owned": the kernel owns
	/// the answer for an id it never saw.
	/// </summary>
	ItemState? FindItem(ulong instanceId);
}
