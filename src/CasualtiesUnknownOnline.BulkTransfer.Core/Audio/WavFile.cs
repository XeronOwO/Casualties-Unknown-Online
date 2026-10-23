using System;
using System.Text;

namespace CasualtiesUnknownOnline.BulkTransfer.Core.Audio;

/// <summary>
/// A decoded WAV file's shape and samples, ready for a playback backend to hand to the engine: RIFF/WAVE with
/// PCM (8- or 16-bit) or IEEE float (32-bit) samples, mono or stereo. Anything else — ADPCM, an MP3 payload
/// behind a WAV header, a truncated chunk — is REFUSED with a reason rather than played as noise, because a
/// music share that plays a corrupted decode is worse than one that says why it will not.
///
/// <para>
/// The decode is game-free on purpose: the player half turns <see cref="Samples"/> into the engine's clip, and
/// this half can be tested without an engine.
/// </para>
/// </summary>
public sealed class WavFile
{
	private const ushort PcmFormat = 1;
	private const ushort FloatFormat = 3;

	private WavFile(int sampleRate, int channels, float[] samples, string? refusal)
	{
		SampleRate = sampleRate;
		Channels = channels;
		Samples = samples;
		Refusal = refusal;
	}

	/// <summary>The sample rate the data declares (the engine's clip must be created with it).</summary>
	public int SampleRate { get; }

	/// <summary>1 for mono, 2 for stereo.</summary>
	public int Channels { get; }

	/// <summary>Interleaved samples in -1..1, or empty when the file was refused.</summary>
	public float[] Samples { get; }

	/// <summary>Why the file could not be decoded, or null when it decoded.</summary>
	public string? Refusal { get; }

	/// <summary>True when <see cref="Refusal"/> is null.</summary>
	public bool Decoded => Refusal is null;

	/// <summary>The clip's length in seconds (0 for a refused file).</summary>
	public double Seconds => Channels == 0 || SampleRate == 0 ? 0 : (double)Samples.Length / Channels / SampleRate;

	/// <summary>Decode a whole WAV file, or refuse it with the reason the caller can log.</summary>
	public static WavFile Decode(byte[] bytes)
	{
		if (bytes is null || bytes.Length < 44)
		{
			return Refuse("the file is shorter than a WAV header");
		}

		if (!Tag(bytes, 0, "RIFF") || !Tag(bytes, 8, "WAVE"))
		{
			return Refuse("the file is not a RIFF/WAVE container");
		}

		var offset = 12;
		var format = 0;
		var channels = 0;
		var sampleRate = 0;
		var bitsPerSample = 0;
		var dataOffset = -1;
		var dataLength = 0;
		while (offset + 8 <= bytes.Length)
		{
			var chunkId = Encoding.ASCII.GetString(bytes, offset, 4);
			var chunkLength = BitConverter.ToInt32(bytes, offset + 4);
			var body = offset + 8;
			if (chunkLength < 0 || body + chunkLength > bytes.Length)
			{
				return Refuse($"the {chunkId} chunk runs past the file's end");
			}

			if (chunkId == "fmt ")
			{
				if (chunkLength < 16)
				{
					return Refuse("the fmt chunk is too short");
				}

				format = BitConverter.ToUInt16(bytes, body);
				channels = BitConverter.ToUInt16(bytes, body + 2);
				sampleRate = BitConverter.ToInt32(bytes, body + 4);
				bitsPerSample = BitConverter.ToUInt16(bytes, body + 14);
			}
			else if (chunkId == "data")
			{
				dataOffset = body;
				dataLength = chunkLength;
			}

			// RIFF chunks are padded to an even length; the pad byte is not part of the chunk.
			offset = body + chunkLength + (chunkLength & 1);
		}

		if (dataOffset < 0)
		{
			return Refuse("the file carries no data chunk");
		}

		if (channels is not (1 or 2))
		{
			return Refuse($"only mono and stereo are supported (the file declares {channels})");
		}

		if (sampleRate is < 4000 or > 192000)
		{
			return Refuse($"the sample rate {sampleRate} is outside 4000..192000");
		}

		if (format != PcmFormat && format != FloatFormat)
		{
			return Refuse($"the sample format {format} is not PCM or IEEE float (a compressed payload behind a WAV header is not played)");
		}

		var sampleBytes = format == PcmFormat ? bitsPerSample / 8 : 4;
		if (format == PcmFormat && bitsPerSample is not (8 or 16))
		{
			return Refuse($"PCM samples must be 8 or 16 bits (the file declares {bitsPerSample})");
		}

		if (format == FloatFormat && bitsPerSample != 32)
		{
			return Refuse($"float samples must be 32 bits (the file declares {bitsPerSample})");
		}

		var sampleCount = dataLength / sampleBytes;
		if (sampleCount == 0)
		{
			return Refuse("the data chunk carries no samples");
		}

		var samples = new float[sampleCount];
		for (var i = 0; i < sampleCount; i++)
		{
			var at = dataOffset + (i * sampleBytes);
			samples[i] = format == FloatFormat
				? BitConverter.ToSingle(bytes, at)
				: bitsPerSample == 8
					? (bytes[at] - 128) / 128f
					: BitConverter.ToInt16(bytes, at) / 32768f;
		}

		return new WavFile(sampleRate, channels, samples, null);
	}

	private static bool Tag(byte[] bytes, int offset, string tag)
	{
		for (var i = 0; i < tag.Length; i++)
		{
			if (bytes[offset + i] != tag[i])
			{
				return false;
			}
		}

		return true;
	}

	private static WavFile Refuse(string reason) => new(0, 0, [], reason);
}
