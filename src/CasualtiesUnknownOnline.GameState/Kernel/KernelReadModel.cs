using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.GameState.Kernel;

/// <summary>
/// Read-only view handed to domain Decide/AssertInvariants. It deliberately has
/// no mutation surface: a command may inspect state but never change it.
/// </summary>
internal sealed class KernelReadModel(
	RunEpoch runEpoch,
	ulong globalRevision,
	IReadOnlyDictionary<ulong, ItemState> items)
{
	public RunEpoch RunEpoch { get; } = runEpoch;

	public ulong GlobalRevision { get; } = globalRevision;

	public IReadOnlyDictionary<ulong, ItemState> Items { get; } = items;

	public ItemState? FindItem(ulong instanceId) =>
		Items.TryGetValue(instanceId, out var item) ? item : null;
}
