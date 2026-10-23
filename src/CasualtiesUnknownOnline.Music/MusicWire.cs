using System;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Music;

/// <summary>The wire shapes of the music-share packets: four fields-only maps, so every side reads the same keys.</summary>
internal static class MusicWire
{
	internal const string Announce = "music.announce";
	internal const string Chunk = "music.chunk";
	internal const string Ack = "music.ack";
	internal const string Play = "music.play";

	private const string KeyTransfer = "transfer";
	private const string KeyName = "name";
	private const string KeyLength = "length";
	private const string KeySha256 = "sha256";
	private const string KeyChunkBytes = "chunk";
	private const string KeyStartTicks = "start";
	private const string KeyIndex = "index";
	private const string KeyBytes = "bytes";

	internal static ModValue AnnounceValue(string transfer, string name, int length, string sha256, int chunkBytes, long startTicks) =>
		ModValue.Map(
			(KeyTransfer, ModValue.Text(transfer)),
			(KeyName, ModValue.Text(name)),
			(KeyLength, ModValue.Integer(length)),
			(KeySha256, ModValue.Text(sha256)),
			(KeyChunkBytes, ModValue.Integer(chunkBytes)),
			(KeyStartTicks, ModValue.Integer(startTicks)));

	internal static bool TryReadAnnounce(ModValue value, out string transfer, out string name, out int length, out string sha256, out int chunkBytes, out long startTicks)
	{
		transfer = name = sha256 = string.Empty;
		length = chunkBytes = 0;
		startTicks = 0;
		return value.TryGetField(KeyTransfer, out var transferValue) && transferValue.TryGetText(out transfer)
			&& value.TryGetField(KeyName, out var nameValue) && nameValue.TryGetText(out name)
			&& value.TryGetField(KeyLength, out var lengthValue) && lengthValue.TryGetInteger(out var lengthLong) && lengthLong is >= 0 and <= int.MaxValue
			&& value.TryGetField(KeySha256, out var shaValue) && shaValue.TryGetText(out sha256)
			&& value.TryGetField(KeyChunkBytes, out var chunkValue) && chunkValue.TryGetInteger(out var chunkLong) && chunkLong is >= 0 and <= int.MaxValue
			&& value.TryGetField(KeyStartTicks, out var startValue) && startValue.TryGetInteger(out startTicks)
			&& (length = (int)lengthLong) >= 0
			&& (chunkBytes = (int)chunkLong) > 0;
	}

	internal static ModValue ChunkValue(string transfer, int index, byte[] bytes) =>
		ModValue.Map(
			(KeyTransfer, ModValue.Text(transfer)),
			(KeyIndex, ModValue.Integer(index)),
			(KeyBytes, ModValue.Binary(bytes)));

	internal static bool TryReadChunk(ModValue value, out string transfer, out int index, out byte[] bytes)
	{
		transfer = string.Empty;
		index = 0;
		bytes = [];
		return value.TryGetField(KeyTransfer, out var transferValue) && transferValue.TryGetText(out transfer)
			&& value.TryGetField(KeyIndex, out var indexValue) && indexValue.TryGetInteger(out var indexLong) && indexLong is >= 0 and <= int.MaxValue
			&& value.TryGetField(KeyBytes, out var bytesValue) && bytesValue.TryGetBinary(out var memory)
			&& (index = (int)indexLong) >= 0
			&& (bytes = memory.ToArray()).Length > 0;
	}

	internal static ModValue AckValue(string transfer, int index) =>
		ModValue.Map(
			(KeyTransfer, ModValue.Text(transfer)),
			(KeyIndex, ModValue.Integer(index)));

	internal static bool TryReadAck(ModValue value, out string transfer, out int index)
	{
		transfer = string.Empty;
		index = 0;
		return value.TryGetField(KeyTransfer, out var transferValue) && transferValue.TryGetText(out transfer)
			&& value.TryGetField(KeyIndex, out var indexValue) && indexValue.TryGetInteger(out var indexLong) && indexLong is >= 0 and <= int.MaxValue
			&& (index = (int)indexLong) >= 0;
	}

	internal static ModValue PlayValue(string transfer, long startTicks) =>
		ModValue.Map(
			(KeyTransfer, ModValue.Text(transfer)),
			(KeyStartTicks, ModValue.Integer(startTicks)));

	internal static bool TryReadPlay(ModValue value, out string transfer, out long startTicks)
	{
		transfer = string.Empty;
		startTicks = 0;
		return value.TryGetField(KeyTransfer, out var transferValue) && transferValue.TryGetText(out transfer)
			&& value.TryGetField(KeyStartTicks, out var startValue) && startValue.TryGetInteger(out startTicks);
	}

	/// <summary>A transfer id that cannot collide with an earlier share from the same process.</summary>
	internal static string NewTransferId() => "m" + Guid.NewGuid().ToString("N").Substring(0, 12);
}
