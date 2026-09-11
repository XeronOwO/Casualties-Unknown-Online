using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The Game Adapter's <see cref="INativeWorldFacts"/>: the three world tables
/// only the game can read and write — keypad codes, geyser liquid types and the
/// game's own partial block damage (<c>WorldGeneration.world.blockDamages</c>).
///
/// The capture half runs while a world is alive and reads the live tables. The
/// apply half CANNOT write at the Continue click (the world does not exist yet),
/// so it holds the restored values until the world-entry seam consumes them:
/// <see cref="RestoredWorldFactReplay"/> takes them once the generation completed,
/// writes the restored cut into the live world and the replay clears the marker.
///
/// A pending restore that is never consumed is NOT left behind: the run that owns
/// it calls <see cref="CancelPendingRestore"/> (a new run through the save layer's
/// <c>TryBeginRun</c>, or the end of the session), so the values can never land in
/// a different world. Holding them instead of writing early is deliberate: a
/// keypad code belongs to an <c>Openable</c> the generation has not created yet,
/// and a partial-damage row belongs to a cell whose block does not exist yet.
/// Writing earlier would be a silent no-op that the restore report would call a
/// success.
///
/// Public because the plugin's composition root is the layer that registers the
/// port for the Runtime's save service (the adapter is the only layer that can
/// implement it, and the Runtime cannot reference the adapter).
/// </summary>
public sealed class NativeWorldFacts(ILogger<NativeWorldFacts> log) : INativeWorldFacts
{
	private List<KeypadEntryMsg>? _pendingKeypads;
	private List<GeyserStateEntryMsg>? _pendingGeysers;
	private List<BlockDamageEntryMsg>? _pendingBlockDamages;

	/// <inheritdoc />
	public bool HasPendingRestore =>
		_pendingKeypads is not null || _pendingGeysers is not null || _pendingBlockDamages is not null;

	/// <inheritdoc />
	public NativeWorldFactCapture Capture()
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — == (a layer-end cut never reads these; a mid-run cut always has one)
		{
			// NOT an empty table: the game's own partial-damage list is the only
			// table that holds it, so reading "no world" as "no damage" would store
			// a clean world and lose every crack. The failure refuses the cut.
			log.LogError("[SaveFacts] no live world to read the native world tables from — the cut must not store an empty damage list.");
			return NativeWorldFactCapture.Unreadable("no live world is present, so the game's own block-damage table could not be read");
		}

		return new NativeWorldFactCapture(
			KeypadCodeTable.Capture(),
			GeyserStateTable.Capture(),
			GameBlockDamageTable.Capture(world),
			Failure: null);
	}

	/// <inheritdoc />
	public IReadOnlyList<BlockDamageEntryMsg>? CaptureBlockDamages()
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — == (the caller sends nothing and says so)
		{
			return null;
		}

		return GameBlockDamageTable.Capture(world);
	}

	/// <inheritdoc />
	public void ApplyKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes) => _pendingKeypads = [.. codes];

	/// <inheritdoc />
	public void ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers) => _pendingGeysers = [.. geysers];

	/// <inheritdoc />
	public void ApplyBlockDamages(IReadOnlyList<BlockDamageEntryMsg> damages) => _pendingBlockDamages = [.. damages];

	/// <inheritdoc />
	public NativeWorldFactRestore ReadPendingRestore()
	{
		if (!HasPendingRestore)
		{
			return NativeWorldFactRestore.Empty;
		}

		return new NativeWorldFactRestore(
			_pendingKeypads ?? [],
			_pendingGeysers ?? [],
			_pendingBlockDamages ?? []);
	}

	/// <inheritdoc />
	public void CommitPendingRestore()
	{
		if (!HasPendingRestore)
		{
			return;
		}

		log.LogInformation(
			"[SaveFacts] committed the restored native world facts ({Keypads} keypad code(s), {Geysers} geyser type(s), {Damages} game block-damage row(s)): the live world has them.",
			_pendingKeypads?.Count ?? 0, _pendingGeysers?.Count ?? 0, _pendingBlockDamages?.Count ?? 0);
		ClearPending();
	}

	/// <inheritdoc />
	public void CancelPendingRestore()
	{
		if (!HasPendingRestore)
		{
			return;
		}

		log.LogInformation(
			"[SaveFacts] cancelled the pending restored native world facts ({Keypads} keypad code(s), {Geysers} geyser type(s), {Damages} game block-damage row(s)): the run that owned them never reached the world-entry seam.",
			_pendingKeypads?.Count ?? 0, _pendingGeysers?.Count ?? 0, _pendingBlockDamages?.Count ?? 0);
		ClearPending();
	}

	/// <summary>The pending set is done with — taken by the replay, or discarded when a new run or the session end supersedes it.</summary>
	private void ClearPending()
	{
		_pendingKeypads = null;
		_pendingGeysers = null;
		_pendingBlockDamages = null;
	}
}
