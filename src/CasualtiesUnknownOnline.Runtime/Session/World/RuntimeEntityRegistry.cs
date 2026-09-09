using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The host's accepted runtime-created world-entity table (sync-coverage audit
/// E3): every creation the host accepted — its own local creations and the
/// guests' reports — stored as the exact <see cref="EntitySpawnedMsg"/> the live
/// channel relays, keyed by <see cref="RuntimeEntityKey"/>. This is the
/// entity-registration authority the world-entry snapshot group and the 60 s
/// cycle send absolutely, so a swallowed creation report (guest → host) or
/// relay (host → guest) heals without a reconnect; the creation-time payload
/// (keypad code, geyser liquid type, crystal tint) travels with the record.
/// Entries are dropped when the host's own copy of that creation dies (the
/// entity must never be resurrected by a later re-broadcast) and cleared when a
/// new world/layer starts generating — the same lifecycle as the trap-layout
/// table. Bounded: at the cap a NEW key is refused (the caller logs the
/// overflow episode once); an existing key always updates.
/// </summary>
public sealed class RuntimeEntityRegistry(ISessionControl session, PacketSender sender)
{
	/// <summary>Same bound as the block difference table — a mod storm cannot grow the table without bound.</summary>
	public const int DefaultCap = 65536;

	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly Dictionary<RuntimeEntityKey, EntitySpawnedMsg> _entities = [];
	private readonly int _cap = DefaultCap;

	/// <summary>Test seam (InternalsVisibleTo): the cap is a policy constant; tests exercise the overflow with a small bound.</summary>
	internal RuntimeEntityRegistry(ISessionControl session, PacketSender sender, int cap)
		: this(session, sender)
	{
		_cap = cap;
	}

	public int Count => _entities.Count;

	public int Cap => _cap;

	/// <summary>
	/// Host only: record (or refresh) an accepted creation. A newer message for
	/// the same key replaces the older record — a guest's re-report after the
	/// entity moved still describes the same creation, and the host's own
	/// enriched relay (generated keypad code) must win over the raw report.
	/// Returns false when the cap refused a NEW key.
	/// </summary>
	public bool Report(EntitySpawnedMsg msg)
	{
		if (_session.Role != SessionRole.Host)
		{
			return false;
		}

		var key = RuntimeEntityKey.From(msg);
		if (!_entities.ContainsKey(key) && _entities.Count >= _cap)
		{
			return false;
		}

		_entities[key] = msg;
		return true;
	}

	/// <summary>
	/// Host only: the runtime-created entity at this creation position is gone
	/// (its local copy died) — drop the record so no snapshot or re-broadcast
	/// resurrects it. Returns whether a record was removed.
	/// </summary>
	public bool Remove(string id, float x, float y)
	{
		if (_session.Role != SessionRole.Host)
		{
			return false;
		}

		return _entities.Remove(new RuntimeEntityKey(id, (int)Math.Floor(x), (int)Math.Floor(y)));
	}

	/// <summary>Host only: send the absolute table to one member (world entry, or the 60 s cycle). A no-op when nothing was created yet.</summary>
	public void SendSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _entities.Count == 0 || targetSteamId == 0)
		{
			return;
		}

		var msg = new RuntimeEntitySnapshotMsg { Entries = [.. _entities.Values] };
		_sender.Send(targetSteamId, NetMsg.RuntimeEntitySnapshot, msg);
	}

	/// <summary>Host only: a new world layer is generating — the previous layer's runtime entities are gone with the scene.</summary>
	public void Reset()
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		_entities.Clear();
	}

	/// <summary>The recorded creations (tests and diagnostics; the live send uses <see cref="SendSnapshot"/>).</summary>
	internal IReadOnlyList<EntitySpawnedMsg> Entries => [.. _entities.Values];
}
