using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The host's accepted runtime-created world-entity table (sync-coverage audit
/// E3): every creation the host accepted — its own local creations and the
/// guests' reports — stored as the exact <see cref="EntitySpawnedMsg"/> the live
/// channel relays, keyed by <see cref="RuntimeEntityKey"/> (prefab id +
/// creation cell + creation-instance token). This is the entity-registration
/// authority the world-entry snapshot group and the 60 s cycle send absolutely,
/// so a swallowed creation report (guest → host) or relay (host → guest) heals
/// without a reconnect; the creation-time payload (keypad code, geyser liquid
/// type, crystal tint) travels with the record.
/// <para>
/// ANIMAL creations are tracked by KEY ONLY, in a separate set: the enemy
/// domain owns their late-join copy (<c>EnemySnapshot.RuntimeSpawns</c>
/// materializes at the animal's current position), so they must never ride the
/// snapshot's entry list — but the creating guest still needs the host's
/// acknowledgement, which the snapshot's key list carries.
/// </para>
/// Entries are dropped when the host's own copy of that creation dies (the
/// entity must never be resurrected by a later re-broadcast) and cleared when a
/// new world/layer starts generating — the same lifecycle as the trap-layout
/// table. Bounded: at the cap a NEW key is refused (the caller logs the
/// overflow episode once); an existing key always updates.
/// </summary>
public sealed class RuntimeEntityRegistry(ISessionControl session, PacketSender sender)
{
	/// <summary>
	/// Table bound. A mod storm cannot grow the table without bound, AND the
	/// absolute snapshot must fit one transport frame: a creation record is
	/// roughly 80 B on the wire, so 4 096 records is ~320 KiB — well inside the
	/// 1 MiB frame limit. (The block difference table's 65 536 cells are ~8 B
	/// each and still fit; copying that bound here would let a full snapshot
	/// close every peer's connection.)
	/// </summary>
	public const int DefaultCap = 4096;

	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly Dictionary<RuntimeEntityKey, EntitySpawnedMsg> _entities = [];
	private readonly HashSet<RuntimeEntityKey> _acceptedAnimals = [];
	private readonly int _cap = DefaultCap;

	/// <summary>Test seam (InternalsVisibleTo): the cap is a policy constant; tests exercise the overflow with a small bound.</summary>
	internal RuntimeEntityRegistry(ISessionControl session, PacketSender sender, int cap)
		: this(session, sender)
	{
		_cap = cap;
	}

	/// <summary>The accepted NON-ANIMAL creations (the snapshot's materializable entry list).</summary>
	public int Count => _entities.Count;

	/// <summary>The accepted ANIMAL creations — acknowledged by key, never materialized from the snapshot.</summary>
	public int AnimalCount => _acceptedAnimals.Count;

	public int Cap => _cap;

	/// <summary>The table's TOTAL bound across the materializable entries and the animal acknowledgement keys — both ride the same snapshot.</summary>
	private int TotalCount => _entities.Count + _acceptedAnimals.Count;

	/// <summary>
	/// Host only: record (or refresh) an accepted NON-ANIMAL creation. A newer
	/// message for the same key replaces the older record — a guest's re-report
	/// after the entity moved still describes the same creation, and the host's
	/// own enriched relay (generated keypad code) must win over the raw report.
	/// Returns false when the cap refused a NEW key.
	/// </summary>
	public bool Report(EntitySpawnedMsg msg)
	{
		if (_session.Role != SessionRole.Host)
		{
			return false;
		}

		var key = RuntimeEntityKey.From(msg);
		if (!_entities.ContainsKey(key) && TotalCount >= _cap)
		{
			return false;
		}

		_entities[key] = msg;
		return true;
	}

	/// <summary>
	/// Host only: record an accepted ANIMAL creation for ACKNOWLEDGEMENT only.
	/// Nothing materializes from it (the enemy domain owns the animal copy), so
	/// only the key is kept. Returns false when the cap refused a NEW key.
	/// </summary>
	public bool ReportAnimal(EntitySpawnedMsg msg)
	{
		if (_session.Role != SessionRole.Host)
		{
			return false;
		}

		var key = RuntimeEntityKey.From(msg);
		if (!_acceptedAnimals.Contains(key) && TotalCount >= _cap)
		{
			return false;
		}

		_acceptedAnimals.Add(key);
		return true;
	}

	/// <summary>
	/// Host only: the runtime-created entity with this creation key is gone (its
	/// local copy died) — drop the record so no snapshot or re-broadcast
	/// resurrects it. Returns whether a record was removed.
	/// </summary>
	public bool Remove(RuntimeEntityKey key)
	{
		if (_session.Role != SessionRole.Host)
		{
			return false;
		}

		// Both sets are always consulted: a key belongs to exactly one of them
		// for a well-behaved peer, but a peer that flips IsAnimal between reports
		// must not leave a stale animal acknowledgement behind.
		var removedEntry = _entities.Remove(key);
		var removedAnimal = _acceptedAnimals.Remove(key);
		return removedEntry || removedAnimal;
	}

	/// <summary>Host only: send the absolute table to one member (world entry, or the 60 s cycle). A no-op when nothing was accepted yet.</summary>
	public void SendSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || targetSteamId == 0)
		{
			return;
		}

		if (_entities.Count == 0 && _acceptedAnimals.Count == 0)
		{
			return;
		}

		var msg = new RuntimeEntitySnapshotMsg
		{
			Entries = [.. _entities.Values],
			AcceptedAnimalKeys = [.. _acceptedAnimals.Select(key => key.ToKeyMsg())],
		};
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
		_acceptedAnimals.Clear();
	}

	/// <summary>The recorded creations (tests and diagnostics; the live send uses <see cref="SendSnapshot"/>).</summary>
	internal IReadOnlyList<EntitySpawnedMsg> Entries => [.. _entities.Values];

	/// <summary>The recorded animal keys (tests and diagnostics).</summary>
	internal IReadOnlyCollection<RuntimeEntityKey> AnimalKeys => _acceptedAnimals;
}
