using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// One cut's world facts, in the file shapes <c>world-blocks.json</c> and
/// <c>world-transients.json</c> carry (§3.4): the block difference table, the
/// partial block damage (which rides the native half of the seam, not the
/// Runtime rows), and the transient world facts the cut captured.
///
/// Empty is a MEANING, not a missing value: a layer-end cut records no in-layer
/// fact at all, because the layer it names is regenerated from the run baseline.
/// <see cref="Failure"/> is the other meaning: a native table could not be read,
/// and the cut must refuse rather than store a world with a silently empty
/// table (<c>Failure</c> null = every table that had to be read was read).
/// The type lives next to the writer so the two files' row shapes stay named in
/// one place.
/// </summary>
internal sealed record WorldSaveFacts(
	IReadOnlyList<SaveWorldBlockRow> Blocks,
	IReadOnlyList<SaveWorldTransientRow> Transients,
	string? Failure = null)
{
	/// <summary>The layer-end shape: a cut that carries no in-layer fact (§4).</summary>
	internal static WorldSaveFacts None { get; } = new([], []);

	/// <summary>A native table could not be read: the cut has no usable fact set and must be refused.</summary>
	internal static WorldSaveFacts Unreadable(string failure) => new([], [], failure);
}
