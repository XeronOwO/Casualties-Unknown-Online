using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The partial block-damage snapshot surface of <see cref="WorldService"/>
/// (split off at the 600-line gate): the registry forwarding + the guest-side
/// receive event. The live delta chain stays in WorldService.cs and the Game
/// Adapter's BlockBreakSync; this surface only owns the late-joiner backfill.
/// </summary>
public sealed partial class WorldService
{
	/// <summary>Guest: the host's partial block-damage snapshot arrived — apply each entry absolutely.</summary>
	public event Action<IReadOnlyList<BlockDamageEntryMsg>>? BlockDamageSnapshotReceived;

	public void FireBlockDamageSnapshotReceived(IReadOnlyList<BlockDamageEntryMsg> entries) =>
		BlockDamageSnapshotReceived?.Invoke(entries);

	/// <summary>Host only: record the block's current accumulated damage (the late-joiner snapshot's fact source).</summary>
	public void ReportBlockDamage(int x, int y, float damage) => _blockDamageRegistry.Report(x, y, damage);

	/// <summary>Host only: the block broke or was air-written away — its partial damage is gone.</summary>
	public void RemoveBlockDamage(int x, int y) => _blockDamageRegistry.Remove(x, y);

	/// <summary>Host only: send the recorded damage to one member (on its world entry / reconnect / the 60 s resend).</summary>
	public void SendBlockDamageSnapshot(ulong targetSteamId) => _blockDamageRegistry.SendSnapshot(targetSteamId);
}
