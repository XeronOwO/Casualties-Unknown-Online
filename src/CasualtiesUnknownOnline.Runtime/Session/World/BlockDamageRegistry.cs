using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Host-authoritative record of CURRENT partial block damage (block-cell-keyed
/// — both sides generate the same world from the same RNG baseline, so the
/// cell IS the block's identity). The live BlockDamaged relay is delta-based
/// and only keeps ALREADY-CONNECTED peers aligned; a late joiner regenerates
/// every block with zero accumulated <c>BlockDamage.damage</c>, so a partially
/// mined block would be back at full HP and break later (desynchronizing the
/// damage chain). Mirrors BuildingEntityHealthRegistry's shape: world domain,
/// reset with the world, shipped on world entry (BlockDamageSnapshot) and the
/// 60 s resend.
/// </summary>
public sealed class BlockDamageRegistry(ISessionControl session, PacketSender sender)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;

	/// <summary>The game's own blockDamages list caps at 128 active entries (WorldGeneration.cs:732-737); 256 leaves headroom for report/remove churn while staying bounded.</summary>
	private const int MaxEntries = 256;

	private readonly Dictionary<(int, int), float> _damage = [];

	/// <summary>Host only: record the block's current accumulated damage at a block cell (latest write wins — idempotent under re-reporting). A non-positive value removes the record.</summary>
	public void Report(int x, int y, float damage)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		var key = (x, y);
		if (damage <= 0f)
		{
			_damage.Remove(key);
			return;
		}

		if (_damage.Count >= MaxEntries && !_damage.ContainsKey(key))
		{
			return; // cap reached — stop tracking new entries rather than grow unbounded
		}

		_damage[key] = damage;
	}

	/// <summary>Host only: the block broke or was air-written away — its partial damage is gone with it.</summary>
	public void Remove(int x, int y)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		_damage.Remove((x, y));
	}

	/// <summary>Host only: a new world layer is generating — the damage records start empty again.</summary>
	public void Reset() => _damage.Clear();

	/// <summary>Host only: send the recorded damage to one member (on its world entry / reconnect / the 60 s resend).</summary>
	public void SendSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _damage.Count == 0)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.BlockDamageSnapshot, new BlockDamageSnapshotMsg
		{
			// Block cells are exact integers — unlike the building-entity
			// position lookup, the receiver needs no sub-cell tolerance.
			Entries = [.. _damage.Select(kv => new BlockDamageEntryMsg
			{
				X = kv.Key.Item1,
				Y = kv.Key.Item2,
				Damage = kv.Value,
			})],
		});
	}
}
