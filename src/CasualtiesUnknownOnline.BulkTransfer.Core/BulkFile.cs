using System;

namespace CasualtiesUnknownOnline.BulkTransfer.Core;

/// <summary>
/// What a sender announces before any chunk: the file's identity, its exact length, its SHA-256 (lowercase hex)
/// and the chunk size it will slice with. Every field is data the RECEIVER can check a chunk against — the
/// length bounds the indices, the hash decides the whole file, and the id keeps two transfers apart.
/// </summary>
public sealed class BulkFile(string id, int length, string sha256, int chunkBytes)
{
	/// <summary>A transfer id chosen by the sender (the session distinguishes concurrent transfers by it).</summary>
	public string Id { get; } = id;

	/// <summary>The file's exact byte length.</summary>
	public int Length { get; } = length;

	/// <summary>The whole file's SHA-256 as lowercase hex.</summary>
	public string Sha256 { get; } = sha256;

	/// <summary>The chunk size the sender slices with (within <see cref="BulkTransferPolicy"/>).</summary>
	public int ChunkBytes { get; } = chunkBytes;

	/// <summary>How many chunks the file is cut into.</summary>
	public int ChunkCount => Length == 0 ? 0 : ((Length - 1) / ChunkBytes) + 1;

	/// <summary>The byte offset of a chunk's first byte.</summary>
	public int OffsetOf(int index) => index * ChunkBytes;

	/// <summary>The byte length of a chunk (the last one is short).</summary>
	public int LengthOf(int index) =>
		index < 0 || index >= ChunkCount ? 0 : Math.Min(ChunkBytes, Length - OffsetOf(index));
}
