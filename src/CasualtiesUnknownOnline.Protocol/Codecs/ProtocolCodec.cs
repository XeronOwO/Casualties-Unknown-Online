using System;
using System.IO;
using CasualtiesUnknownOnline.Protocol.Wire;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Codecs;

/// <summary>
/// Deterministic protobuf-net codec for the Phase C envelope frames. The
/// codec has no GameState/Runtime dependency; it only understands wire DTOs.
/// </summary>
public static class ProtocolCodec
{
	public static byte[] Encode(ProtocolFrame frame)
	{
		if (frame is null)
		{
			throw new ArgumentNullException(nameof(frame));
		}

		using var stream = new MemoryStream();
		Serializer.Serialize(stream, frame);
		return stream.ToArray();
	}

	public static ProtocolFrame Decode(byte[] frame)
	{
		if (frame is null || frame.Length == 0)
		{
			throw new ArgumentException("Frame must not be null or empty.", nameof(frame));
		}

		using var stream = new MemoryStream(frame, writable: false);
		var result = Serializer.Deserialize<ProtocolFrame>(stream);
		if (result is null)
		{
			throw new InvalidOperationException("Protocol frame deserialized to null.");
		}

		return result;
	}
}
