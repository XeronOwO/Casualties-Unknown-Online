namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One entry of a backup archive: the path relative to the snapshot root (no
/// trailing separator for directories) and, for files, the exact bytes a later
/// read uses to prove the archive round-trips.
/// </summary>
internal sealed record SaveArchiveEntry(string Path, byte[]? Content)
{
	/// <summary>A directory entry, preserved so an unpacked backup has the snapshot's shape (§2).</summary>
	internal static SaveArchiveEntry Directory(string path) => new(path, null);

	internal bool IsDirectory => Content is null;
}
