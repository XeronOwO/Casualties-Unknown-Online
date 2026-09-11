using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// One cut's world facts, in the file shapes <c>world-blocks.json</c> and
/// <c>world-transients.json</c> carry (§3.4): the block difference table, the
/// partial block damage, and the transient world facts the cut captured.
///
/// Empty is a MEANING, not a missing value: a layer-end cut records no in-layer
/// fact at all, because the layer it names is regenerated from the run baseline.
/// The type lives next to the service so <c>WorldSaveService</c> stays within
/// the type-size gate while the two files' row shapes stay named in one place.
/// </summary>
internal sealed record WorldSaveFacts(
	IReadOnlyList<SaveWorldBlockRow> Blocks,
	IReadOnlyList<SaveWorldTransientRow> Transients)
{
	/// <summary>The layer-end shape: a cut that carries no in-layer fact (§4).</summary>
	internal static WorldSaveFacts None { get; } = new([], []);
}
