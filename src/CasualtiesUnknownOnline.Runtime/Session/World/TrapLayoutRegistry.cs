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

	/// <summary>Host only: record one generated entity (the adapter's scanner reports it on the generation-finished edge). <paramref name="creationKey"/> rides along when the host's copy carries a runtime-creation marker.</summary>
	public void Report(EntityEventKind kind, float x, float y, string prefabName, RuntimeEntityKeyMsg? creationKey = null)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		Upsert(kind, x, y, prefabName, creationKey);
	}

	/// <summary>
	/// Host only: replace the whole table with a fresh scan of the LIVE scene —
	/// the in-session repair re-derives the layout from the world right before it
	/// sends it, so an entity the world has since removed (a self-destructed
	/// turret, a broken crystal) is gone from the snapshot instead of being
	/// re-materialized on every peer every cycle.
	///
	/// An EMPTY scan against a non-empty table is refused: inactive and unloaded
	/// objects are invisible to the scene scan, so "the whole layer's traps were
	/// destroyed at once" is far less likely than a scene in transition, and the
	/// fail-safe direction is a stale entry (corrected by the next scan that sees
	/// entities) rather than a mass destroy on every guest. A layer that has
	/// genuinely emptied is re-derived at its next generation edge, which clears
	/// the table first.
	///
	/// Returns false when the replace did NOT happen — the caller is not the host,
	/// or the empty-scan fail-safe refused it — so the refusing branch is
	/// observable instead of silent (the caller logs it).
	/// </summary>
	public bool Replace(IReadOnlyList<TrapLayoutEntryMsg> entries)
	{
		if (_session.Role != SessionRole.Host || (entries.Count == 0 && _layout.Count > 0))
		{
			return false;
		}

		_layout.Clear();
		foreach (var entry in entries)
		{
			Upsert(entry.Kind, entry.X, entry.Y, entry.PrefabName, entry.CreationKey);
		}

		return true;
	}

	/// <summary>Host only: send the layout to one member (on its world entry, or on the in-session repair).</summary>
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

	private void Upsert(EntityEventKind kind, float x, float y, string prefabName, RuntimeEntityKeyMsg? creationKey)
	{
		var key = (kind, (int)Math.Floor(x), (int)Math.Floor(y));
		if (_layout.Count >= MaxEntries && !_layout.ContainsKey(key))
		{
			return;
		}

		_layout[key] = new TrapLayoutEntryMsg { Kind = kind, X = x, Y = y, PrefabName = prefabName, CreationKey = creationKey };
	}
}
