using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The cut-TRIGGER family, split out of <see cref="WorldSaveService"/> (which owns
/// WHETHER and WHERE a cut may be taken): the armed request, the bounded wait for
/// in-flight state, the interval timer, and the write of the armed request through
/// <see cref="WorldCutWriter"/> — followed by the retention pass every committed cut
/// owes the archive's backup policy (§7).
///
/// The split is the one the class-size gate needed and the one the responsibilities
/// asked for: what a cut is ALLOWED to happen is session state (a role, a world, a
/// refused trigger), while what happens between "armed" and "written" is one bounded
/// sequence with its own clock and its own deadline.
///
/// The interval autosave lives here because it is a trigger like any other: it arms
/// the same request, waits the same deadline, is written by the same seam and is
/// reported through the same event. What it does NOT do is reset on a refusal or a
/// deferral — only a committed cut restarts the interval (see
/// <see cref="WorldAutosaveInterval"/>).
/// </summary>
internal sealed class WorldCutTrigger(
	ISessionControl session,
	WorldCutWriter? writer,
	WorldCharacterBinder binder,
	IWorldCutTransientProbe? transients,
	bool nativeReaderAvailable,
	ILogger<WorldCutTrigger> log,
	Func<DateTime> utcNow)
{
	private readonly ISessionControl _session = session;
	private readonly WorldCutWriter? _writer = writer;
	private readonly WorldCharacterBinder _binder = binder;
	private readonly IWorldCutTransientProbe? _transients = transients;
	private readonly bool _nativeReaderAvailable = nativeReaderAvailable;
	private readonly ILogger<WorldCutTrigger> _log = log;
	private readonly Func<DateTime> _utcNow = utcNow;
	private readonly WorldCutDeferral _deferral = new();
	private readonly WorldAutosaveInterval _autosave = new();

	private WorldCutReason? _armed;

	/// <summary>True = a cut is armed and waiting for the seam (or for in-flight state to resolve).</summary>
	internal bool HasArmed => _armed is not null;

	/// <summary>The writer this trigger drives. Internal because the save suites pin the writer's row shapes directly.</summary>
	internal WorldCutWriter? Writer => _writer;

	/// <summary>
	/// Every finished cut attempt (captured or refused — a deferral is not a result).
	/// The service forwards it to its own subscribers, so the player-facing console sees
	/// the same reports it always did.
	/// </summary>
	internal event Action<WorldCutReport>? Reported;

	/// <summary>The world's write history starts here: called when a run begins or a restore adopts a world.</summary>
	internal void RestartInterval() => _autosave.Restart(_utcNow());

	/// <summary>
	/// No world is owned any more (a tutorial entry, a run that could not be created):
	/// the armed request, its wait and the interval all stand down — a request armed for
	/// a previous world must never cut this one.
	/// </summary>
	internal void StandDown()
	{
		_armed = null;
		_deferral.Reset();
		_autosave.StandDown();
	}

	/// <summary>True = the configured interval has elapsed since this world was last written (and a world is owned at all).</summary>
	internal bool IsAutosaveDue(bool enabled, TimeSpan interval) =>
		_autosave.IsDue(_utcNow(), enabled, interval);

	/// <summary>
	/// Arm a cut for the next seam. Re-arming the trigger that is already armed keeps the
	/// wait it has already spent (the menu-return seam retries every frame a deferred cut
	/// waits) and stays quiet for the log's sake; a different trigger starts its own wait
	/// and names the one it superseded, because one cut is taken at the seam.
	/// </summary>
	internal void Arm(WorldCutReason reason, string worldId)
	{
		if (_armed == reason)
		{
			_log.LogDebug("Cut {Reason} is still armed for world {WorldId}.", reason, worldId);
			return;
		}

		if (_armed is { } armed)
		{
			_log.LogInformation("The armed {Armed} cut is superseded by {Reason}; one cut is taken at the seam.", armed, reason);
		}

		_deferral.Reset();
		_armed = reason;
		_log.LogInformation("Cut {Reason} armed for world {WorldId}: the pump takes it at the frame-end seam.", reason, worldId);
	}

	/// <summary>
	/// Host pump (the frame-end seam): take the armed cut. Null when nothing is armed.
	/// The payload is written at the instant this call runs — the pump point where no
	/// command batch and no frame flush is in flight — and the transient policy decides
	/// whether to defer first. <paramref name="keepBackups"/> is the retention policy
	/// read at this instant (a config edit hot-reloads into the next cut).
	/// </summary>
	internal WorldCutReport? Take(
		WorldCutTarget target,
		int frame,
		CharacterDataMsg? hostCharacter,
		IReadOnlyList<WorldTransientCount>? liveTransients,
		int keepBackups)
	{
		if (_armed is not { } reason)
		{
			return null;
		}

		var dropped = new List<string>();
		if (WorldCutWriter.KindOf(reason) != WorldCutKind.LayerEnd)
		{
			if (!TryCollectTransients(frame, target, reason, liveTransients, dropped, out var deferred, out var malformed))
			{
				// A malformed report is an owner bug, not a runtime condition: the cut
				// is refused (nothing is written) and the class is named, because a
				// snapshot whose in-flight state is unaccounted for is exactly what
				// §6 forbids.
				_armed = null;
				_deferral.Reset();
				return Publish(new WorldCutReport(WorldCutResult.Refused, reason, target.WorldId, malformed, dropped));
			}

			if (deferred is not null)
			{
				// Still waiting: the request stays armed for the next pump frame.
				return deferred;
			}
		}

		_armed = null;
		_deferral.Reset();
		return Publish(TryWrite(target, reason, hostCharacter, dropped, keepBackups));
	}

	/// <summary>
	/// Write the cut the kernel's own layer-advance commit asks for. No transient policy
	/// applies: a layer-end cut names a layer the restore REGENERATES, so it carries no
	/// in-layer fact and cannot lose one.
	/// </summary>
	internal WorldCutReport TakeLayerAdvance(WorldCutTarget target, int keepBackups) =>
		Publish(TryWrite(target, WorldCutReason.LayerAdvance, hostCharacter: null, dropped: [], keepBackups));

	/// <summary>
	/// Writes one cut, turning a throw into a refusal. Both callers run inside the game's
	/// frame pump — one from the kernel's own layer-advance commit, one from the frame-end
	/// seam — and a snapshot that cannot be written must not take the frame (or the run)
	/// down with it: the failure is named in the same report a refusal uses, so the
	/// trigger's answer is never missing.
	/// </summary>
	private WorldCutReport TryWrite(WorldCutTarget target, WorldCutReason reason, CharacterDataMsg? hostCharacter, IReadOnlyList<string> dropped, int keepBackups)
	{
		try
		{
			return Write(target, reason, hostCharacter, dropped, keepBackups);
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Cut {Reason} of world {WorldId} threw while writing.", reason, target.WorldId);
			return new WorldCutReport(WorldCutResult.Refused, reason, target.WorldId, $"the cut threw while writing ({ex.Message})", dropped);
		}
	}

	/// <summary>Write one cut and turn the writer's account into the trigger's report.</summary>
	private WorldCutReport Write(WorldCutTarget target, WorldCutReason reason, CharacterDataMsg? hostCharacter, IReadOnlyList<string> dropped, int keepBackups)
	{
		if (_session.Role == SessionRole.Guest)
		{
			_log.LogWarning("No cut taken ({Reason}): a guest never writes a world archive (decision 164).", reason);
			return new WorldCutReport(WorldCutResult.Refused, reason, target.WorldId, "a guest never writes a world archive", dropped);
		}

		if (_writer is null)
		{
			_log.LogWarning("No cut taken ({Reason}): this composition root has no world repository.", reason);
			return new WorldCutReport(WorldCutResult.Refused, reason, target.WorldId, "this composition root has no world repository", dropped);
		}

		if (target.WorldId.Length == 0)
		{
			_log.LogWarning("No cut taken ({Reason}): this host has no world for the current run (no run was started by the host).", reason);
			return new WorldCutReport(WorldCutResult.Refused, reason, target.WorldId, "this host has no CUO world for the current run", dropped);
		}

		var collected = _binder.Collect(hostCharacter);
		var write = _writer.Write(new WorldCutWriteRequest(
			target.WorldId,
			target.DisplayName,
			reason,
			WorldCutWriter.KindOf(reason),
			PhaseOf(reason),
			collected.Characters));

		// A character the cut could not carry is drawn from the same well as an
		// in-flight class it left behind: the player is told at the cut, never only
		// in the log (§6).
		var notCarried = new List<string>(dropped);
		notCarried.AddRange(collected.NotCarried);

		if (!write.Success)
		{
			return new WorldCutReport(WorldCutResult.Refused, reason, target.WorldId, write.Detail, notCarried);
		}

		// Only a COMMITTED cut restarts the interval: a refused or deferred one has not
		// written this world, and counting it would push the next autosave out by a full
		// interval for a world that was never saved.
		_autosave.NoteCutTaken(_utcNow());

		// §7's retention pass, at the one moment it can be exact: the transaction just
		// committed, so the archive set on disk is the one this cut belongs to. A failed
		// prune is reported and never un-commits the cut (the repository deletes
		// oldest-first and can never delete the newest).
		var prune = _writer.Prune(target.WorldId, keepBackups);

		var summary = $"world {target.WorldId} at revision {write.Revision}, layer {write.Layer} "
			+ $"({write.Files} file(s), {write.BlockRows} world-block row(s), {write.TransientRows} transient row(s), "
			+ $"backup {write.BackupPath}, {RetentionNote(prune)})";
		return new WorldCutReport(WorldCutResult.Captured, reason, target.WorldId, summary, notCarried);
	}

	/// <summary>The retention half of a cut's account: what the pass kept and what it could not delete.</summary>
	private static string RetentionNote(BackupPruneResult prune) =>
		prune.Clean
			? $"{prune.Kept.Count} archive(s) kept, {prune.Deleted.Count} pruned"
			: $"{prune.Kept.Count} archive(s) kept, {prune.Failures.Count} could not be pruned";

	/// <summary>The manifest's cut phase for a trigger (§4): the layer boundary and the frame-end seam are the two moments a cut is taken.</summary>
	internal static string PhaseOf(WorldCutReason reason) =>
		WorldCutWriter.KindOf(reason) == WorldCutKind.LayerEnd
			? WorldSaveService.LayerBoundaryCutPhase
			: WorldSaveService.FrameEndCutPhase;

	/// <summary>
	/// Applies the transient policy to the states reported at this instant. Returns
	/// false only for an undeclared class (<paramref name="malformed"/>);
	/// <paramref name="deferred"/> is a report that must be returned as-is, and
	/// <paramref name="dropped"/> collects the classes the cut will not carry. The
	/// observation and the naming are pure (<see cref="WorldCutTransients"/>); the
	/// WAIT's deadline is this class's, because it owns the armed request.
	/// </summary>
	private bool TryCollectTransients(
		int frame,
		WorldCutTarget target,
		WorldCutReason reason,
		IReadOnlyList<WorldTransientCount>? liveTransients,
		List<string> dropped,
		out WorldCutReport? deferred,
		out string malformed)
	{
		deferred = null;
		malformed = string.Empty;

		var observation = WorldCutTransients.Observe(_transients, liveTransients);
		if (WorldCutTransients.UndeclaredClass(observation) is { } undeclared)
		{
			malformed = $"an owner reported an undeclared transient class '{undeclared}'";
			_log.LogError("[Save] {Failure}: refusing the cut rather than writing a snapshot whose in-flight state is not accounted for.", malformed);
			return false;
		}

		var waiting = WorldCutTransients.WaitingFor(observation);
		if (waiting.Count > 0)
		{
			var waitingText = waiting.Select(WorldTransientPolicy.Describe).ToList();
			if (_deferral.ShouldWait(frame, out var firstFrame))
			{
				if (firstFrame)
				{
					_log.LogInformation("[Save] the armed cut waits for in-flight state to resolve: {Waiting}.", string.Join(", ", waitingText));
				}
				else
				{
					_log.LogDebug("[Save] the armed cut is still waiting at frame {Frame}: {Waiting}.", frame, string.Join(", ", waitingText));
				}

				deferred = new WorldCutReport(WorldCutResult.Deferred, reason, target.WorldId, "waiting for in-flight state", waitingText);
				return true;
			}

			// Deadlock guard: the deadline passed and the state is still there (a
			// stuck pending record). The cut goes on and NAMES what it could not take.
			_log.LogWarning("[Save] the armed cut waited {Frames} frame(s) for {Waiting} and took the cut without it.",
				frame - _deferral.StartedAtFrame, string.Join(", ", waitingText));
		}

		dropped.AddRange(WorldCutTransients.Dropped(observation, waiting, nativeReaderAvailable: _nativeReaderAvailable));
		return true;
	}

	/// <summary>
	/// Log one finished attempt and hand it to the surface that answers the player. A
	/// deferral is not a result: it is logged where it happens and never pushed to the
	/// console.
	/// </summary>
	private WorldCutReport Publish(WorldCutReport report)
	{
		if (report.Result != WorldCutResult.Deferred)
		{
			Reported?.Invoke(report);
		}

		return report;
	}
}
