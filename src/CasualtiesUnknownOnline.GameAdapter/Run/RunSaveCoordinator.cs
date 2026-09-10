using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
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
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly IWorldSaveControl _saves = saves;
	private readonly WorldParamsService _parameters = parameters;
	private readonly Character.CharacterDataSync _characterData = characterData;
	private readonly ILogger _log = log;

	/// <summary>
	/// Host AND solo: every run gets its own world folder before any content
	/// exists, so a later cut has somewhere to go. A guest never writes (the
	/// Runtime refuses it too — the host is the only save authority).
	/// </summary>
	internal void BeginRun()
	{
		if (_session.Role != SessionRole.Guest)
		{
			_saves.TryBeginRun();
		}
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
			_log.LogError("CUO continue refused ({WorldId}): {Summary}", outcome.WorldId, outcome.Summary);
			return false;
		}

		_parameters.MarkRestorePending();
		_world.SetHostRunPending(true);
		_log.LogInformation("Continuing CUO world {WorldId}: {Summary}", outcome.WorldId, outcome.Summary);
		return true;
	}

	/// <summary>The local body's character snapshot right now — the cut needs the state at this instant, not the last 1 Hz report.</summary>
	internal CharacterDataMsg? CaptureLocal(Body? body) =>
		body is null ? null : _characterData.CaptureLocal(body); // Unity object — is null misses scene-reload-destroyed
}
