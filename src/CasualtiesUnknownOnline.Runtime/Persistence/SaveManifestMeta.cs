namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The provenance and cut fields a caller must supply for a write; the writer
/// fills everything else (schema identity, integrity, timestamp). Kept separate
/// from <see cref="SaveManifest"/> so no caller can accidentally hand the writer
/// a stale file list or a forged checksum.
/// </summary>
public sealed class SaveManifestMeta
{
	/// <summary>The world's player-facing name at the cut (§3.2); renameable, unlike the world id.</summary>
	public string DisplayName { get; init; } = string.Empty;

	public string GameBuild { get; init; } = string.Empty;

	public string CuoBuild { get; init; } = string.Empty;

	public int ProtocolVersion { get; init; }

	public string ContentFingerprint { get; init; } = string.Empty;

	public string RunEpoch { get; init; } = string.Empty;

	public long GlobalRevision { get; init; }

	public int LayerIndex { get; init; }

	public int BiomeDepth { get; init; }

	/// <summary>How many players are in the world at this cut — what <c>world.json</c> reports as <c>playerCount</c> (§3.3).</summary>
	public int PlayerCount { get; init; }

	public string CutPhase { get; init; } = string.Empty;

	public string SaveReason { get; init; } = string.Empty;

	/// <summary>
	/// The same provenance with the display name replaced. The repository uses this so a
	/// cut's manifest always carries the world's current name, whatever a caller passed —
	/// the manifest's cut identity and <c>world.json</c> must not disagree.
	/// </summary>
	internal SaveManifestMeta WithDisplayName(string displayName) => new()
	{
		DisplayName = displayName,
		GameBuild = GameBuild,
		CuoBuild = CuoBuild,
		ProtocolVersion = ProtocolVersion,
		ContentFingerprint = ContentFingerprint,
		RunEpoch = RunEpoch,
		GlobalRevision = GlobalRevision,
		LayerIndex = LayerIndex,
		BiomeDepth = BiomeDepth,
		PlayerCount = PlayerCount,
		CutPhase = CutPhase,
		SaveReason = SaveReason,
	};
}
