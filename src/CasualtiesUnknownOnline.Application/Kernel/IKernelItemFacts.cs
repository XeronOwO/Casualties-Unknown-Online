using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The item read model the admission seam needs: an item's judged location
/// decides whether a member may report its destruction at all. The host's kernel
/// authority implements this; the gateway only reads through it and never writes.
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
