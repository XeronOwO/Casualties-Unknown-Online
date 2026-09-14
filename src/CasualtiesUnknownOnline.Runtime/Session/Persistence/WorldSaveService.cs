using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The save system's control point: which world this run writes into, when a cut
/// is taken, and what the Continue entry resolves to. It is the only writer of
/// the world repository (decision 164) and the only reader of the native save's
/// place (decision 165) — the game side never decides either.
///
/// Two triggers, two seams:
///
/// - The layer-end cut is taken from <see cref="ItemKernelAuthority.BatchCommitted"/>:
///   the kernel raises it AFTER the layer advance committed, so the snapshot holds
///   the run baseline of the layer being entered (its generation random state and
///   layer index), which is exactly what a restore has to replay.
/// - Every other cut (the host's <c>/save</c> command, the deliberate menu return)
///   is ARMED here and taken by the adapter at the frame-end pump seam
///   (<see cref="TryCaptureArmedCut"/>), where no command batch and no frame flush
///   is in flight. A cut inside the console callback would read a half-applied
///   frame; arming it keeps the request out of that callback.
///
/// The cut is also where the transient policy is applied: an in-flight state the
/// policy resolves first defers the cut for a bounded number of frames
/// (<see cref="MaxCutDeferralFrames"/>) instead of losing it, and every state the
/// cut does not carry is named in the report (§6: no silent loss).
///
/// The writing half itself is <see cref="WorldCutWriter"/>: this class decides
/// WHETHER and WHERE, the writer decides WHAT the archive holds.
/// </summary>
public sealed class WorldSaveService : IWorldSaveControl, IDisposable
{
	/// <summary>The manifest's cut phase for a cut taken at the layer boundary (§4).</summary>
	public const string LayerBoundaryCutPhase = "layer-boundary";

	/// <summary>The manifest's cut phase for a cut taken at the host pump's frame-end seam (§4).</summary>
	public const string FrameEndCutPhase = "frame-end";

	/// <summary>
	/// How many pump frames an armed cut waits for <see cref="WorldTransientVerdict.ResolveBeforeSave"/>
	/// state. The longest such window is the trap drop hold (two frames), so eight
	/// frames is generous for the live path while keeping a STUCK pending state
	/// from starving the request: after the deadline the cut proceeds and names
	/// the state it could not take.
	/// </summary>
	public const int MaxCutDeferralFrames = 8;

	private readonly WorldRepository? _repository;
	private readonly ISessionControl _session;
	private readonly ItemKernelAuthority _kernel;
	private readonly ITransportIdentity _transport;
	private readonly IWorldFactSource _worldFacts;
	private readonly INativeWorldFacts? _nativeWorldFacts;
	private readonly WorldCutWriter? _writer;
	private readonly WorldCharacterBinder _binder;
	private readonly WorldRestoreApplier _restore;
	private readonly IWorldCutTransientProbe? _transients;
	private readonly WorldRestoreAudit? _audit;
	private readonly IRestoredWorldEntitySource? _worldEntities;
	private readonly IItemControl? _items;
	private readonly ILogger<WorldSaveService> _log;

	private string _worldId = string.Empty;
	private string _displayName = string.Empty;
	private IReadOnlyList<SavedCharacter> _pendingCharacters = [];
	private WorldCutReason? _armedReason;
	private int? _deferralStartFrame;
	private bool _disposed;

	public WorldSaveService(
		WorldRepository? repository,
		ISessionControl session,
		ICharacterDataControl characters,
		ItemKernelAuthority kernel,
		ITransportIdentity transport,
		WorldSnapshotEncoder encoder,
		IWorldFactSource worldFacts,
		ILoggerFactory loggerFactory,
		ILogger<WorldSaveService> log,
		string? gameBuild = null,
		Func<DateTime>? utcNow = null,
		INativeWorldFacts? nativeWorldFacts = null,
		IWorldCutTransientProbe? transients = null,
		WorldRestoreAudit? audit = null,
		IItemControl? items = null,
		IRestoredWorldEntitySource? worldEntities = null)
	{
		_repository = repository;
		_session = session;
		_kernel = kernel;
		_transport = transport;
		_worldFacts = worldFacts;
		_nativeWorldFacts = nativeWorldFacts;
		_transients = transients;
		_audit = audit;
		_worldEntities = worldEntities;
		_items = items;
		_binder = new WorldCharacterBinder(session, characters, transport, loggerFactory.CreateLogger<WorldCharacterBinder>());
		_restore = new WorldRestoreApplier(
			repository,
			kernel,
			characters,
			worldFacts,
			nativeWorldFacts,
			_binder,
			items,
			audit,
			// The restore's host-local kernel resets (a layer-end cut's enemy/fluid rows)
			// run as the LOCAL peer, resolved at call time — see
			// WorldRestoreApplier.DropReplacedLayerKernelTables.
			() => session.LocalSteamId,
			loggerFactory,
			loggerFactory.CreateLogger<WorldRestoreApplier>(),
			worldEntities);
		_writer = repository is null
			? null
			: new WorldCutWriter(
				repository,
				kernel,
				encoder,
				worldFacts,
				nativeWorldFacts,
				loggerFactory.CreateLogger<WorldCutWriter>(),
				gameBuild ?? string.Empty,
				utcNow ?? (() => DateTime.UtcNow));
		_log = log;

		_kernel.BatchCommitted += OnBatchCommitted;
	}

	public bool IsEnabled => _repository is not null;

	public bool HasRestorableWorld => ContinueWorldId is not null;

	public string CurrentWorldId => _worldId;

	public bool HasArmedCut => _armedReason is not null;

	public event Action<WorldCutReport>? CutReported;

	/// <summary>
	/// The world the Continue entry opens: the repository's last-opened pointer
	/// when it still names a world on disk, the newest world otherwise. There is
	/// no picker yet — choosing the world in-game is the management surface a
	/// later stage owns.
	/// </summary>
	public string? ContinueWorldId
	{
		get
		{
			if (_repository is null)
			{
				return null;
			}

			// Only a world that actually carries a snapshot is continuable: a folder
			// created by a run that never cut anything has nothing to restore, and
			// offering it would both fail the entry and hide the world behind it.
			var worlds = _repository.ListWorlds();
			var withSnapshot = worlds.Where(world => _repository.HasSnapshot(world.WorldId)).ToList();
			if (withSnapshot.Count == 0)
			{
				return null;
			}

			var lastOpened = _repository.LastOpenedWorldId;
			foreach (var world in withSnapshot)
			{
				if (string.Equals(world.WorldId, lastOpened, StringComparison.Ordinal))
				{
					return world.WorldId;
				}
			}

			return withSnapshot[0].WorldId;
		}
	}

	public IReadOnlyList<SavedCharacter> PendingCharacters => _pendingCharacters;

	/// <summary>
	/// The Continue attempt is dead: it applied a checkpoint but no world generation
	/// will consume it (no run baseline to publish). Every handover the click armed is
	/// released here — the account, the Runtime fact tables, the kernel's restored
	/// per-entity facts and the adapter's native handover — because the next run's own
	/// cancels are too late to protect the generation in between (an armed restore makes
	/// the world-entry seam SKIP the layer-boundary reset, then writes a dead attempt's
	/// facts into a world that is not the one it describes).
	/// </summary>
	public void AbandonRestore(string reason)
	{
		_log.LogWarning("The CUO continue attempt is abandoned: {Reason}. Every handover it armed is released and the live world keeps the state it already has.", reason);
		_audit?.AbandonRestore();
		_worldFacts.ClearPendingLiveReplay();
		_worldEntities?.CancelPendingRestore(reason);
		_nativeWorldFacts?.CancelPendingRestore();
		_items?.CancelRestoredWorldItems(reason);
	}

	/// <summary>
	/// The cut writer this service drives. Internal because the save suites pin the
	/// writer's row shapes (which facts a cut kind carries) directly — the same
	/// reason the capture methods used to be internal on this class.
	/// </summary>
	internal WorldCutWriter? Writer => _writer;

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_kernel.BatchCommitted -= OnBatchCommitted;
	}

	public bool TryBeginRun(bool isTutorial)
	{
		if (_session.Role == SessionRole.Guest)
		{
			_log.LogWarning("A guest never writes a world archive; the host is the only save authority (decision 164).");
			return false;
		}

		// A new run owns the next generation, whatever happens to the folder below:
		// a request armed for a previous world must never cut this one, and a restore
		// armed for a previous (refused or abandoned) attempt must never be written
		// into this world. Every half is cancelled here — BEFORE the folder is
		// created, so a repository that is missing or fails to create the folder cannot
		// leave the previous attempt's values armed for this run's first generation:
		// the armed cut, the Runtime fact tables, the kernel's restored per-entity
		// facts and the adapter's native handover (keypad codes, geyser liquid types,
		// the game's own damage rows, the run clock base and the recipe unlock table).
		_armedReason = null;
		_deferralStartFrame = null;
		_audit?.AbandonRestore();
		_worldFacts.ClearPendingLiveReplay();
		_worldEntities?.CancelPendingRestore("a new run superseded the restore");
		_nativeWorldFacts?.CancelPendingRestore();

		// A TUTORIAL run gets no archive: it is generated with
		// biomeOverride == Tutorial, the game's own save surface is disabled there
		// (WorldGeneration.cs:979 excludes the tutorial from the layer-end panel), and
		// an archive is what the Continue entry opens. The previous run's identity is
		// released here as well — leaving it set would aim a /save fired during the
		// tutorial at the PREVIOUS run's world and silently rewrite its snapshot.
		if (isTutorial)
		{
			_worldId = string.Empty;
			_displayName = string.Empty;
			_pendingCharacters = [];
			_log.LogInformation("This entry is the tutorial — it gets no world archive, and the previous run's identity is released.");
			return false;
		}

		if (_repository is null)
		{
			_log.LogWarning("This composition root has no world repository; the run will not be saved.");
			return false;
		}

		var displayName = string.IsNullOrWhiteSpace(_transport.LocalDisplayName) ? "World" : _transport.LocalDisplayName.Trim();
		var created = _repository.CreateWorld(displayName);
		if (!created.Success)
		{
			_log.LogError("Could not create the world folder for this run: {Detail}", created.Failure);
			_worldId = string.Empty;
			return false;
		}

		_worldId = created.WorldId;
		_displayName = displayName;
		_pendingCharacters = [];

		// The picker pointer moves on the FIRST CUT, not here: an aborted start (the
		// tutorial gate refuses after the click) must not hide the previous world
		// behind a folder that holds no snapshot — which would also make Continue
		// reachable for a world that cannot be opened.
		_log.LogInformation("This run writes into world {WorldId} ({DisplayName}) under {Root}.", _worldId, displayName, _repository.Root);
		return true;
	}

	/// <summary>
	/// Host: arm a cut for the next pump seam. The console command path — the cut
	/// itself never runs inside the console callback (a command batch must not
	/// interleave with it).
	/// </summary>
	public bool TryRequestCut(WorldCutReason reason, out string? refusal)
	{
		refusal = null;
		if (_session.Role == SessionRole.Guest)
		{
			refusal = "a guest never writes a world archive (the host is the only save authority)";
			return false;
		}

		if (_repository is null)
		{
			refusal = "this build has no CUO world repository";
			return false;
		}

		if (_worldId.Length == 0)
		{
			refusal = "this host has no CUO world for the current run yet";
			return false;
		}

		if (WorldCutWriter.KindOf(reason) == WorldCutKind.LayerEnd)
		{
			// A layer-end cut names a layer the restore REGENERATES, so it carries no
			// in-layer fact and its baseline must be read at the kernel's own
			// layer-advance commit. Arming one here would write a layer-end payload
			// with the frame-end phase — a snapshot whose kind and phase disagree.
			refusal = "a layer-end cut is taken by the kernel's own layer-advance commit, never at the frame-end seam";
			return false;
		}

		if (_armedReason == reason)
		{
			// Re-arming the SAME trigger: the menu-return seam retries every frame a
			// deferred cut waits, so this path must keep the wait it already spent and
			// stay quiet for the log's sake.
			_log.LogDebug("Cut {Reason} is still armed for world {WorldId}.", reason, _worldId);
			return true;
		}

		if (_armedReason is { } armed)
		{
			_log.LogInformation("The armed {Armed} cut is superseded by {Reason}; one cut is taken at the seam.", armed, reason);
		}

		// A DIFFERENT trigger starts its own wait (the same trigger kept the one above).
		_deferralStartFrame = null;
		_armedReason = reason;
		_log.LogInformation("Cut {Reason} armed for world {WorldId}: the pump takes it at the frame-end seam.", reason, _worldId);
		return true;
	}

	/// <summary>
	/// Host pump (the frame-end seam): take the armed cut. Null when nothing is
	/// armed. The payload is written by <see cref="WorldCutWriter"/> at the instant
	/// this call runs — the pump point where no command batch and no frame flush is
	/// in flight — and the transient policy decides whether to defer first.
	/// </summary>
	public WorldCutReport? TryCaptureArmedCut(
		CharacterDataMsg? hostCharacter,
		int frame,
		IReadOnlyList<WorldTransientCount>? liveTransients = null)
	{
		if (_armedReason is not { } reason)
		{
			return null;
		}

		var dropped = new List<string>();
		if (WorldCutWriter.KindOf(reason) != WorldCutKind.LayerEnd)
		{
			if (!TryCollectTransients(frame, liveTransients, dropped, out var deferred, out var malformed))
			{
				// A malformed report is an owner bug, not a runtime condition: the cut
				// is refused (nothing is written) and the class is named, because a
				// snapshot whose in-flight state is unaccounted for is exactly what
				// §6 forbids.
				_armedReason = null;
				_deferralStartFrame = null;
				return Publish(new WorldCutReport(WorldCutResult.Refused, reason, _worldId, malformed, dropped));
			}

			if (deferred is not null)
			{
				// Still waiting: the request stays armed for the next pump frame.
				return deferred;
			}
		}

		_armedReason = null;
		_deferralStartFrame = null;
		return Publish(TryWriteCut(reason, FrameEndCutPhase, hostCharacter, dropped));
	}

	/// <summary>
	/// Writes one cut, turning a throw into a refusal. Both callers run inside the
	/// game's frame pump — one from the kernel's own layer-advance commit, one from
	/// the frame-end seam — and a snapshot that cannot be written must not take the
	/// frame (or the run) down with it: the failure is named in the same report a
	/// refusal uses, so the trigger's answer is never missing.
	/// </summary>
	private WorldCutReport TryWriteCut(WorldCutReason reason, string cutPhase, CharacterDataMsg? hostCharacter, IReadOnlyList<string> dropped)
	{
		try
		{
			return WriteCut(reason, cutPhase, hostCharacter, dropped);
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Cut {Reason} of world {WorldId} threw while writing.", reason, _worldId);
			return new WorldCutReport(WorldCutResult.Refused, reason, _worldId, $"the cut threw while writing ({ex.Message})", dropped);
		}
	}

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
			_deferralStartFrame ??= frame;
			if (frame - _deferralStartFrame.Value < MaxCutDeferralFrames)
			{
				if (_deferralStartFrame == frame)
				{
					_log.LogInformation("[Save] the armed cut waits for in-flight state to resolve: {Waiting}.", string.Join(", ", waitingText));
				}
				else
				{
					_log.LogDebug("[Save] the armed cut is still waiting at frame {Frame}: {Waiting}.", frame, string.Join(", ", waitingText));
				}

				deferred = new WorldCutReport(WorldCutResult.Deferred, _armedReason!.Value, _worldId, "waiting for in-flight state", waitingText);
				return true;
			}

			// Deadlock guard: the deadline passed and the state is still there (a
			// stuck pending record). The cut goes on and NAMES what it could not take.
			_log.LogWarning("[Save] the armed cut waited {Frames} frame(s) for {Waiting} and took the cut without it.",
				frame - _deferralStartFrame.Value, string.Join(", ", waitingText));
		}

		dropped.AddRange(WorldCutTransients.Dropped(observation, waiting, nativeReaderAvailable: _nativeWorldFacts is not null));
		return true;
	}

	private void OnBatchCommitted(CommittedBatch batch)
	{
		if (_repository is null || _worldId.Length == 0 || _session.Role == SessionRole.Guest)
		{
			return;
		}

		if (!batch.Events.Any(@event => @event is RunAdvancedEvent))
		{
			return;
		}

		// The layer-end cut carries no in-layer fact, so the transient policy does
		// not apply to it at all: the layer it names is regenerated.
		Publish(TryWriteCut(WorldCutReason.LayerAdvance, LayerBoundaryCutPhase, hostCharacter: null, dropped: []));
	}

	/// <summary>Write one cut and turn the writer's account into the trigger's report.</summary>
	private WorldCutReport WriteCut(WorldCutReason reason, string cutPhase, CharacterDataMsg? hostCharacter, IReadOnlyList<string> dropped)
	{
		if (_session.Role == SessionRole.Guest)
		{
			_log.LogWarning("No cut taken ({Reason}): a guest never writes a world archive (decision 164).", reason);
			return new WorldCutReport(WorldCutResult.Refused, reason, _worldId, "a guest never writes a world archive", dropped);
		}

		if (_repository is null || _writer is null)
		{
			_log.LogWarning("No cut taken ({Reason}): this composition root has no world repository.", reason);
			return new WorldCutReport(WorldCutResult.Refused, reason, _worldId, "this composition root has no world repository", dropped);
		}

		if (_worldId.Length == 0)
		{
			_log.LogWarning("No cut taken ({Reason}): this host has no world for the current run (no run was started by the host).", reason);
			return new WorldCutReport(WorldCutResult.Refused, reason, _worldId, "this host has no CUO world for the current run", dropped);
		}

		var characters = _binder.Collect(hostCharacter);
		var write = _writer.Write(new WorldCutWriteRequest(
			_worldId,
			_displayName,
			reason,
			WorldCutWriter.KindOf(reason),
			cutPhase,
			characters));

		if (!write.Success)
		{
			return new WorldCutReport(WorldCutResult.Refused, reason, _worldId, write.Detail, dropped);
		}

		var summary = $"world {_worldId} at revision {write.Revision}, layer {write.Layer} "
			+ $"({write.Files} file(s), {write.BlockRows} world-block row(s), {write.TransientRows} transient row(s), backup {write.BackupPath})";
		return new WorldCutReport(WorldCutResult.Captured, reason, _worldId, summary, dropped);
	}

	/// <summary>Log one finished attempt and hand it to the surface that answers the player. A deferral is not a result: it is logged where it happens and never pushed to the console.</summary>
	private WorldCutReport Publish(WorldCutReport report)
	{
		if (report.Result != WorldCutResult.Deferred)
		{
			CutReported?.Invoke(report);
		}

		return report;
	}

	public bool TryContinue(out WorldContinueOutcome outcome)
	{
		// The restore half is its own object: it opens the archive and reports what it
		// produced. This class owns the CUT half, so the identity a restore produced is
		// adopted HERE and nowhere else — every later cut of this session writes back
		// into that world.
		var restore = _restore.TryApply(ContinueWorldId);
		if (restore.Started)
		{
			_worldId = restore.WorldId;
			_displayName = restore.DisplayName;
			_pendingCharacters = restore.Characters;
		}

		outcome = restore.Outcome;
		return restore.Started;
	}

	// The characters a cut carries and the peer arbitration of a restore belong to
	// the WorldCharacterBinder: identity is transport-scoped (decision 162) and
	// orthogonal to the archive, so S3.1's structure review split it out of this
	// class, which owns the cut itself.
}
