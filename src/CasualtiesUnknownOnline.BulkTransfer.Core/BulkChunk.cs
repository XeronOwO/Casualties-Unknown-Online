namespace CasualtiesUnknownOnline.BulkTransfer.Core;

/// <summary>One chunk on the wire: its index and the slice's bytes.</summary>
public sealed class BulkChunk(int index, byte[] bytes)
{
	/// <summary>The chunk's position in the file.</summary>
	public int Index { get; } = index;

	/// <summary>The slice itself (a copy is kept, so a caller reusing its buffer cannot corrupt the chunk).</summary>
	public byte[] Bytes { get; } = [.. bytes];
}
