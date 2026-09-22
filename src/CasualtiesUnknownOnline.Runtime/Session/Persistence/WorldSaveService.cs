using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The save system's control point: which world this run writes into, whether a cut
/// may be taken at all, and what the Continue entry resolves to. It is the only
/// writer of the world repository (decision 164) and the only reader of the native
/// save's place (decision 165) — the game side never decides either.
///
/// Two triggers, two seams:
///
/// - The layer-end cut is taken from <see cref="ItemKernelAuthority.BatchCommitted"/>:
///   the kernel raises it AFTER the layer advance committed, so the snapshot holds
///   the run baseline of the layer being entered (its generation random state and
///   layer index), which is exactly what a restore has to replay.
/// - Every other cut (the host's <c>/save</c> command, the deliberate menu return,
///   the interval autosave) is ARMED here and taken by the adapter at the frame-end
///   pump seam (<see cref="TryCaptureArmedCut"/>), where no command batch and no frame
///   flush is in flight. A cut inside the console callback would read a half-applied
///   frame; arming it keeps the request out of that callback.
///
/// This class owns WHETHER and WHERE; the sequence between "armed" and "written" — the
/// bounded wait for in-flight state, the interval timer, the write and the backup
/// retention pass — belongs to <see cref="WorldCutTrigger"/>, and the writing half
/// itself to <see cref="WorldCutWriter"/>.
/// </summary>
public sealed class WorldSaveService : IWorldSaveControl, IDisposable, ISessionReset
{
	/// <summary>The manifest's cut phase for a cut taken at the layer boundary (§4).</summary>
	public const string LayerBoundaryCutPhase = "layer-boundary";

	/// <summary>The manifest's cut phase for a cut taken at the host pump's frame-end seam (§4).</summary>
	public const string FrameEndCutPhase = "frame-end";

	/// <summary>The save policy a composition with no options monitor runs with (the frozen defaults of §7).</summary>
	private static readonly SaveOptions FallbackOptions = new();

	private readonly WorldRepository? _repository;
	private readonly ISessionControl _session;
	private readonly ItemKernelAuthority _kernel;
	private readonly ITransportIdentity _transport;
	private readonly IWorldFactSource _worldFacts;
	private readonly INativeWorldFacts? _nativeWorldFacts;
	private readonly IOptionsMonitor<SaveOptions> _options;
	private readonly WorldRestoreAudit? _audit;
	private readonly WorldRestoreApplier _restore;
	private readonly WorldCutTrigger _trigger;
	private readonly IRestoredWorldEntitySource? _worldEntities;
	private readonly IRestoredWorldItemSource? _items;
	private readonly ILogger<WorldSaveService> _log;

	private string _worldId = string.Empty;
	private string _displayName = string.Empty;
	private IReadOnlyList<SavedCharacter> _pendingCharacters = [];
	private bool _disposed;

	/// <summary>
	/// The Continue attempt's player-facing account: what the click resolved, whether an
	/// applied attempt is still outstanding, and the one place a report is raised. Its own
	/// type because it is about what the PLAYER is told rather than about what a snapshot
	/// holds — and because this class sits at the architecture file-size ceiling.
	/// </summary>
	private readonly WorldRestoreAccountRelay _restoreAccount;

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
		IRestoredWorldItemSource? items = null,
		IRestoredWorldEntitySource? worldEntities = null,
		IOptionsMonitor<SaveOptions>? options = null)
	{
		_repository = repository;
		_session = session;
		_kernel = kernel;
		_transport = transport;
		_worldFacts = worldFacts;
		_nativeWorldFacts = nativeWorldFacts;
		_worldEntities = worldEntities;
		_items = items;
		_audit = audit;
		_options = options ?? new MutableOptionsMonitor<SaveOptions>(FallbackOptions);
		var binder = new WorldCharacterBinder(session, characters, transport, loggerFactory.CreateLogger<WorldCharacterBinder>());
		_restore = new WorldRestoreApplier(
			repository,
			kernel,
			characters,
			worldFacts,
			nativeWorldFacts,
			binder,
			items,
			audit,
			// The restore's host-local kernel resets (a layer-end cut's enemy/fluid rows)
			// run as the LOCAL peer, resolved at call time — see
			// WorldRestoreApplier.DropReplacedLayerKernelTables.
			() => session.LocalSteamId,
			loggerFactory,
			loggerFactory.CreateLogger<WorldRestoreApplier>(),
			worldEntities);
		var clock = utcNow ?? (() => DateTime.UtcNow);
		var writer = repository is null
			? null
			: new WorldCutWriter(
				repository,
				kernel,
				encoder,
				worldFacts,
				nativeWorldFacts,
				loggerFactory.CreateLogger<WorldCutWriter>(),
				gameBuild ?? string.Empty,
				clock);
		_trigger = new WorldCutTrigger(
			session,
			writer,
			binder,
			transients,
			nativeReaderAvailable: nativeWorldFacts is not null,
			loggerFactory.CreateLogger<WorldCutTrigger>(),
			clock);
		_trigger.Reported += report => CutReported?.Invoke(report);
		_log = log;
		_restoreAccount = new WorldRestoreAccountRelay(report => RestoreReported?.Invoke(report), audit);

		_kernel.BatchCommitted += OnBatchCommitted;
		_session.SessionEnded += ResetSessionState;
	}

	public bool IsEnabled => _repository is not null;

	public bool HasRestorableWorld => ContinueWorldId is not null;

	public string CurrentWorldId => _worldId;

	public bool HasArmedCut => _trigger.HasArmed;

	public event Action<WorldCutReport>? CutReported;

	/// <summary>The Continue click's own account, raised once the attempt resolves (see <see cref="IWorldSaveControl.RestoreReported"/>).</summary>
	public event Action<WorldRestoreReport>? RestoreReported;

	/// <summary>
	/// The world the Continue entry opens: the repository's last-opened pointer when it
	/// still names a world that carries a snapshot, the first such world otherwise. The
	/// Worlds page is the picker that moves that pointer now
	/// (<see cref="IWorldLibrary.TrySelectWorld"/>), and it reads THIS property to mark
	/// its row rather than re-deriving the rule — so the page and the native Load button
	/// cannot disagree about which world is about to open.
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

	/// <summary>The save policy in force right now — read at the instant of each decision, so a config edit hot-reloads without a restart (decision 25).</summary>
	private SaveOptions Options => _options.CurrentValue;

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

		// The click already reported "applied" (the absence of a click reported nothing at
		// all, and that absence is why the relay, not this method, decides whether there is
		// an attempt to close); this is the second and last word on it, and the player needs
		// it for the same reason a refusal needs a line: the run does not start, and without
		// this the console would say nothing about the button that just did nothing.
		_restoreAccount.Abandoned(reason);

		_audit?.AbandonRestore();
		_worldFacts.ClearPendingLiveReplay();
		_worldEntities?.CancelPendingRestore(reason);
		_nativeWorldFacts?.CancelPendingRestore();
		_items?.CancelRestoredWorldItems(reason);
	}

	/// <summary>
	/// The session is gone, and with it the world-entry seam that would have written
	/// whatever this restore still owed. Every arm is released by its own owner as the
	/// session tears down (the world-fact tables by <see cref="WorldService"/>, the
	/// adapter's native handover by the adapter's session binding, the world-entity
	/// facts by <see cref="WorldEntityKernelProjection"/>, the restored items by
	/// <see cref="ItemService"/>), and each of those releases accounts for its own half
	/// — EXCEPT the world-fact half, whose two owners cannot attribute it: the Runtime
	/// tables carry the attempt, the adapter's native handover carries none, and this is
	/// the one place holding both. The half is therefore contributed here, stamped with
	/// the attempt the tables were applied by; the account drops the contribution when
	/// that is not the attempt it is open for, or when the half already reported at the
	/// seam (which is why contributing unconditionally is safe).
	/// </summary>
	public void ResetSessionState() =>
		_audit?.LiveWriteAbandoned(
			WorldRestoreHalf.WorldFacts,
			_worldFacts.AppliedRestoreSequence,
			"the session ended before the world-entry seam wrote the restored world facts");

	/// <summary>
	/// The cut writer this service drives. Internal because the save suites pin the
	/// writer's row shapes (which facts a cut kind carries) directly — the same
	/// reason the capture methods used to be internal on this class.
	/// </summary>
	internal WorldCutWriter? Writer => _trigger.Writer;

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_kernel.BatchCommitted -= OnBatchCommitted;
		_session.SessionEnded -= ResetSessionState;
		_restoreAccount.Dispose();

		// A clean shutdown refreshes no lease: releasing it here is what keeps the next
		// instance from waiting out the staleness window for a writer that is provably
		// gone. A lease another process took over meanwhile is left alone.
		if (_worldId.Length > 0)
		{
			_repository?.ReleaseWorld(_worldId);
		}
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
		// the armed cut, the interval clock, the Runtime fact tables, the kernel's
		// restored per-entity facts and the adapter's native handover (keypad codes,
		// geyser liquid types, the game's own damage rows, the run clock base and the
		// recipe unlock table).
		_trigger.StandDown();
		_restoreAccount.Superseded();
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

		// The interval autosave counts from HERE, not from the process start: the first
		// autosave of a run lands one interval after the run began, never on its first
		// frame (WorldAutosaveInterval).
		_trigger.RestartInterval();

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

		_trigger.Arm(reason, _worldId);
		return true;
	}

	/// <summary>
	/// The pump's interval tick, run every frame BEFORE the armed cut is taken: arm the
	/// interval autosave when the configured interval has elapsed since this world was
	/// last written. <paramref name="inWorld"/> is the adapter's own answer to "is a
	/// world loaded right now", and it is load-bearing: a host that returned to the main
	/// menu still owns a world folder, and cutting it every interval would churn the
	/// archive set (and prune the archives of the run that IS being played) for as long
	/// as the menu stays open.
	/// </summary>
	public bool TryArmIntervalAutosave(bool inWorld)
	{
		if (!inWorld || _session.Role == SessionRole.Guest || _repository is null || _worldId.Length == 0)
		{
			return false;
		}

		// A cut is already armed, and it will write this world at the next seam: arming
		// the interval on top of it would supersede the player's own trigger and answer
		// it with nothing (an autosave is not player-initiated), so the player's /save
		// would silently become an autosave.
		if (_trigger.HasArmed)
		{
			return false;
		}

		var options = Options;
		return _trigger.IsAutosaveDue(options.AutosaveEnabled, options.AutosaveInterval)
			&& TryRequestCut(WorldCutReason.AutoInterval, out _);
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
		IReadOnlyList<WorldTransientCount>? liveTransients = null) =>
		_trigger.Take(Target, frame, hostCharacter, liveTransients, Options.Retention);

	/// <summary>Which world a cut of this instant writes into (the identity a restore may have adopted a moment ago).</summary>
	private WorldCutTarget Target => new(_worldId, _displayName);

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
		_trigger.TakeLayerAdvance(Target, Options.Retention);
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

			// A restored world starts its own write history: the first interval autosave
			// lands one interval after the click, not one interval after the last cut of
			// the session that preceded it.
			_trigger.RestartInterval();
		}

		outcome = restore.Outcome;

		// The click account is a REPORT, not a log line (§6): the console renders these
		// same lines where the player is already reading, so a refused continue — and a
		// continue that skipped an entry, refused a stored character or fell back to a
		// backup — is never discoverable only by reading a file. Raised for the refusals
		// too, which is the half a player otherwise experiences as "nothing happened".
		// The account itself, disposition included, is the restore's own (Result.Report).
		_restoreAccount.Resolved(restore.Report);
		return restore.Started;
	}

	// The characters a cut carries and the peer arbitration of a restore belong to
	// the WorldCharacterBinder: identity is transport-scoped (decision 162) and
	// orthogonal to the archive, so S3.1's structure review split it out of this
	// class, which owns the cut itself.
}
