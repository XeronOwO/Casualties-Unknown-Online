using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace CasualtiesUnknownOnline.BulkTransfer.Core;

/// <summary>
/// The sending half: the whole file, the announced identity, and which chunks are still outstanding. It does not
/// send anything — a caller asks <see cref="Next"/> for a chunk, sends it on its own channel, and reports the
/// acknowledgement with <see cref="Acknowledged"/>; a stalled or lost chunk simply stays outstanding and is
/// offered again, which is the whole of the resume story (no separate resume handshake to get wrong).
/// </summary>
public sealed class BulkSender
{
	private readonly byte[] _file;
	private readonly HashSet<int> _outstanding = [];
	private readonly HashSet<int> _acknowledged = [];
	private int _cursor;

	/// <summary>Slice <paramref name="file"/> into a transfer with its own identity and hash.</summary>
	public BulkSender(string id, byte[] file, int chunkBytes = BulkTransferPolicy.DefaultChunkBytes)
	{
		if (file is null)
		{
			throw new ArgumentNullException(nameof(file));
		}

		if (!BulkTransferPolicy.IsUsableChunkSize(chunkBytes))
		{
			throw new ArgumentOutOfRangeException(nameof(chunkBytes), chunkBytes, "the chunk size is outside the capability's rails");
		}

		_file = [.. file];
		File = new BulkFile(id, _file.Length, Hash(_file), chunkBytes);
	}

	/// <summary>The announcement: identity, length, hash and chunk size.</summary>
	public BulkFile File { get; }

	/// <summary>True when every chunk has been acknowledged once.</summary>
	public bool Complete => _acknowledged.Count == File.ChunkCount;

	/// <summary>How many chunks are sent but not yet acknowledged.</summary>
	public int Outstanding => _outstanding.Count;

	/// <summary>
	/// The next chunk to send, or null when the window is full or everything is acknowledged. A chunk whose
	/// acknowledgement never arrives is offered again once the cursor wraps.
	/// </summary>
	public BulkChunk? Next(int window = BulkTransferPolicy.DefaultWindow)
	{
		if (window < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(window), window, "the window must allow at least one chunk in flight");
		}

		if (_outstanding.Count >= window || Complete)
		{
			return null;
		}

		for (var step = 0; step < File.ChunkCount; step++)
		{
			var index = (_cursor + step) % File.ChunkCount;
			if (_acknowledged.Contains(index) || _outstanding.Contains(index))
			{
				continue;
			}

			_outstanding.Add(index);
			_cursor = (index + 1) % File.ChunkCount;
			var slice = new byte[File.LengthOf(index)];
			Buffer.BlockCopy(_file, File.OffsetOf(index), slice, 0, slice.Length);
			return new BulkChunk(index, slice);
		}

		return null;
	}

	/// <summary>Record an acknowledgement; an unknown index is refused rather than trusted.</summary>
	public bool Acknowledged(int index)
	{
		if (!_outstanding.Remove(index))
		{
			return false;
		}

		_acknowledged.Add(index);
		return true;
	}

	/// <summary>The chunks not yet acknowledged, in ascending order — what a receiver that lost its state asks for.</summary>
	public IReadOnlyList<int> Missing()
	{
		var missing = new List<int>();
		for (var index = 0; index < File.ChunkCount; index++)
		{
			if (!_acknowledged.Contains(index))
			{
				missing.Add(index);
			}
		}

		return missing;
	}

	/// <summary>The lowercase hex SHA-256 of a whole file, the one integrity value both sides compare.</summary>
	public static string Hash(byte[] file)
	{
		using var sha = SHA256.Create();
		var hash = sha.ComputeHash(file);
		return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
	}
}
