using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

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
public sealed class BlockDamageRegistry(ISessionControl session, PacketSender sender, ILogger<BlockDamageRegistry> log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<BlockDamageRegistry> _log = log;

	/// <summary>The game's own blockDamages list caps at 128 active entries (WorldGeneration.cs:732-737); 256 leaves headroom for report/remove churn while staying bounded.</summary>
	private const int MaxEntries = 256;

	private readonly Dictionary<(int, int), float> _damage = [];

	/// <summary>
	/// Record the block's current accumulated damage at a block cell (latest write
	/// wins — idempotent under re-reporting). A non-positive value removes the
	/// record.
	///
	/// No role gate: the TABLE-OWNING surface does that (the message service's
	/// <c>ReportBlockDamage</c> is host-gated, and the save layer's restore is
	/// refused for a guest), and a second gate here would be a silent no-op in the
	/// no-lobby state the game reports as <c>SessionRole.None</c> — which is exactly
	/// how a restore would lose its partial damage while reporting success.
	/// </summary>
	public void Report(int x, int y, float damage)
	{
		var key = (x, y);
		if (damage <= 0f)
		{
			_damage.Remove(key);
			return;
		}

		if (_damage.Count >= MaxEntries && !_damage.ContainsKey(key))
		{
			// Cap reached — stop tracking new cells rather than grow unbounded. The
			// cell is NAMED so a snapshot that overflowed the table is visible in the
			// log instead of looking like a clean restore.
			_log.LogWarning("[BlockDamageRegistry] cap reached ({Cap} cells): damage at ({X},{Y}) is not tracked.", MaxEntries, x, y);
			return;
		}

		_damage[key] = damage;
	}

	/// <summary>The block broke or was air-written away — its partial damage is gone with it (no role gate; see <see cref="Report"/>).</summary>
	public void Remove(int x, int y) => _damage.Remove((x, y));

	/// <summary>Host only: a new world layer is generating — the damage records start empty again.</summary>
	public void Reset() => _damage.Clear();

	/// <summary>
	/// Host only: the recorded damage in the wire shape the late-joiner snapshot
	/// sends. The save layer reads the table through this (a read-only view of
	/// the live dictionary never escapes), and <see cref="SendSnapshot"/> sends
	/// exactly what this returns — one authority for the snapshot's shape.
	/// </summary>
	public IReadOnlyList<BlockDamageEntryMsg> CaptureEntries() =>
	[.. _damage.Select(kv => new BlockDamageEntryMsg
	{
		// Block cells are exact integers — unlike the building-entity
		// position lookup, the receiver needs no sub-cell tolerance.
		X = kv.Key.Item1,
		Y = kv.Key.Item2,
		Damage = kv.Value,
	})];

	/// <summary>Host only: send the recorded damage to one member (on its world entry / reconnect / the 60 s resend).</summary>
	public void SendSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _damage.Count == 0)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.BlockDamageSnapshot, new BlockDamageSnapshotMsg
		{
			Entries = [.. CaptureEntries()],
		});
	}
}
