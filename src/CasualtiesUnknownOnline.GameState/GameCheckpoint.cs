using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Complete authoritative state snapshot at a revision. Phase A snapshots only
/// the item table; later phases add every domain table.
/// </summary>
public sealed record GameCheckpoint(
	RunEpoch RunEpoch,
	ulong GlobalRevision,
	IReadOnlyList<ItemState> Items);
