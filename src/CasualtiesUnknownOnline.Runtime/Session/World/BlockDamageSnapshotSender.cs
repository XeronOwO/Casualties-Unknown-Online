using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The late-joiner partial block-damage snapshot's SEND half. CUO keeps no
/// partial-damage table of its own: the rows are read from the GAME's own
/// <c>WorldGeneration.world.blockDamages</c> list at send time, so what a member
/// receives is exactly what this host's gameplay holds — same cells, same order,
/// same bound (the game's own 128 entries, <c>WorldGeneration.cs:732-737</c>).
/// That is also why the CUO-side registry that used to sit beside the game's list
/// is gone: two bounded sets with different caps and different eviction policies
/// are two sets that drift.
///
/// The HOST side is the only side this makes exact. The receiver applies the rows
/// into its own game list, which can hold cells this host never saw (its own
/// unhooked local damage), and a receiver whose list is full refuses the rows it
/// cannot take — per cell, in the log, never silently.
///
/// The reader is the adapter's and is OPTIONAL by design (see
/// <see cref="INativeWorldFacts"/>): a composition without one — the Runtime-only
/// test host — sends nothing and says so, rather than inventing rows.
/// </summary>
internal sealed class BlockDamageSnapshotSender(
	ISessionControl session,
	PacketSender sender,
	INativeWorldFacts? nativeWorldFacts,
	KernelWorldGenerationSource generations,
	ILogger<WorldService> log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly INativeWorldFacts? _nativeWorldFacts = nativeWorldFacts;
	private readonly KernelWorldGenerationSource _generations = generations;
	private readonly ILogger<WorldService> _log = log;

	/// <summary>Host only: send the partial damage this host's game list holds (world entry / reconnect / the 60 s resend).</summary>
	internal void Send(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		if (_nativeWorldFacts is null)
		{
			_log.LogDebug("[BlockDamageSnapshot] no native world-fact reader is registered — the snapshot carries no partial damage.");
			return;
		}

		var entries = _nativeWorldFacts.CaptureBlockDamages();
		if (entries is null)
		{
			// No live world to read the game's own list from: the snapshot carries no
			// partial damage rather than an empty set that reads as "no cracks".
			_log.LogWarning("[BlockDamageSnapshot] no live world to read the game's own block-damage list from — the snapshot carries no partial damage.");
			return;
		}

		if (entries.Count == 0)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.BlockDamageSnapshot, new BlockDamageSnapshotMsg { Entries = [.. entries], Generation = _generations.Stamp() });
	}
}
