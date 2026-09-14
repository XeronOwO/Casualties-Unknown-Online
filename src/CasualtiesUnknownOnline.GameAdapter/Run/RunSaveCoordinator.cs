using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.GameAdapter.World;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The run's save arm: it reports the two moments the game itself knows about
/// (a run starts, the host continues an existing one) and hands the save layer
/// the live character of the moment it leaves the world. Split out of
/// <see cref="RunCoordinator"/> — the run life-cycle phase machine and the save
/// authority are separate responsibilities, and the coordinator had reached the
/// architecture line gate.
///
/// The Runtime owns the repository, the format and the kernel; nothing here
/// decides what a snapshot holds.
/// </summary>
internal sealed class RunSaveCoordinator(
	ISessionControl session,
	IWorldControl world,
	IWorldSaveControl saves,
	WorldParamsService parameters,
	Character.CharacterDataSync characterData,
	IItemControl items,
	WorldRestoreAudit? restoreAudit,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly IWorldSaveControl _saves = saves;
	private readonly WorldParamsService _parameters = parameters;
	private readonly Character.CharacterDataSync _characterData = characterData;
	private readonly IItemControl _items = items;
	private readonly ILogger _log = log;

	/// <summary>
	/// The caller of <see cref="IWorldSaveControl.TryContinue"/> is also where the
	/// restore's SECOND half reports: the Continue click applied the kernel and the
	/// Runtime tables, and the live world's write happens at the world-entry seam
	/// afterwards. Without this subscription a restore that the game's own bounded
	/// tables partly refused would look like a clean success at the only place the
	/// adapter ever sees its outcome.
	/// </summary>
	private readonly WorldRestoreAudit? _restoreAudit = restoreAudit;

	/// <summary>
	/// Host AND solo: every RUN gets its own world folder before any content
	/// exists, so a later cut has somewhere to go. A guest never writes and a
	/// TUTORIAL entry gets no archive at all — both rules belong to the save layer
	/// (<see cref="IWorldSaveControl.TryBeginRun"/>), which also releases the
	/// previous run's identity on a tutorial entry. What stays here is the run's
	/// own restore arm: a new run owns the next generation, so a restore armed for
	/// a refused or abandoned Continue attempt must never replay into it.
	/// </summary>
	internal void BeginRun(bool isTutorial)
	{
		// A new run owns the next generation: a restore armed for a refused/aborted
		// Continue attempt must never replay into it — the world baseline, the native
		// values (the adapter cancels its own handover with the same call) and the
		// local body's queued character alike. The item domain's restore arm belongs to
		// that set too: left armed, the new run's generation would reconcile its fresh
		// objects against the old world's ids.
		//
		// This half runs for EVERY entry, the tutorial included: abandoning a stale
		// restore is not a save-repository action.
		_parameters.CancelRestorePending();
		_characterData.CancelAllLocalRestores();
		_items.CancelRestoredWorldItems("a new run superseded the restore");

		if (_session.Role == SessionRole.Guest)
		{
			return;
		}

		// The tutorial is refused by the save layer itself (no archive for a
		// tutorial entry, and the previous run's identity is released there).
		_saves.TryBeginRun(isTutorial);
	}

	/// <summary>
	/// The native Continue entry was used on the host (or in solo play). CUO
	/// restores the world the repository resolves as "the selected one" — kernel
	/// checkpoint first, characters second — and marks the generation boundary to
	/// replay the RESTORED run baseline instead of capturing a new one. False
	/// blocks the original LoadRun: falling through would read the native
	/// <c>save.sv</c> and regenerate a layer the snapshot never named (decision 165).
	/// </summary>
	internal bool OnContinueRequested()
	{
		if (_session.Role == SessionRole.Guest)
		{
			return false; // the guest gate already refused; never restore the host's world on a guest
		}

		if (!_saves.TryContinue(out var outcome))
		{
			// A previous attempt's local restore must not survive a refused one: this
			// run will never reach a body.
			_characterData.CancelAllLocalRestores();
			_log.LogError("CUO continue refused ({WorldId}): {Summary}", outcome.WorldId, outcome.Summary);
			return false;
		}

		// The layer is generated from the RESTORED baseline: WorldGeneration.Start reads
		// the run settings BEFORE GenerateWorld fires, so the baseline is applied here,
		// at the click. A missing baseline is a hard refusal — generating from the live
		// RNG stream is exactly the silent restart the restore contract forbids.
		if (!_parameters.TryApplyRestoredNow())
		{
			// The restore will never reach its world-entry seam: the save layer releases
			// every handover the click armed (the account, the Runtime fact tables, the
			// kernel's restored per-entity facts, the native handover and the item
			// reconcile) — leaving any of them armed would make the next generation skip
			// its layer-boundary reset and write a dead attempt's facts into it.
			_saves.AbandonRestore("the restore published no run baseline, so no world generation will consume it");
			_characterData.CancelAllLocalRestores();
			_log.LogError("CUO continue refused ({WorldId}): the restore published no run baseline.", outcome.WorldId);
			return false;
		}

		_world.SetHostRunPending(true);

		// The host's own character comes back the same way a respawn's does: queued on
		// the local two-frame restore path, applied when the freshly generated world
		// puts a body under this client. Nothing else can do it — the character table
		// slot it was bound into is the same one the live 1 Hz snapshot writes, so the
		// fresh body would overwrite it before anything read it, and the world hands
		// out no starting supplies when a run is being continued (WorldPlacePlayer).
		QueueLocalCharacter(outcome.LocalCharacter);

		_log.LogInformation("Continuing CUO world {WorldId}: {Summary}", outcome.WorldId, outcome.Summary);

		// The live-world half of this restore reports at the world-entry seam, after
		// this method returned. Subscribe here — the click is what started it — so
		// the caller that saw "restored" also sees what the live world actually took.
		if (_restoreAudit is not null)
		{
			_restoreAudit.Reported -= OnRestoreLiveWrite;
			_restoreAudit.Reported += OnRestoreLiveWrite;
		}

		return true;
	}

	/// <summary>
	/// Hand the archive's character for this player to the local restore path. A null
	/// one is decision 162's "that player joins as a NEW character" (the archive had no
	/// file this session's key space claims) — named, not silently skipped.
	/// </summary>
	private void QueueLocalCharacter(CharacterDataMsg? localCharacter)
	{
		if (localCharacter is null)
		{
			_log.LogInformation("CUO continue: the archive carries no character this player claims; the run starts it as a new character (decision 162).");
			return;
		}

		_characterData.QueueLocalRestore(localCharacter);
	}

	/// <summary>The restore's second half: the world-entry seam wrote the restored facts into the live world.</summary>
	private void OnRestoreLiveWrite(WorldRestoreLiveWriteReport report)
	{
		if (report.Complete)
		{
			_log.LogInformation("CUO restore of world {WorldId} reached the live world: {Summary}", report.WorldId, report.Summary);
			return;
		}

		_log.LogError(
			"CUO restore of world {WorldId} is INCOMPLETE — the live world did not take every restored fact: {Summary}",
			report.WorldId, report.Summary);
	}

	/// <summary>The local body's character snapshot right now — the cut needs the state at this instant, not the last 1 Hz report.</summary>
	internal CharacterDataMsg? CaptureLocal(Body? body) =>
		body is null ? null : _characterData.CaptureLocal(body); // Unity object — is null misses scene-reload-destroyed
}
