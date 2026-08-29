using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

namespace CasualtiesUnknownOnline.GameState.Kernel;

/// <summary>
/// Read-only view handed to domain Decide/AssertInvariants. It deliberately has
/// no mutation surface: a command may inspect state but never change it.
/// </summary>
internal sealed class KernelReadModel(
	RunEpoch runEpoch,
	ulong globalRevision,
	IReadOnlyDictionary<ulong, ItemState> items,
	RunState? run,
	WorldEntityState? worldEntities,
	PlayerStateTable? players,
	EnemyStateTable? enemies)
{
	public RunEpoch RunEpoch { get; } = runEpoch;

	public ulong GlobalRevision { get; } = globalRevision;

	public IReadOnlyDictionary<ulong, ItemState> Items { get; } = items;

	public RunState? Run { get; } = run;

	public WorldEntityState? WorldEntities { get; } = worldEntities;

	public PlayerStateTable? Players { get; } = players;

	public EnemyStateTable? Enemies { get; } = enemies;

	public ItemState? FindItem(ulong instanceId) =>
		Items.TryGetValue(instanceId, out var item) ? item : null;
}
