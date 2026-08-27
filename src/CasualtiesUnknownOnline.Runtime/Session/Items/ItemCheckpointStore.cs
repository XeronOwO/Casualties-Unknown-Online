using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Phase B temporary item checkpoint path. The kernel already owns the typed
/// checkpoint; this store is the in-memory seam used before the Phase C save
/// format arrives. A real disk/save header is Phase C scope.
/// </summary>
public sealed class ItemCheckpointStore(ItemKernelAuthority authority)
{
	private readonly Dictionary<string, GameCheckpoint> _checkpoints = [];

	public void Save(string slot) => _checkpoints[slot] = authority.CreateCheckpoint();

	public bool TryLoad(string slot)
	{
		if (!_checkpoints.TryGetValue(slot, out var checkpoint))
		{
			return false;
		}

		return authority.Restore(checkpoint).Success;
	}

	public void Clear() => _checkpoints.Clear();

	public int Count => _checkpoints.Count;
}
