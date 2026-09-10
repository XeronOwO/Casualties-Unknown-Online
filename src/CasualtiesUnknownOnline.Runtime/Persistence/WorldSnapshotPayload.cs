using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Everything one cut writes: the in-memory kernel checkpoint (the in-memory
/// shape), the character of every member present at the cut, and the provenance
/// the manifest states. This is the in-memory/disc boundary — nothing here knows
/// how a domain file is spelled.
/// </summary>
public sealed record WorldSnapshotPayload(
	GameCheckpoint Checkpoint,
	IReadOnlyList<SavedCharacter> Characters,
	string DisplayName,
	string SaveReason,
	string CutPhase,
	string GameBuild = "",
	string CuoBuild = "",
	string ContentFingerprint = "");
