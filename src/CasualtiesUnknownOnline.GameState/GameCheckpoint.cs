using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Complete authoritative state snapshot at a revision. Phase A snapshots only
/// the item table; later phases add every domain table. Random streams are
/// optional and empty until a domain actually owns one.
/// </summary>
public sealed record GameCheckpoint(
	RunEpoch RunEpoch,
	ulong GlobalRevision,
	IReadOnlyList<ItemState> Items,
	IReadOnlyList<RandomStreamState>? RandomStreams = null);
