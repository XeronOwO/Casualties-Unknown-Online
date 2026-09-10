using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The manifest is the only hard gate of the format (§3.2, §6): schema identity,
/// provenance, cut identity and the per-file integrity list. It is written last
/// in every transaction, so a snapshot whose manifest reads is a snapshot whose
/// payload files were all staged and verified.
/// </summary>
public sealed class SaveManifest
{
	/// <summary>The schema version this build reads and writes. A newer file is skipped, never guessed (§6.1).</summary>
	public const int CurrentSchemaVersion = 1;

	/// <summary>Constant <c>format</c> marker: a foreign JSON file must never be mistaken for an archive.</summary>
	public const string FormatMarker = "cuo-world-archive";

	public int SchemaVersion { get; init; } = CurrentSchemaVersion;

	public string Format { get; init; } = FormatMarker;

	// ---- provenance (§3.2) ----

	public string GameBuild { get; init; } = string.Empty;

	public string CuoBuild { get; init; } = string.Empty;

	public int ProtocolVersion { get; init; }

	/// <summary>The content-set fingerprint of the world-determinism layer; an entry whose content id is absent is salvaged per entry (§6).</summary>
	public string ContentFingerprint { get; init; } = string.Empty;

	// ---- cut identity (§3.2) ----

	public string WorldId { get; init; } = string.Empty;

	public string DisplayName { get; init; } = string.Empty;

	public WorldCutKind Kind { get; init; }

	public string RunEpoch { get; init; } = string.Empty;

	public long GlobalRevision { get; init; }

	public int LayerIndex { get; init; }

	public int BiomeDepth { get; init; }

	/// <summary>The host main-thread pump phase the cut was taken in (§4); finalised by S3.</summary>
	public string CutPhase { get; init; } = string.Empty;

	/// <summary>What triggered the cut: <c>layer-advance</c>, <c>menu-return</c>, <c>command</c>, <c>auto-interval</c>, <c>pre-restore-backup</c>.</summary>
	public string SaveReason { get; init; } = string.Empty;

	/// <summary>Formatted with <see cref="SaveArchiveFormat.FormatUtc"/> — sortable, timezone-free.</summary>
	public string SavedAtUtc { get; init; } = string.Empty;

	// ---- integrity (§3.2) ----

	public ChecksumPolicy ChecksumPolicy { get; init; } = ChecksumPolicy.Required;

	/// <summary>Every file in the snapshot, path-relative to the snapshot root, sorted by path. The manifest itself is not listed.</summary>
	public List<SaveManifestFile> Files { get; init; } = [];

	/// <summary>
	/// The property names the gate requires in the raw JSON: a DTO with defaults
	/// cannot tell a missing field from a default one, and a manifest that does not
	/// state what it holds is not a manifest (§6).
	/// </summary>
	internal static string[] RequiredProperties =>
		["format", "schemaVersion", "worldId", "kind", "savedAtUtc", "checksumPolicy", "files"];

	/// <summary>One snapshot file: canonical relative path, lowercase hex SHA-256 and byte length.</summary>
	public sealed record SaveManifestFile(string Path, string Sha256, long Bytes);
}
