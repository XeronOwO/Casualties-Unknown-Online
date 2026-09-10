namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// What <see cref="WorldRepository.CreateWorld"/> produced: the immutable folder
/// key, the world's folder and its first metadata record. The world id is
/// generated once (<c>w-&lt;yyyyMMdd&gt;-&lt;4 hex&gt;</c>) and is the directory name for
/// the rest of the world's life; a rename never touches it.
/// </summary>
public sealed record WorldCreateResult(
	bool Success,
	string WorldId,
	string WorldDirectory,
	WorldMetadata? Metadata,
	string Failure)
{
	internal static WorldCreateResult Created(string worldId, string worldDirectory, WorldMetadata metadata) =>
		new(true, worldId, worldDirectory, metadata, string.Empty);

	internal static WorldCreateResult Failed(string failure) =>
		new(false, string.Empty, string.Empty, null, failure);
}
