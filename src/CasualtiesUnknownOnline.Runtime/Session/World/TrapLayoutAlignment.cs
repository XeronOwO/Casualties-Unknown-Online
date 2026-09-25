using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>The guest's layout alignment result: what to materialize (the host
/// has it, the local world does not — or no same-kind match within the radius)
/// and what to destroy (the LOCAL entries the host's layout does not claim —
/// indices into the local list, so the adapter resolves the live components).</summary>
internal readonly struct TrapLayoutAlignment
{
	internal IReadOnlyList<TrapLayoutEntryMsg> ToSpawn { get; init; }

	internal IReadOnlyList<int> ToDestroy { get; init; }
}
