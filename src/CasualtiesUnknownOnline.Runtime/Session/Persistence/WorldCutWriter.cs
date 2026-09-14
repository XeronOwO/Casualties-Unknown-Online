using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// Writes ONE cut of the live world into the run's world folder: freezes the
/// kernel revision, reads the world fact tables, encodes the payload files and
/// hands the transaction to the repository.
///
/// Split out of <see cref="WorldSaveService"/> — which owns WHICH world a run
/// writes into and WHEN a cut is taken — because the two halves change for
/// different reasons: this one follows the archive's payload contract (which
/// facts a kind carries), the service follows the trigger and the transient
/// policy. The service passes the characters it collected (its binder owns the
/// transport-scoped identity) and the kind it decided, so the phase/kind
/// decision stays in one place.
/// </summary>
internal sealed class WorldCutWriter(
	WorldRepository repository,
	ItemKernelAuthority kernel,
	WorldSnapshotEncoder encoder,
	IWorldFactSource worldFacts,
	INativeWorldFacts? nativeWorldFacts,
	ILogger<WorldCutWriter> log,
	string gameBuild,
	Func<DateTime> utcNow)
{
	private readonly WorldRepository _repository = repository;
	private readonly ItemKernelAuthority _kernel = kernel;
	private readonly WorldSnapshotEncoder _encoder = encoder;
	private readonly IWorldFactSource _worldFacts = worldFacts;
	private readonly INativeWorldFacts? _nativeWorldFacts = nativeWorldFacts;
	private readonly ILogger<WorldCutWriter> _log = log;
	private readonly string _gameBuild = string.IsNullOrWhiteSpace(gameBuild) ? "unknown" : gameBuild!;
	private readonly Func<DateTime> _utcNow = utcNow;

	/// <summary>
	/// The cut kind a trigger produces. A layer advance is the one trigger that
	/// names a layer the world regenerates, so it is the only layer-end cut; every
	/// other trigger cuts into a live layer and carries its in-layer facts.
	/// </summary>
	internal static WorldCutKind KindOf(WorldCutReason reason) => reason switch
	{
		WorldCutReason.LayerAdvance => WorldCutKind.LayerEnd,
		_ => WorldCutKind.MidRun,
	};

	/// <summary>Freezes the revision, gathers the facts and commits the snapshot. A refusal names the step that refused; nothing partial is written.</summary>
	internal WorldCutWriteResult Write(WorldCutWriteRequest request)
	{
		// The revision is frozen BEFORE any table is read: everything this cut
		// carries comes from the one instant the caller timed (the kernel commits
		// synchronously, so a checkpoint cannot straddle a batch).
		var checkpoint = _kernel.CreateCheckpoint();
		if (checkpoint.Run is null)
		{
			_log.LogWarning("No cut taken ({Reason}): the kernel holds no run baseline yet.", request.Reason);
			return WorldCutWriteResult.Refused("the kernel holds no run baseline yet");
		}

		var facts = CaptureWorldFacts(request.Reason, request.Kind);
		if (facts.Failure is not null)
		{
			// A native table that cannot be read is NOT an empty table: writing the
			// cut anyway would store a world whose partial damage silently vanished
			// (the game's own list holds damage no CUO hook ever saw).
			_log.LogError("No cut taken ({Reason}): {Failure}.", request.Reason, facts.Failure);
			return WorldCutWriteResult.Refused(facts.Failure);
		}

		IReadOnlyList<SavePayloadFile> files;
		WorldSnapshotCounts counts;
		SaveManifestMeta meta;
		try
		{
			var payload = new WorldSnapshotPayload(
				checkpoint,
				request.Characters,
				request.DisplayName,
				SaveArchiveFormat.CutReasonName(request.Reason),
				request.CutPhase,
				_gameBuild,
				CuoBuild,
				ContentFingerprint: string.Empty,
				facts.Blocks,
				facts.Transients,
				request.Kind,
				facts.RunFields);
			var encoded = _encoder.EncodeWithCounts(payload);
			files = encoded.Files;
			counts = encoded.Counts;
			meta = MetaOf(checkpoint, request.DisplayName, request.Characters.Count, request.Reason, request.CutPhase);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
		{
			_log.LogError(ex, "No cut taken ({Reason}): the checkpoint could not be encoded.", request.Reason);
			return WorldCutWriteResult.Refused($"the checkpoint could not be encoded ({ex.Message})");
		}

		var write = _repository.WriteSnapshot(request.WorldId, new SaveWorldRequest
		{
			WorldId = request.WorldId,
			// The manifest's kind and the payload's kind are ONE decision: the
			// manifest names what the snapshot holds, so deriving them separately
			// would let a mid-run payload announce itself as a layer-end cut (and a
			// restore would then apply in-layer facts to a layer it regenerates).
			Kind = request.Kind,
			Payload = files,
			Meta = meta,
			SavedAtUtc = _utcNow(),
		});

		if (!write.Success)
		{
			_log.LogError("Cut {Reason} of world {WorldId} failed at {Step}: {Detail}",
				request.Reason, request.WorldId, write.Reason, write.Detail);
			return WorldCutWriteResult.Refused($"the write transaction failed at {write.Reason} ({write.Detail})");
		}

		// ONE line per cut (S4 scope 3): the trigger, the kind and phase the manifest
		// recorded, the revision and layer the snapshot was frozen at, and the
		// per-domain counts — the same domains the restore's own line reports, in the
		// same order (WorldSnapshotCounts), so "the restored world is missing rows"
		// is a two-line comparison instead of a deploy-and-reproduce loop. The counts
		// are the ENCODER's, i.e. the rows the archive holds: a layer-end cut drops its
		// in-layer rows, so the checkpoint's own counts would name a set nobody can read.
		_log.LogInformation(
			"Cut {Reason} committed for world {WorldId}: {Kind} cut taken at {CutPhase}, revision {Revision}, layer {Layer}, {Files} file(s), backup {Backup}; {Counts}.",
			request.Reason, request.WorldId, SaveArchiveFormat.CutKindName(request.Kind), request.CutPhase, checkpoint.GlobalRevision,
			checkpoint.Run.LayerIndex, files.Count, write.BackupArchivePath, counts.Describe());
		// The result carries the SAME counts the line above reports, from the same source:
		// the account a console prints and the line a log keeps must not be able to name
		// different numbers for one cut.
		return WorldCutWriteResult.Captured(
			checkpoint.GlobalRevision, checkpoint.Run.LayerIndex, files.Count,
			counts.WorldBlocks, counts.WorldTransients, write.BackupArchivePath ?? string.Empty);
	}

	/// <summary>
	/// The world facts one cut carries. A layer-end cut carries no IN-LAYER fact BY
	/// DESIGN — the layer it names is regenerated from the run baseline, so its two
	/// in-layer files stay empty (§4) — but it carries the native run fields like
	/// every other cut, because the run clock and the recipe unlocks are properties
	/// of the run, not of the layer.
	/// </summary>
	internal WorldSaveFacts CaptureWorldFacts(WorldCutReason reason, WorldCutKind cutKind)
	{
		var facts = cutKind == WorldCutKind.LayerEnd ? WorldSaveFacts.None : CaptureMidRunFacts(reason, cutKind);
		if (facts.Failure is not null)
		{
			return facts;
		}

		var (runFields, failure) = CaptureRunFields(reason);
		return failure is not null ? WorldSaveFacts.Unreadable(failure) : facts with { RunFields = runFields };
	}

	/// <summary>
	/// The native run values a cut records: the run clock base and the recipe
	/// unlock table. Read at EVERY cut kind — see <see cref="CaptureWorldFacts"/> —
	/// and read through its own port call so a layer-end cut never touches the
	/// keypad table, whose capture ROLLS the codes the game has not decided yet.
	///
	/// A reader that cannot read them REFUSES the cut: a snapshot that reads back as
	/// "clock zero, nothing unlocked" is worse than no snapshot, because the player
	/// continues from it believing the world was recorded. A composition with no
	/// native reader at all is a different case — nothing could have been read — and
	/// is NAMED in the log and again by the restore that finds no row.
	/// </summary>
	private (NativeRunFields? Fields, string? Failure) CaptureRunFields(WorldCutReason reason)
	{
		if (_nativeWorldFacts is null)
		{
			// The rarity multipliers are still in this snapshot — they ride the kernel
			// run baseline — so only the two values no CUO domain owns go missing, and
			// the restore that finds no row names them.
			_log.LogWarning(
				"Cut {Reason} carries no native run fields: no INativeWorldFacts is registered, so the run clock base and the recipe unlock table are not in this snapshot (the rarity multipliers still ride the run baseline).",
				reason);
			return (null, null);
		}

		var capture = _nativeWorldFacts.CaptureRunFields();
		if (capture.Failure is not null)
		{
			_log.LogError("No cut taken ({Reason}): {Failure}.", reason, capture.Failure);
			return (null, capture.Failure);
		}

		return (capture, null);
	}

	/// <summary>
	/// The mid-run capture: the Runtime fact tables through
	/// <see cref="IWorldFactSource"/> and, when the adapter registered one, the
	/// native tables through <see cref="INativeWorldFacts"/>. A native table that
	/// cannot be read REFUSES the cut — the caller writes nothing rather than a
	/// snapshot with a silently empty damage list.
	/// </summary>
	internal WorldSaveFacts CaptureMidRunFacts(WorldCutReason reason, WorldCutKind cutKind)
	{
		var blocks = new List<SaveWorldBlockRow>();
		foreach (var state in _worldFacts.CaptureBlockStates())
		{
			blocks.Add(SaveWorldBlockRow.OfBlockState(state.X, state.Y, state.Block));
		}

		var transients = new List<SaveWorldTransientRow>();

		// The Runtime half of the transient set: the radiation line is host world
		// state every peer is already aligned to over the wire.
		if (_worldFacts.CaptureRadiationLine() is { } radiation)
		{
			transients.Add(SaveWorldTransientRow.OfRadiationLine(radiation));
		}

		if (_nativeWorldFacts is null)
		{
			// No reader at all: the decided native values (keypad codes, geyser
			// liquid types) and the partial block damage cannot be carried — a
			// restored world re-rolls the first two and LOSES the third. Named,
			// never silent.
			_log.LogWarning(
				"Cut {Reason} carries no native world fact: no INativeWorldFacts is registered, so keypad codes, geyser liquid types and the game's own block-damage table are not in this snapshot.",
				reason);
		}
		else
		{
			// ONE read of all three native tables: the cut's consistency is the
			// whole point, and the game's list is the ONLY partial-damage table CUO
			// has (the copy that used to sit beside it was deleted).
			var capture = _nativeWorldFacts.Capture();
			if (capture.Failure is not null)
			{
				return WorldSaveFacts.Unreadable(capture.Failure);
			}

			foreach (var damage in capture.BlockDamages)
			{
				blocks.Add(SaveWorldBlockRow.OfNativeBlockDamage(damage.X, damage.Y, damage.Damage));
			}

			// These are DECIDED values (§4): carrying them is the whole point,
			// because regenerating the layer would re-roll them and a code the
			// player already read would stop opening the door.
			foreach (var code in capture.Keypads)
			{
				transients.Add(SaveWorldTransientRow.OfKeypad(code));
			}

			foreach (var geyser in capture.Geysers)
			{
				transients.Add(SaveWorldTransientRow.OfGeyser(geyser));
			}
		}

		// Debug, not Information: the commit line of this same cut reports these two
		// counts (with the rest of the domains), and a second Information line for one
		// cut is the noise that hides the line a reader is looking for.
		_log.LogDebug("Cut {Reason} ({Kind}) carries {Blocks} world-block row(s) and {Transients} transient row(s).",
			reason, cutKind, blocks.Count, transients.Count);
		return new WorldSaveFacts(blocks, transients);
	}

	private SaveManifestMeta MetaOf(GameCheckpoint checkpoint, string displayName, int playerCount, WorldCutReason reason, string cutPhase)
	{
		var run = checkpoint.Run!;
		return new SaveManifestMeta
		{
			DisplayName = displayName,
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
			// instead.
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

	private static string CuoBuild =>
		typeof(WorldCutWriter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}
