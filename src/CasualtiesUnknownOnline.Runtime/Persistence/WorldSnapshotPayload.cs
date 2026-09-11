using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Everything one cut writes: the in-memory kernel checkpoint (the in-memory
/// shape), the character of every member present at the cut, the world facts the
/// kernel does not own (the block diff and the transient world facts), and the
/// provenance the manifest states. This is the in-memory/disc boundary — nothing
/// here knows how a domain file is spelled.
///
/// <see cref="Kind"/> is what decides whether the world facts are written at
/// all: a <see cref="WorldCutKind.LayerEnd"/> cut records no in-layer fact,
/// because the layer it names is regenerated from the run baseline (S2's
/// contract, and why a layer-end cut's two arrays are empty in every build).
/// </summary>
public sealed record WorldSnapshotPayload(
	GameCheckpoint Checkpoint,
	IReadOnlyList<SavedCharacter> Characters,
	string DisplayName,
	string SaveReason,
	string CutPhase,
	string GameBuild = "",
	string CuoBuild = "",
	string ContentFingerprint = "",
	IReadOnlyList<SaveWorldBlockRow>? WorldBlocks = null,
	IReadOnlyList<SaveWorldTransientRow>? WorldTransients = null,
	WorldCutKind Kind = WorldCutKind.LayerEnd);
