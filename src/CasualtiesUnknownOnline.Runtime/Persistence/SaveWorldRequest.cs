using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One save: the world, the kind of cut, the payload files and the manifest
/// provenance. The caller hands over payloads only — staging, checksums,
/// archiving, rename ordering and crash leftovers are the writer's business.
/// </summary>
public sealed record SaveWorldRequest
{
	public string WorldId { get; init; } = string.Empty;

	public WorldCutKind Kind { get; init; }

	public IReadOnlyList<SavePayloadFile> Payload { get; init; } = [];

	public SaveManifestMeta Meta { get; init; } = new();

	/// <summary>The cut instant; also names the backup archive (§2).</summary>
	public DateTime SavedAtUtc { get; init; }
}
