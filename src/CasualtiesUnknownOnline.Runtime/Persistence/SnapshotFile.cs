namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One file of an opened snapshot: its canonical path, its exact bytes and the
/// manifest entry that describes it (checksum, declared length). A replacement
/// payload for a salvage retry carries the manifest entry along so the retry is
/// still bound to the manifest's integrity statement.
/// </summary>
public sealed record SnapshotFile(string Path, byte[] Bytes, SaveManifest.SaveManifestFile ManifestEntry)
{
	/// <summary>Same path, different bytes — what a salvage retry hands back to <c>ReadSalvage</c>.</summary>
	public SnapshotFile WithBytes(byte[] bytes) => new(Path, bytes, ManifestEntry);
}
