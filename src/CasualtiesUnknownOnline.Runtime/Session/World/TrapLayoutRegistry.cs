using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using System;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Host-authoritative record of the GENERATED trap/mechanism layout — the
/// entity-distribution truth (the game distributes entities with physics
/// queries the random-stream isolation does not cover, so the guest's
/// regenerated layout diverges — observed: the host's spike at (-13,466.8),
/// the guest's nearest 42 units away). Keyed by (kind, cell) — the position
/// key plus the kind (two kinds can share a cell without being the same
/// entity). Resets when a new world layer starts generating (the same
/// lifecycle as the trap-consumption and opened-entities tables); ships to
/// members on their world entry (TrapLayoutSnapshot, sent alongside the
/// other world-entry snapshots).
/// </summary>
public sealed class TrapLayoutRegistry(ISessionControl session, PacketSender sender)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;

	private const int MaxEntries = 65536; // cap, mirroring the trap-consumption table

	private readonly Dictionary<(EntityEventKind Kind, int X, int Y), TrapLayoutEntryMsg> _layout = [];

	/// <summary>Host only: record one generated entity (the adapter's scanner reports it on the generation-finished edge).</summary>
	public void Report(EntityEventKind kind, float x, float y, string prefabName)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		var key = (kind, (int)Math.Floor(x), (int)Math.Floor(y));
		if (_layout.Count >= MaxEntries && !_layout.ContainsKey(key))
		{
			return;
		}

		_layout[key] = new TrapLayoutEntryMsg { Kind = kind, X = x, Y = y, PrefabName = prefabName };
	}

	/// <summary>Host only: send the layout to one member (on its world entry).</summary>
	public void SendSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _layout.Count == 0)
		{
			return;
		}

		var msg = new TrapLayoutSnapshotMsg { Entries = [.. _layout.Values] };
		_sender.Send(targetSteamId, NetMsg.TrapLayoutSnapshot, msg);
	}

	/// <summary>Host only: a new world layer is generating — the layout starts empty again.</summary>
	public void Reset() => _layout.Clear();
}
