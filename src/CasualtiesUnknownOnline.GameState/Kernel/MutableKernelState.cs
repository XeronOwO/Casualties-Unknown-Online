using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

namespace CasualtiesUnknownOnline.GameState.Kernel;

/// <summary>
/// Mutable transaction working copy. Reducers write here; the kernel swaps a
/// complete copy into place only after invariants pass.
/// </summary>
internal sealed class MutableKernelState(
	RunEpoch runEpoch,
	ulong globalRevision,
	IEnumerable<ItemState> items,
	RunState? run,
	WorldEntityState? worldEntities,
	PlayerStateTable? players,
	EnemyStateTable? enemies)
{
	private readonly Dictionary<ulong, ItemState> _items = items.ToDictionary(item => item.Identity.InstanceId);

	public RunEpoch RunEpoch { get; set; } = runEpoch;

	public ulong GlobalRevision { get; set; } = globalRevision;

	public RunState? Run { get; set; } = run;

	public WorldEntityState? WorldEntities { get; set; } = worldEntities;

	public PlayerStateTable? Players { get; set; } = players;

	public EnemyStateTable? Enemies { get; set; } = enemies;

	public IReadOnlyDictionary<ulong, ItemState> Items => _items;

	public bool TryGetItem(ulong instanceId, out ItemState item) => _items.TryGetValue(instanceId, out item);

	public void UpsertItem(ItemState item) => _items[item.Identity.InstanceId] = item;

	public void SetRun(RunState run) => Run = run;

	public bool RemoveItem(ulong instanceId) => _items.Remove(instanceId);
}
