using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The host's block-table reconcile: the 60 s step and the per-member baseline of the rows each
/// member was last told about. The service owns the table and the wire; this owns the clock and
/// the difference, so the difference can be read without a session (the reconcile is a backstop —
/// deterministic generation stays the fix, the ticket says why).
/// </summary>
internal sealed class BlockTableReconciler
{
	/// <summary>The same 60 s step the member→host re-report uses — one cadence for both directions.</summary>
	private const int IntervalMs = 60_000;

	private readonly Dictionary<ulong, Dictionary<(int X, int Y), ushort>> _baselines = [];

	private long _nextMs;

	/// <summary>
	/// False until the step is due. The first call only arms the clock: a member that just entered
	/// received the entry snapshot, so an immediate reconcile would repeat it.
	/// </summary>
	public bool TryBegin(long nowMs)
	{
		if (_nextMs == 0)
		{
			_nextMs = nowMs + IntervalMs;
			return false;
		}

		if (nowMs < _nextMs)
		{
			return false;
		}

		_nextMs = nowMs + IntervalMs;
		return true;
	}

	/// <summary>
	/// The rows that changed since each member was last told. A member with no change produces no
	/// list, so an idle run sends no message at all; the baseline moves only by what this call
	/// returns.
	/// </summary>
	public List<(ulong Member, List<BlockStateEntryMsg> Rows)> Diff(
		IEnumerable<ulong> members,
		IEnumerable<DamagedBlock> cells)
	{
		var table = new List<DamagedBlock>(cells);
		List<(ulong Member, List<BlockStateEntryMsg> Rows)> changed = [];
		foreach (var member in members)
		{
			if (!_baselines.TryGetValue(member, out var baseline))
			{
				baseline = [];
				_baselines[member] = baseline;
			}

			List<BlockStateEntryMsg> rows = [];
			foreach (var cell in table)
			{
				if (baseline.TryGetValue((cell.X, cell.Y), out var sent) && sent == cell.Block)
				{
					continue;
				}

				rows.Add(new BlockStateEntryMsg
				{
					X = cell.X,
					Y = cell.Y,
					Block = cell.Block,
					SupportLossSettled = cell.SupportLossSettled,
				});
				baseline[(cell.X, cell.Y)] = cell.Block;
			}

			if (rows.Count > 0)
			{
				changed.Add((member, rows));
			}
		}

		return changed;
	}

	/// <summary>Drops a member's baseline (it left the world; its next entry sends a fresh snapshot).</summary>
	public void Forget(ulong member) => _baselines.Remove(member);

	/// <summary>Drops every baseline and re-arms the clock (session reset).</summary>
	public void Reset()
	{
		_baselines.Clear();
		_nextMs = 0;
	}
}
