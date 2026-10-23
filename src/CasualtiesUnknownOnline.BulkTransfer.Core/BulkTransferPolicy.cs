namespace CasualtiesUnknownOnline.BulkTransfer.Core;

/// <summary>
/// The transfer's rails: how big a chunk may be, how many may be in flight, and the floor between two sends.
/// The chunk ceiling is NOT this capability's own number — the mod packet channel's binary leaf carries at most
/// 32 KB (<c>ModValueCodec.MaxBinaryBytes</c>), so a chunk stops well short of it to leave room for the envelope
/// a mod adds around the payload.
/// </summary>
public static class BulkTransferPolicy
{
	/// <summary>The largest chunk this capability will produce or accept.</summary>
	public const int MaximumChunkBytes = 24 * 1024;

	/// <summary>The smallest chunk worth a round trip.</summary>
	public const int MinimumChunkBytes = 1024;

	/// <summary>The default chunk: big enough to keep a song to a few hundred frames, small enough for one packet.</summary>
	public const int DefaultChunkBytes = 16 * 1024;

	/// <summary>How many chunks a sender may have unacknowledged before it must wait (one window of the file).</summary>
	public const int DefaultWindow = 8;

	/// <summary>True when the chunk size is one this capability will use.</summary>
	public static bool IsUsableChunkSize(int bytes) => bytes is >= MinimumChunkBytes and <= MaximumChunkBytes;
}
