using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

namespace CasualtiesUnknownOnline.GameState.Kernel;

/// <summary>
/// The authoritative kernel state owned by the kernel. The store is internal:
/// only the kernel can replace it atomically.
/// </summary>
internal sealed class GameStateStore(RunEpoch runEpoch)
{
	private readonly Dictionary<ulong, ItemState> _items = [];
	private RunState? _run;
	private WorldEntityState? _worldEntities;
	private PlayerStateTable? _players;
	private EnemyStateTable? _enemies;
	private FluidStateTable? _fluids;

	public RunEpoch RunEpoch { get; private set; } = runEpoch;

	public ulong GlobalRevision { get; private set; }

	public IReadOnlyDictionary<ulong, ItemState> Items => _items;

	public RunState? Run => _run;

	public WorldEntityState? WorldEntities => _worldEntities;

	public PlayerStateTable? Players => _players;

	public EnemyStateTable? Enemies => _enemies;

	public FluidStateTable? Fluids => _fluids;

	public CommittedOperationWindow Operations { get; } = new(2048);

	public MutableKernelState CreateWorkingCopy() => new(RunEpoch, GlobalRevision, _items.Values, _run, _worldEntities, _players, _enemies, _fluids);

	public void ReplaceWith(MutableKernelState working)
	{
		_items.Clear();
		foreach (var item in working.Items.Values)
		{
			_items[item.Identity.InstanceId] = item;
		}

		_run = working.Run;
		_worldEntities = working.WorldEntities;
		_players = working.Players;
		_enemies = working.Enemies;
		_fluids = working.Fluids;
		GlobalRevision = working.GlobalRevision;
		RunEpoch = working.RunEpoch;
	}

	public GameCheckpoint CreateCheckpoint() =>
		new(RunEpoch, GlobalRevision, [.. _items.Values], null, _run, _worldEntities, _players, _enemies, _fluids);

	public void Restore(GameCheckpoint checkpoint)
	{
		RunEpoch = checkpoint.RunEpoch;
		GlobalRevision = checkpoint.GlobalRevision;
		_run = checkpoint.Run;
		_worldEntities = checkpoint.WorldEntities;
		_players = checkpoint.Players;
		_enemies = checkpoint.Enemies;
		_fluids = checkpoint.Fluids;
		_items.Clear();
		foreach (var item in checkpoint.Items)
		{
			_items[item.Identity.InstanceId] = item;
		}

		Operations.Clear();
	}
}
