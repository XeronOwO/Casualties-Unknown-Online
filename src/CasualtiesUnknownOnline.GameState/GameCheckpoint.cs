using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Complete authoritative state snapshot at a revision. Phase A snapshots only
/// the item table; Phase D adds World/Run, WorldEntities, and Players baselines
/// and later phases add every remaining domain table. Random streams are
/// optional and empty until a domain actually owns one.
/// </summary>
public sealed record GameCheckpoint(
	RunEpoch RunEpoch,
	ulong GlobalRevision,
	IReadOnlyList<ItemState> Items,
	IReadOnlyList<RandomStreamState>? RandomStreams = null,
	RunState? Run = null,
	WorldEntityState? WorldEntities = null,
	PlayerStateTable? Players = null);
