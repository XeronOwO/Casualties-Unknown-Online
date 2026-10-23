using System;
using CasualtiesUnknownOnline.BulkTransfer.Core;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Transfers;

/// <summary>
/// The bulk transfer's rules, without a session: a file sliced into bounded chunks reassembles byte for byte,
/// a chunk that never arrives is offered again (which IS the resume story), a duplicate is not applied twice, a
/// chunk outside the announced shape is refused, and a file whose hash does not match the announcement is never
/// handed over.
/// </summary>
public class BulkTransferTests
{
	private static byte[] Data(int length, byte seed = 7)
	{
		var bytes = new byte[length];
		for (var i = 0; i < length; i++)
		{
			bytes[i] = (byte)((i * seed) & 0xFF);
		}

		return bytes;
	}

	[Fact]
	public void AWholeFile_ReassemblesByteForByte()
	{
		var file = Data(5000);
		var sender = new BulkSender("song-1", file, chunkBytes: BulkTransferPolicy.MinimumChunkBytes);
		var receiver = new BulkReceiver(sender.File);

		while (sender.Next() is { } chunk)
		{
			Assert.Equal(BulkChunkVerdict.Accepted, receiver.Take(chunk));
			Assert.True(sender.Acknowledged(chunk.Index));
		}

		Assert.True(sender.Complete);
		Assert.True(receiver.Complete);
		Assert.Equal(file, receiver.TryAssemble());
	}

	[Fact]
	public void TheLastChunk_IsShortAndCountsAsTheAnnouncedLength()
	{
		var sender = new BulkSender("song-1", Data(4300), chunkBytes: 1024);

		Assert.Equal(5, sender.File.ChunkCount);
		Assert.Equal(1024, sender.File.LengthOf(0));
		Assert.Equal(204, sender.File.LengthOf(4));
		Assert.Equal(4300, sender.File.Length);
	}

	[Fact]
	public void AChunkThatNeverArrives_StaysOutstandingAndBlocksTheWindowUntilRetried()
	{
		var sender = new BulkSender("song-1", Data(4096), chunkBytes: 1024);
		var receiver = new BulkReceiver(sender.File);
		var first = sender.Next(window: 1);
		Assert.NotNull(first);

		// Its acknowledgement is lost, so the window stays full: nothing new is offered, and the chunk is still
		// missing (a retry re-offers it once the window opens).
		Assert.Null(sender.Next(window: 1));
		Assert.Equal(1, sender.Outstanding);
		Assert.Equal([0, 1, 2, 3], sender.Missing());
		Assert.False(sender.Acknowledged(99));

		Assert.True(sender.Acknowledged(first!.Index));
		var second = sender.Next(window: 1);
		Assert.NotNull(second);
		Assert.Equal(1, second!.Index);
		Assert.Equal(BulkChunkVerdict.Accepted, receiver.Take(first));
		Assert.Equal(BulkChunkVerdict.Accepted, receiver.Take(second));
	}

	[Fact]
	public void ADuplicateChunk_IsNotAppliedTwice()
	{
		var sender = new BulkSender("song-1", Data(3000), chunkBytes: 1024);
		var receiver = new BulkReceiver(sender.File);
		var chunk = sender.Next()!;

		Assert.Equal(BulkChunkVerdict.Accepted, receiver.Take(chunk));
		Assert.Equal(BulkChunkVerdict.Duplicate, receiver.Take(chunk));
		Assert.Equal(2, receiver.MissingCount);
	}

	[Fact]
	public void AChunkOutsideTheFile_IsRefused()
	{
		var receiver = new BulkReceiver(new BulkSender("song-1", Data(2000), chunkBytes: 1024).File);

		Assert.Equal(BulkChunkVerdict.Malformed, receiver.Take(new BulkChunk(2, Data(1024))));
		Assert.Equal(BulkChunkVerdict.Malformed, receiver.Take(new BulkChunk(0, Data(10))));
		Assert.Equal(BulkChunkVerdict.Malformed, receiver.Take(new BulkChunk(-1, Data(1024))));
	}

	[Fact]
	public void AFileWhoseHashDoesNotMatch_IsNeverHandedOver()
	{
		var sender = new BulkSender("song-1", Data(2000), chunkBytes: 1024);
		var receiver = new BulkReceiver(sender.File);
		while (sender.Next() is { } chunk)
		{
			var corrupt = new BulkChunk(chunk.Index, [.. chunk.Bytes]);
			corrupt.Bytes[0] ^= 0xFF;
			receiver.Take(corrupt);
			sender.Acknowledged(chunk.Index);
		}

		Assert.True(receiver.Complete);
		Assert.Null(receiver.TryAssemble());
	}

	[Fact]
	public void AnIncompleteFile_IsNotAssembled()
	{
		var sender = new BulkSender("song-1", Data(2000), chunkBytes: 1024);
		var receiver = new BulkReceiver(sender.File);
		receiver.Take(sender.Next()!);

		Assert.False(receiver.Complete);
		Assert.Null(receiver.TryAssemble());
	}

	[Fact]
	public void AChunkSizeOutsideTheRails_IsRefused()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new BulkSender("song-1", Data(10), chunkBytes: 10));
		Assert.Throws<ArgumentOutOfRangeException>(() => new BulkSender("song-1", Data(10), chunkBytes: BulkTransferPolicy.MaximumChunkBytes + 1));
	}

	[Fact]
	public void TheHashIsTheSameOnBothSides_AndLowercaseHex()
	{
		var file = Data(1234);
		var hash = BulkSender.Hash(file);

		Assert.Equal(64, hash.Length);
		Assert.Equal(hash.ToLowerInvariant(), hash);
		Assert.Equal(hash, new BulkSender("a", file).File.Sha256);
	}
}
