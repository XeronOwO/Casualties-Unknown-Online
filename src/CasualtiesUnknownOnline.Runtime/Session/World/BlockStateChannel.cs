using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The host's block difference table as one message family: the live report that
/// records a cell, the snapshot a late joiner receives, the reconcile backstop and
/// the save layer's capture/apply surface.
/// <para>
/// Split out of <see cref="WorldStateMessageService"/> because the table, its cap and
/// its three sends change together while the rest of the world-state messages do not.
/// The save layer still reads the table as the wire shape it already knew, so this
/// type owns the only surface that escapes the table.
/// </para>
/// </summary>
internal sealed class BlockStateChannel(
	ISessionControl session,
	PacketSender sender,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger _log = log;

	private readonly Dictionary<(int, int), DamagedBlock> _damagedBlocks = [];

	/// <summary>The cap the live table refuses new cells past (the payload bound the reconcile relies on).</summary>
	public const int MaxDamagedBlocks = 65536;

	/// <summary>Guest: the host's block rows arrived (the local table applies them).</summary>
	public event Action<IReadOnlyList<DamagedBlock>>? BlockStateReceived;

	public void FireBlockStateReceived(IReadOnlyList<DamagedBlock> blocks) => BlockStateReceived?.Invoke(blocks);

	public void ReportBlockState(int x, int y, ushort block)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (_damagedBlocks.Count >= MaxDamagedBlocks && !_damagedBlocks.ContainsKey((x, y)))
		{
			return;
		}

		// A live write: whatever support loss this air transition causes is settled
		// HERE (on the side that owns the world), so a receiver re-runs it.
		_damagedBlocks[(x, y)] = new DamagedBlock(x, y, block, SupportLossSettled: false);
	}

	public void RemoveBlockState(int x, int y)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_damagedBlocks.Remove((x, y));
	}

	public void SendBlockStateSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _damagedBlocks.Count == 0)
		{
			return;
		}

		var msg = new BlockStateMsg
		{
			Blocks = [.. _damagedBlocks.Values.Select(cell => new BlockStateEntryMsg
			{
				X = cell.X,
				Y = cell.Y,
				Block = cell.Block,
				SupportLossSettled = cell.SupportLossSettled,
			})],
		};
		_sender.Send(targetSteamId, NetMsg.WorldBlockState, msg);
		_log.LogInformation("Sent block-state snapshot ({Count} blocks) to {Peer}.", _damagedBlocks.Count, targetSteamId);
	}

	/// <summary>The rows each member has been told about, so a reconcile sends differences only.</summary>
	private readonly BlockTableReconciler _blockReconciler = new();

	/// <summary>
	/// Host: the drift backstop. The entry snapshot is the only time a member hears the table; a
	/// write no live event carried stayed wrong for the run. Every step this sends each in-world
	/// member only the rows that changed (an idle run sends nothing; the cap bounds the payload).
	/// </summary>
	public void ReconcileBlockTable(long nowMs)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_blockReconciler.TryBegin(nowMs))
		{
			return;
		}

		foreach (var (member, rows) in _blockReconciler.Diff(_session.InWorldRemoteSteamIds(), _damagedBlocks.Values))
		{
			_sender.Send(member, NetMsg.WorldBlockState, new BlockStateMsg { Blocks = [.. rows] });
			_log.LogInformation("Reconciled {Count} block row(s) to {Peer}.", rows.Count, member);
		}
	}

	/// <summary>The host's block difference table in the wire shape the late-joiner snapshot already sends.</summary>
	public IReadOnlyList<BlockStateEntryMsg> CaptureBlockStates() =>
	[.. _damagedBlocks.Values.Select(cell => new BlockStateEntryMsg
	{
		X = cell.X,
		Y = cell.Y,
		Block = cell.Block,
		SupportLossSettled = cell.SupportLossSettled,
	})];

	/// <summary>
	/// Host: upsert one restored block state. The cell is the identity, so a
	/// repeated cell simply wins (a snapshot never carries the same cell twice).
	/// Returns false when the table's cap refused a cell it does not already
	/// hold — the restore report counts it instead of reporting a clean restore.
	/// </summary>
	public bool ApplyBlockState(BlockStateEntryMsg entry)
	{
		if (_damagedBlocks.Count >= MaxDamagedBlocks && !_damagedBlocks.ContainsKey((entry.X, entry.Y)))
		{
			_log.LogWarning("[SaveFacts] block-state ({X},{Y}) skipped: the {Cap}-cell table is full.", entry.X, entry.Y, MaxDamagedBlocks);
			return false;
		}

		_damagedBlocks[(entry.X, entry.Y)] = new DamagedBlock(entry.X, entry.Y, entry.Block, entry.SupportLossSettled);
		return true;
	}

	/// <summary>Empty the table — a cleared layer or a restore about to write the whole set.</summary>
	public void Clear() => _damagedBlocks.Clear();
}
