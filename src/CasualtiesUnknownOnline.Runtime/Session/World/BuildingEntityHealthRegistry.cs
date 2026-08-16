using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Host-authoritative record of CURRENT building-entity health (position-keyed
/// — world entities are generated deterministically, so the position IS the
/// entity's identity). The live BuildingEntityDamaged relay covers players who
/// are already in the world; a late joiner regenerates every entity at full
/// health and would otherwise resurrect destroyed plants/crates and lose all
/// intermediate damage. Mirrors OpenedEntityRegistry's shape: lives in the
/// world domain, resets when a new world starts generating, ships to members
/// on their world entry (BuildingEntityHealthSnapshot, sent alongside the
/// block-state / trap-state / opened-entities snapshots).
/// </summary>
public sealed class BuildingEntityHealthRegistry(ISessionControl session, PacketSender sender)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;

	private const int MaxEntries = 4096; // cap, mirroring the opened-entities table

	private readonly Dictionary<(int, int), float> _health = [];

	/// <summary>Host only: record the entity's current health at a world position (integer key — the position is identity, sub-unit drift is noise). The latest write wins (idempotent under re-reporting).</summary>
	public void Report(float x, float y, float health)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		var key = ((int)Math.Floor(x), (int)Math.Floor(y));
		if (_health.Count >= MaxEntries && !_health.ContainsKey(key))
		{
			return;
		}

		_health[key] = health;
	}

	/// <summary>Host only: a new world layer is generating — the health records start empty again.</summary>
	public void Reset() => _health.Clear();

	/// <summary>Host only: send the recorded health to one member (on its world entry).</summary>
	public void SendSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _health.Count == 0)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.BuildingEntityHealthSnapshot, new BuildingEntityHealthSnapshotMsg
		{
			// The integer key's cell centre — the receiver's entity lookup
			// tolerates sub-cell drift (OverlapPoint at the exact position).
			Entries = [.. _health.Select(kv => new BuildingEntityHealthEntryMsg
			{
				X = kv.Key.Item1 + 0.5f,
				Y = kv.Key.Item2 + 0.5f,
				Health = kv.Value,
			})],
		});
	}
}
