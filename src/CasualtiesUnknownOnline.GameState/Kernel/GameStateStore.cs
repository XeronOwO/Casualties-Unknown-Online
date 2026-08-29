using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.World;

namespace CasualtiesUnknownOnline.GameState.Kernel;

/// <summary>
/// The authoritative kernel state owned by the kernel. The store is internal:
/// only the kernel can replace it atomically.
/// </summary>
internal sealed class GameStateStore(RunEpoch runEpoch)
{
	private readonly Dictionary<ulong, ItemState> _items = [];
	private RunState? _run;

	public RunEpoch RunEpoch { get; private set; } = runEpoch;

	public ulong GlobalRevision { get; private set; }

	public IReadOnlyDictionary<ulong, ItemState> Items => _items;

	public RunState? Run => _run;

	public CommittedOperationWindow Operations { get; } = new(2048);

	public MutableKernelState CreateWorkingCopy() => new(RunEpoch, GlobalRevision, _items.Values, _run);

	public void ReplaceWith(MutableKernelState working)
	{
		_items.Clear();
		foreach (var item in working.Items.Values)
		{
			_items[item.Identity.InstanceId] = item;
		}

		_run = working.Run;
		GlobalRevision = working.GlobalRevision;
		RunEpoch = working.RunEpoch;
	}

	public GameCheckpoint CreateCheckpoint() =>
		new(RunEpoch, GlobalRevision, [.. _items.Values], null, _run);

	public void Restore(GameCheckpoint checkpoint)
	{
		RunEpoch = checkpoint.RunEpoch;
		GlobalRevision = checkpoint.GlobalRevision;
		_run = checkpoint.Run;
		_items.Clear();
		foreach (var item in checkpoint.Items)
		{
			_items[item.Identity.InstanceId] = item;
		}

		Operations.Clear();
	}
}
