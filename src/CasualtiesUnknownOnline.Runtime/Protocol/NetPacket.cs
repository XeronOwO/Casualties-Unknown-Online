using System;
using System.IO;
using System.Text;

namespace CasualtiesUnknownOnline.Runtime.Protocol;

/// <summary>
/// Frame encode/decode helpers over SteamTransport's raw byte payloads.
/// A frame is [msgId:1] followed by a BinaryWriter payload (strings are
/// length-prefixed UTF8, floats little-endian).
/// </summary>
public static class NetPacket
{
	public static byte[] Encode(NetMsg msg, Action<BinaryWriter>? writePayload = null)
	{
		using var stream = new MemoryStream();
		stream.WriteByte((byte)msg);
		if (writePayload != null)
		{
			using var writer = new BinaryWriter(stream, Encoding.UTF8);
			writePayload(writer);
		}
		return stream.ToArray();
	}

	public static void Decode(byte[] frame, Action<BinaryReader> readPayload)
	{
		using var stream = new MemoryStream(frame, 1, frame.Length - 1);
		using var reader = new BinaryReader(stream, Encoding.UTF8);
		readPayload(reader);
	}

	public static NetVector2 ReadVector2(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle());

	public static void WriteVector2(BinaryWriter writer, NetVector2 value)
	{
		writer.Write(value.X);
		writer.Write(value.Y);
	}
}
