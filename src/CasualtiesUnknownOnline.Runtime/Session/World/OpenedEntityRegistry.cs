using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Host-authoritative record of OPENED lockable entities (position-keyed —
/// world entities are generated deterministically, so the position IS the
/// entity's identity). An open is a one-shot write (health = 0) with no
/// re-open, so a late joiner must learn the opens from the host instead of
/// seeing doors that are open on every other side closed (observed: the
/// survival pod door). Mirrors TrapConsumptionRegistry's shape: lives in the
/// world domain, resets when a new world starts generating, ships to members
/// on their world entry (OpenedEntitiesSnapshot, sent alongside the
/// block-state and trap-state snapshots).
/// </summary>
public sealed class OpenedEntityRegistry(ISessionControl session, PacketSender sender)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;

	private const int MaxOpened = 4096; // cap, mirroring the damaged-blocks table

	private readonly HashSet<(int, int)> _opened = [];

	/// <summary>Host only: record an opened entity at a world position (integer key — the position is identity, sub-unit drift is noise). Idempotent.</summary>
	public void Report(float x, float y)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		if (_opened.Count >= MaxOpened && !_opened.Contains(((int)Math.Floor(x), (int)Math.Floor(y))))
		{
			return;
		}

		_opened.Add(((int)Math.Floor(x), (int)Math.Floor(y)));
	}

	/// <summary>Host only: a new world layer is generating — the opens start empty again.</summary>
	public void Reset() => _opened.Clear();

	/// <summary>Host only: send the opened positions to one member (on its world entry).</summary>
	public void SendSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _opened.Count == 0)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.OpenedEntitiesSnapshot, new OpenedEntitiesSnapshotMsg
		{
			// The integer key's cell centre — the receiver's entity lookup
			// tolerates sub-cell drift (OverlapPoint at the exact position).
			Positions = [.. _opened.Select(k => new NetVector2Msg(k.Item1 + 0.5f, k.Item2 + 0.5f))],
		});
	}
}
