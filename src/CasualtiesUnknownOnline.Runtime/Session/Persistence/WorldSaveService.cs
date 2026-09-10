using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The save system's control point: which world this run writes into, when a cut
/// is taken, and what the Continue entry resolves to. It is the only writer of
/// the world repository (decision 164) and the only reader of the native save's
/// place (decision 165) — the game side never decides either.
///
/// The layer-end cut is taken from <see cref="ItemKernelAuthority.BatchCommitted"/>:
/// the kernel raises it AFTER the layer advance committed, so the snapshot holds
/// the run baseline of the layer being entered (its generation random state and
/// layer index), which is exactly what a restore has to replay. Taking it any
/// earlier would store the previous layer's baseline and regenerate a different
/// world.
/// </summary>
public sealed class WorldSaveService : IWorldSaveControl, IDisposable
{
	/// <summary>The manifest's cut phase for a cut taken at the layer boundary (§4).</summary>
	public const string LayerBoundaryCutPhase = "layer-boundary";

	/// <summary>The manifest's cut phase for the host's deliberate menu return (the game's Update pump, §4).</summary>
	public const string MenuReturnCutPhase = "menu-return";

	private readonly WorldRepository? _repository;
	private readonly ISessionControl _session;
	private readonly ICharacterDataControl _characters;
	private readonly ItemKernelAuthority _kernel;
	private readonly ITransportIdentity _transport;
	private readonly WorldSnapshotEncoder _encoder;
	private readonly ILoggerFactory _loggerFactory;
	private readonly ILogger<WorldSaveService> _log;
	private readonly string _gameBuild;
	private readonly Func<DateTime> _utcNow;

	private string _worldId = string.Empty;
	private string _displayName = string.Empty;
	private IReadOnlyList<SavedCharacter> _pendingCharacters = [];
	private bool _disposed;

	public WorldSaveService(
		WorldRepository? repository,
		ISessionControl session,
		ICharacterDataControl characters,
		ItemKernelAuthority kernel,
		ITransportIdentity transport,
		WorldSnapshotEncoder encoder,
		ILoggerFactory loggerFactory,
		ILogger<WorldSaveService> log,
		string? gameBuild = null,
		Func<DateTime>? utcNow = null)
	{
		_repository = repository;
		_session = session;
		_characters = characters;
		_kernel = kernel;
		_transport = transport;
		_encoder = encoder;
		_loggerFactory = loggerFactory;
		_log = log;
		_gameBuild = string.IsNullOrWhiteSpace(gameBuild) ? "unknown" : gameBuild!;
		_utcNow = utcNow ?? (() => DateTime.UtcNow);

		_kernel.BatchCommitted += OnBatchCommitted;
	}

	public bool IsEnabled => _repository is not null;

	public bool HasRestorableWorld => ContinueWorldId is not null;

	public string CurrentWorldId => _worldId;

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

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_kernel.BatchCommitted -= OnBatchCommitted;
	}

	public bool TryBeginRun()
	{
		if (_session.Role == SessionRole.Guest)
		{
			_log.LogWarning("A guest never writes a world archive; the host is the only save authority (decision 164).");
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
	/// The host is leaving the world on purpose — the one cut the player asks for
	/// explicitly. It is a layer-end-class cut: the kernel is at a committed
	/// revision and S2 has no in-layer diff, so the layer the runner re-enters is
	/// regenerated from the run baseline.
	/// </summary>
	public bool TryCaptureMenuReturnCut(CharacterDataMsg? hostCharacter) =>
		Capture(WorldCutReason.MenuReturn, MenuReturnCutPhase, hostCharacter);

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

		Capture(WorldCutReason.LayerAdvance, LayerBoundaryCutPhase, hostCharacter: null);
	}

	private bool Capture(WorldCutReason reason, string cutPhase, CharacterDataMsg? hostCharacter)
	{
		if (_session.Role == SessionRole.Guest)
		{
			_log.LogWarning("No cut taken ({Reason}): a guest never writes a world archive (decision 164).", reason);
			return false;
		}

		if (_repository is null)
		{
			_log.LogWarning("No cut taken ({Reason}): this composition root has no world repository.", reason);
			return false;
		}

		if (_worldId.Length == 0)
		{
			_log.LogWarning("No cut taken ({Reason}): this host has no world for the current run (no run was started by the host).", reason);
			return false;
		}

		var checkpoint = _kernel.CreateCheckpoint();
		if (checkpoint.Run is null)
		{
			_log.LogWarning("No cut taken ({Reason}): the kernel holds no run baseline yet.", reason);
			return false;
		}

		var characters = CollectCharacters(hostCharacter);
		var payload = new WorldSnapshotPayload(
			checkpoint,
			characters,
			_displayName,
			SaveArchiveFormat.CutReasonName(reason),
			cutPhase,
			_gameBuild,
			CuoBuild,
			ContentFingerprint: string.Empty);

		IReadOnlyList<SavePayloadFile> files;
		try
		{
			files = _encoder.Encode(payload);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
		{
			_log.LogError(ex, "No cut taken ({Reason}): the checkpoint could not be encoded.", reason);
			return false;
		}

		var request = new SaveWorldRequest
		{
			WorldId = _worldId,
			Kind = WorldCutKind.LayerEnd,
			Payload = files,
			Meta = MetaOf(checkpoint, characters.Count, reason, cutPhase),
			SavedAtUtc = _utcNow(),
		};

		var result = _repository.WriteSnapshot(_worldId, request);
		if (!result.Success)
		{
			_log.LogError("Cut {Reason} of world {WorldId} failed at {Step}: {Detail}", reason, _worldId, result.Reason, result.Detail);
			return false;
		}

		_log.LogInformation("Cut {Reason} committed for world {WorldId} at revision {Revision}, layer {Layer} ({Files} file(s), backup {Backup}).",
			reason, _worldId, checkpoint.GlobalRevision, checkpoint.Run.LayerIndex, files.Count, result.BackupArchivePath);
		return true;
	}

	public bool TryContinue(out WorldContinueOutcome outcome)
	{
		if (_repository is null)
		{
			outcome = WorldContinueOutcome.Refused(string.Empty, "this composition root has no world repository", EmptySalvage);
			return false;
		}

		var worldId = ContinueWorldId;
		if (worldId is null)
		{
			outcome = WorldContinueOutcome.Refused(string.Empty, "no CUO world exists to continue", EmptySalvage);
			return false;
		}

		// The restore is about to APPLY the payload, so it verifies the manifest's
		// digests — a listing would not (WorldLoadOptions).
		var options = new WorldLoadOptions { VerifyChecksums = true, RepairMode = true };
		var load = _repository.LoadSnapshot(worldId, options);
		if (load.Content is null)
		{
			_log.LogError("Continue refused for world {WorldId}: {Summary}", worldId, load.Summary);
			outcome = WorldContinueOutcome.Refused(worldId, load.Summary, new SalvageResult(load.Report));
			return false;
		}

		var decoder = new WorldSnapshotDecoder(load.Content.Manifest, _loggerFactory.CreateLogger<WorldSnapshotDecoder>());
		var (_, salvage) = _repository.ReadSalvage(load, decoder.DecodeEntry, options);
		var decode = decoder.Finish();
		if (decode.Checkpoint is null)
		{
			var refusal = $"{decode.Refusal}; {salvage.Report.Describe()}";
			_log.LogError("Continue refused for world {WorldId}: {Refusal}", worldId, refusal);
			outcome = WorldContinueOutcome.Refused(worldId, refusal, salvage);
			return false;
		}

		var restored = _kernel.Restore(decode.Checkpoint);
		if (!restored.Success)
		{
			var refusal = $"the kernel rejected the checkpoint: {restored.Error}";
			_log.LogError("Continue refused for world {WorldId}: {Refusal}", worldId, refusal);
			outcome = WorldContinueOutcome.Refused(worldId, refusal, salvage);
			return false;
		}

		// The run continues in the same world: every later cut of this session
		// writes back into it, and the picker's pointer follows the player.
		_worldId = worldId;
		_displayName = load.Content.Manifest.DisplayName;
		_pendingCharacters = decode.UsableCharacters;

		// The archive is authoritative for this world: the legacy reconnect table
		// (CasualtiesUnknownOnline.character-data.bin) is dropped before the archive's
		// characters are bound, so a player the package omits cannot be resurrected
		// from stale data (decision 162: absent from the package = new character).
		_characters.ClearSavedCharacters();
		ApplyCharacters(decode.UsableCharacters);
		_repository.SetLastOpenedWorld(worldId);

		// The summary is the account the caller logs (and S4's surface reads), so it
		// is built from the WHOLE report — a backup fallback is repository-scope
		// damage that a per-entry "clean" check would hide (§6: silent loss is
		// forbidden).
		var summary = salvage.Report.Entries.Count == 0
			? $"world {worldId} restored from {load.Content.SourcePath}"
			: $"world {worldId} restored with damage: {salvage.Report.Describe()}";
		_log.LogInformation("Continue restored world {WorldId} at revision {Revision} (layer {Layer}, {Players} stored character(s)): {Summary}",
			worldId, decode.Checkpoint.GlobalRevision, decode.Checkpoint.Run?.LayerIndex ?? -1, decode.UsableCharacters.Count, summary);
		outcome = new WorldContinueOutcome(true, worldId, summary, salvage);
		return true;
	}

	/// <summary>
	/// Binds the snapshot's characters back onto the peers that claim them (the
	/// host's own through the host slot, everyone else through the saved-character
	/// table the existing restore path reads). A key nobody claims is not an
	/// error — decision 162: that player joins as a NEW player.
	/// </summary>
	private void ApplyCharacters(IReadOnlyList<SavedCharacter> characters)
	{
		if (characters.Count == 0)
		{
			return;
		}

		var keys = characters.Select(character => character.PlayerKey).ToList();
		if (!PlayerKeyResolution.TrySpaceOfSet(keys, out var space))
		{
			_log.LogError("The snapshot's character files mix transport key spaces ({Keys}); no character was applied.", string.Join(", ", keys));
			return;
		}

		var live = LiveKeySpace();
		if (space == PlayerKeySpace.Unknown)
		{
			space = live;
		}
		else if (space != live)
		{
			// The two key spaces are separate (§2): a world written over IP-direct is
			// never claimed over Steam, even when a Steam persona happens to spell the
			// same name. Every key stays unclaimed — that player joins as a NEW
			// character (decision 162), and the files stay for a later claim.
			_log.LogInformation("The snapshot's key space {Stored} differs from the live transport {Live}; no stored character is claimed in this session.", space, live);
			return;
		}

		var peers = PresentPeers();
		var localPeerId = LocalPeerId;
		var applied = 0;
		var unclaimed = 0;
		foreach (var character in characters)
		{
			if (!PlayerKeyResolution.TryResolve(character.PlayerKey, space, peers, out var peerId))
			{
				unclaimed++;
				_log.LogInformation("Character {PlayerKey} has no claimant in this session; decision 162: that player joins as a new character. The file stays in the archive for a later claim.", character.PlayerKey);
				continue;
			}

			if (peerId == localPeerId)
			{
				_characters.SaveHostCharacterData(character.Character);
			}
			else
			{
				_characters.SaveCharacterData(peerId, character.Character);
			}

			applied++;
		}

		_log.LogInformation("Restored characters: {Applied} bound to present peers, {Unclaimed} left unclaimed (key space {Space}).", applied, unclaimed, space);
	}

	private List<SavedCharacter> CollectCharacters(CharacterDataMsg? hostCharacter)
	{
		var peers = PresentPeers();
		var space = LiveKeySpace();
		var localPeerId = LocalPeerId;
		var characters = new List<SavedCharacter>(peers.Count);
		foreach (var peer in peers)
		{
			var data = peer.PeerId == localPeerId
				? hostCharacter ?? _characters.GetHostCharacterData()
				: _characters.GetSavedCharacter(peer.PeerId);
			if (data is null)
			{
				_log.LogDebug("No character snapshot for peer {Peer} at this cut; it is not written (decisions 162/166: the save holds every member PRESENT at the cut).", peer.PeerId);
				continue;
			}

			characters.Add(new SavedCharacter(peer.KeyIn(space), data));
		}

		return characters;
	}

	/// <summary>
	/// Every peer whose character this cut can carry: the local player plus every
	/// handshaken member. Membership — not the "in world" flag — is the predicate:
	/// a layer boundary is a LOADING moment, where every peer's scene state reads
	/// as "not in the world". Whoever has no character snapshot is skipped by
	/// <see cref="CollectCharacters"/>, so a lobby-sitting member cannot add a file.
	/// </summary>
	private List<PlayerIdentity> PresentPeers()
	{
		var localPeerId = LocalPeerId;
		var peers = new List<PlayerIdentity> { new(localPeerId, _transport.LocalDisplayName) };
		foreach (var member in _session.Members)
		{
			if (!member.Handshaken || member.SteamId == localPeerId)
			{
				continue;
			}

			peers.Add(new PlayerIdentity(member.SteamId, member.DisplayName));
		}

		return peers;
	}

	/// <summary>The local peer's id — the session's when a session exists, the transport's otherwise (solo play has no session but still has an account).</summary>
	private ulong LocalPeerId => _session.LocalSteamId != 0 ? _session.LocalSteamId : _transport.LocalPeerId;

	/// <summary>The key space the LIVE transport writes in (§2).</summary>
	private PlayerKeySpace LiveKeySpace() => _transport.IsIpDirect ? PlayerKeySpace.IpDirect : PlayerKeySpace.Steam;

	private SaveManifestMeta MetaOf(GameCheckpoint checkpoint, int playerCount, WorldCutReason reason, string cutPhase)
	{
		var run = checkpoint.Run!;
		return new SaveManifestMeta
		{
			DisplayName = _displayName,
			GameBuild = _gameBuild,
			CuoBuild = CuoBuild,
			ProtocolVersion = ProtocolVersion.Current,
			// The content-set fingerprint belongs to the world-determinism layer,
			// which has no producer in this build; an empty value is "unknown",
			// never a guessed one (§6.1).
			ContentFingerprint = string.Empty,
			RunEpoch = checkpoint.RunEpoch.Value.ToString(CultureInfo.InvariantCulture),
			// The manifest stores the revision as a signed 64-bit number (§3.2); a
			// counter that outgrew it would silently wrap, so the cut is refused
			// instead (checked by the caller before anything is staged).
			GlobalRevision = checkpoint.GlobalRevision <= long.MaxValue
				? (long)checkpoint.GlobalRevision
				: throw new NotSupportedException($"revision {checkpoint.GlobalRevision} does not fit the manifest's revision field"),
			LayerIndex = run.LayerIndex,
			BiomeDepth = run.BiomeDepth,
			PlayerCount = playerCount,
			CutPhase = cutPhase,
			SaveReason = SaveArchiveFormat.CutReasonName(reason),
		};
	}

	/// <summary>An empty salvage for a refusal that never opened a snapshot.</summary>
	private static SalvageResult EmptySalvage => new(DamageReport.Empty);

	/// <summary>The build string every cut records: the Runtime's informational version (version + commit).</summary>
	private static string CuoBuild =>
		typeof(WorldSaveService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}
