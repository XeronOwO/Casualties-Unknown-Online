namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One payload file of a snapshot: the canonical path inside the snapshot
/// (for example <c>characters/steam-76561198000000000.json</c>) and its exact
/// bytes. S1 callers are tests; S2/S3 hand in the domain encoders' output.
/// </summary>
public sealed class SavePayloadFile(string path, byte[] content)
{
	/// <summary>Snapshot-relative path; validated by the writer before anything is staged.</summary>
	public string Path { get; } = path;

	public byte[] Content { get; } = content;
}
