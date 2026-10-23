using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.BulkTransfer.Core.Audio;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Transfers;

/// <summary>
/// The WAV decode the player half will hand to the engine: a mono/stereo PCM file becomes interleaved samples
/// with the declared shape, and anything the decode cannot honestly play is refused with a reason instead of
/// being turned into noise.
/// </summary>
public class WavFileTests
{
	/// <summary>Build a minimal RIFF/WAVE file: a fmt chunk and a data chunk, optionally with an extra chunk before them.</summary>
	private static byte[] Build(ushort format, ushort channels, int sampleRate, ushort bits, byte[] data, string? extraChunkId = null, int extraChunkLength = 0)
	{
		var chunks = new List<byte>();
		if (extraChunkId is not null)
		{
			chunks.AddRange(Encoding.ASCII.GetBytes(extraChunkId));
			chunks.AddRange(BitConverter.GetBytes(extraChunkLength));
			chunks.AddRange(new byte[extraChunkLength + (extraChunkLength & 1)]);
		}

		var fmt = new List<byte>();
		fmt.AddRange(BitConverter.GetBytes(format));
		fmt.AddRange(BitConverter.GetBytes(channels));
		fmt.AddRange(BitConverter.GetBytes(sampleRate));
		fmt.AddRange(BitConverter.GetBytes(sampleRate * channels * bits / 8));
		fmt.AddRange(BitConverter.GetBytes((ushort)(channels * bits / 8)));
		fmt.AddRange(BitConverter.GetBytes(bits));
		chunks.AddRange(Encoding.ASCII.GetBytes("fmt "));
		chunks.AddRange(BitConverter.GetBytes(fmt.Count));
		chunks.AddRange(fmt);
		chunks.AddRange(Encoding.ASCII.GetBytes("data"));
		chunks.AddRange(BitConverter.GetBytes(data.Length));
		chunks.AddRange(data);

		var file = new List<byte>();
		file.AddRange(Encoding.ASCII.GetBytes("RIFF"));
		file.AddRange(BitConverter.GetBytes(4 + chunks.Count));
		file.AddRange(Encoding.ASCII.GetBytes("WAVE"));
		file.AddRange(chunks);
		return [.. file];
	}

	private static byte[] Samples16(params short[] values)
	{
		var bytes = new List<byte>();
		foreach (var value in values)
		{
			bytes.AddRange(BitConverter.GetBytes(value));
		}

		return [.. bytes];
	}

	[Fact]
	public void AMonoPcmFile_DecodesItsShapeAndSamples()
	{
		var bytes = Build(1, 1, 44100, 16, Samples16(0, 16384, -32768, 32767));

		var wav = WavFile.Decode(bytes);

		Assert.True(wav.Decoded);
		Assert.Equal(44100, wav.SampleRate);
		Assert.Equal(1, wav.Channels);
		Assert.Equal(4, wav.Samples.Length);
		Assert.Equal(0f, wav.Samples[0], 3);
		Assert.Equal(0.5f, wav.Samples[1], 3);
		Assert.Equal(-1f, wav.Samples[2], 3);
		Assert.True(wav.Seconds > 0);
	}

	[Fact]
	public void AStereoFile_KeepsTheInterleavedOrder()
	{
		var bytes = Build(1, 2, 48000, 16, Samples16(100, -100, 200, -200));

		var wav = WavFile.Decode(bytes);

		Assert.True(wav.Decoded);
		Assert.Equal(2, wav.Channels);
		Assert.Equal(4, wav.Samples.Length);
		Assert.True(wav.Samples[0] > 0);
		Assert.True(wav.Samples[1] < 0);
	}

	[Fact]
	public void AnExtraChunkBeforeTheData_IsSkippedWithItsPadByte()
	{
		var bytes = Build(1, 1, 8000, 16, Samples16(0, 0), extraChunkId: "LIST", extraChunkLength: 5);

		var wav = WavFile.Decode(bytes);

		Assert.True(wav.Decoded);
		Assert.Equal(2, wav.Samples.Length);
	}

	[Fact]
	public void ACompressedPayload_IsRefusedWithItsFormat()
	{
		var bytes = Build(0x55, 1, 44100, 16, Samples16(0, 0));

		var wav = WavFile.Decode(bytes);

		Assert.False(wav.Decoded);
		Assert.Contains("not PCM", wav.Refusal);
	}

	[Fact]
	public void ATruncatedFile_IsRefused()
	{
		var bytes = Build(1, 1, 44100, 16, Samples16(0, 0));
		var truncated = bytes.Take(bytes.Length - 3).ToArray();

		var wav = WavFile.Decode(truncated);

		Assert.False(wav.Decoded);
		Assert.NotNull(wav.Refusal);
	}

	[Fact]
	public void AFileThatIsNotRiff_IsRefused()
	{
		var wav = WavFile.Decode([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);

		Assert.False(wav.Decoded);
		Assert.Contains("shorter", wav.Refusal);
	}

	[Fact]
	public void FloatSamples_DecodeAsWritten()
	{
		var data = new List<byte>();
		data.AddRange(BitConverter.GetBytes(0.25f));
		data.AddRange(BitConverter.GetBytes(-0.5f));
		var bytes = Build(3, 1, 22050, 32, [.. data]);

		var wav = WavFile.Decode(bytes);

		Assert.True(wav.Decoded);
		Assert.Equal(0.25f, wav.Samples[0], 4);
		Assert.Equal(-0.5f, wav.Samples[1], 4);
		Assert.InRange(wav.Seconds, 0.00001, 0.001);
	}
}
