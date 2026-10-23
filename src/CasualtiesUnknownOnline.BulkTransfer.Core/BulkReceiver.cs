using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.BulkTransfer.Core;

/// <summary>
/// The receiving half: it holds the announced identity, takes chunks by index, answers which are still missing
/// and, once every one is present, VERIFIES the whole file against the announced SHA-256 before handing it over.
/// A transfer that fails the hash is refused with nothing returned — a corrupted song must never play.
/// </summary>
public sealed class BulkReceiver(BulkFile file)
{
	private readonly BulkFile _file = file ?? throw new ArgumentNullException(nameof(file));
	private readonly byte[] _buffer = new byte[file!.Length];
	private readonly bool[] _received = new bool[file!.ChunkCount];
	private int _receivedCount;

	/// <summary>The announcement this receiver is filling.</summary>
	public BulkFile File => _file;

	/// <summary>True when every chunk is present (not yet the same as a verified file).</summary>
	public bool Complete => _receivedCount == _file.ChunkCount;

	/// <summary>How many chunks are still missing.</summary>
	public int MissingCount => _file.ChunkCount - _receivedCount;

	/// <summary>Take a chunk, applying the announced identity and shape as the gate.</summary>
	public BulkChunkVerdict Take(BulkChunk chunk)
	{
		if (chunk is null || chunk.Index < 0 || chunk.Index >= _file.ChunkCount || chunk.Bytes.Length != _file.LengthOf(chunk.Index))
		{
			return BulkChunkVerdict.Malformed;
		}

		if (_received[chunk.Index])
		{
			return BulkChunkVerdict.Duplicate;
		}

		Buffer.BlockCopy(chunk.Bytes, 0, _buffer, _file.OffsetOf(chunk.Index), chunk.Bytes.Length);
		_received[chunk.Index] = true;
		_receivedCount++;
		return BulkChunkVerdict.Accepted;
	}

	/// <summary>The indices still missing, ascending — what to ask the sender to offer again.</summary>
	public IReadOnlyList<int> Missing()
	{
		var missing = new List<int>();
		for (var index = 0; index < _file.ChunkCount; index++)
		{
			if (!_received[index])
			{
				missing.Add(index);
			}
		}

		return missing;
	}

	/// <summary>
	/// The completed file, or null when it is incomplete or its hash does not match the announcement. The hash is
	/// the whole file's, so one flipped bit anywhere refuses the transfer rather than handing over a bad song.
	/// </summary>
	public byte[]? TryAssemble()
	{
		if (!Complete)
		{
			return null;
		}

		return string.Equals(BulkSender.Hash(_buffer), _file.Sha256, StringComparison.Ordinal) ? [.. _buffer] : null;
	}
}
