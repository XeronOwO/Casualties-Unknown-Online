namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One file of an archive before its manifest is trusted: the canonical path and
/// the bytes read from the live folder or a backup entry. The manifest gate runs
/// on these, then they are re-issued as <see cref="SnapshotFile"/> with the
/// manifest entry that vouches for them.
/// </summary>
internal sealed record ArchiveFileEntry(string Path, byte[] Bytes);
