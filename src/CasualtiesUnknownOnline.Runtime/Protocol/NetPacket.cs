using System.IO;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol;

/// <summary>
/// Frame encode/decode helpers over SteamTransport's raw byte payloads.
/// A frame is [msgId:1] followed by a protobuf-net serialized [ProtoContract]
/// message (Protocol/Messages/). Length is implicit (rest of the frame).
/// </summary>
public static class NetPacket
{
	public static byte[] Encode(NetMsg msg, object? payload = null)
	{
		using var stream = new MemoryStream();
		stream.WriteByte((byte)msg);
		if (payload is not null)
		{
			Serializer.Serialize(stream, payload);
		}

		return stream.ToArray();
	}

	/// <summary>Deserializes the payload of a received frame (skips the msgId byte).</summary>
	public static T DecodePayload<T>(byte[] frame)
	{
		using var stream = new MemoryStream(frame, 1, frame.Length - 1);
		return Serializer.Deserialize<T>(stream);
	}
}
