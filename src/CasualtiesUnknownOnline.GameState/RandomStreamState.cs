using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// A named deterministic random stream or a pre-decided result set that must
/// survive checkpoint/save. This phase stores the opaque state string plus any
/// explicitly decided values; later domains may add typed stream carriers.
/// </summary>
public sealed record RandomStreamState(
	string Name,
	string State,
	IReadOnlyList<ulong> DecidedValues);
