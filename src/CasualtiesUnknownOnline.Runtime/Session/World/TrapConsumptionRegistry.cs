using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Host-authoritative record of ONE-SHOT trap consumptions (position-keyed —
/// world entities are generated deterministically, so the position IS the
/// entity's identity). Repeatable traps (clamps, fences, coils, geysers, jump
/// pads, ...) are NOT recorded: each side's copy re-arms naturally, which is
/// the vanilla behaviour and the correct late-joiner state. Mirrors the
/// damaged-blocks table's ownership and lifecycle: it lives in the world
/// domain, resets when a new world starts generating (via ResetDamagedBlocks —
/// the same lifecycle), and ships to members on their world entry
/// (TrapStateSnapshot, sent alongside the block-state snapshot).
/// </summary>
public sealed class TrapConsumptionRegistry(ISessionControl session, PacketSender sender)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;

	private const int MaxConsumptions = 65536; // cap, mirroring the damaged-blocks table

	private readonly Dictionary<(int, int), (EntityEventKind Kind, byte Extra)> _consumed = [];

	/// <summary>Host only: record a one-shot consumption at a world position (integer key — the position is identity, sub-unit drift is noise). Extra rides along (ScrapEaterProgress = the progress %).</summary>
	public void Report(EntityEventKind kind, float x, float y, byte extra)
	{
		var key = ((int)Math.Floor(x), (int)Math.Floor(y));
		if (_consumed.Count >= MaxConsumptions && !_consumed.ContainsKey(key))
		{
			return;
		}

		_consumed[key] = (kind, extra);
	}

	/// <summary>Host only: a new world layer is generating — the table starts empty again.</summary>
	public void Reset() => _consumed.Clear();

	/// <summary>Host only: send the consumptions to one member (on its world entry).</summary>
	public void SendSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _consumed.Count == 0)
		{
			return;
		}

		var msg = new TrapStateSnapshotMsg
		{
			Consumed = [.. _consumed.Select(kv => new EntityEventMsg
			{
				Kind = kv.Value.Kind,
				Extra = kv.Value.Extra,
				// The integer key's cell centre — the receiver's entity lookup
				// tolerates sub-cell drift and the 3-unit matching radius.
				Position = new NetVector2Msg(kv.Key.Item1 + 0.5f, kv.Key.Item2 + 0.5f),
			})],
		};
		_sender.Send(targetSteamId, NetMsg.TrapStateSnapshot, msg);
	}
}
