using System;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// The IP-direct wire's frame codec and peer-id helper: <c>[int32 big-endian length][payload]</c>, the
/// uint64 hello the guest sends right after connecting, and a random guest peer id that never collides
/// with the host's reserved id. Kept out of the transport so the connection and diagnostics concerns stay
/// readable.
/// </summary>
internal static class IpDirectFrames
{
	internal static void WriteInt32(byte[] buffer, int offset, int value)
	{
		buffer[offset] = (byte)(value >> 24);
		buffer[offset + 1] = (byte)(value >> 16);
		buffer[offset + 2] = (byte)(value >> 8);
		buffer[offset + 3] = (byte)value;
	}

	internal static int ReadInt32(byte[] buffer, int offset) =>
		(buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3];

	internal static void WriteUInt64(byte[] buffer, int offset, ulong value)
	{
		for (var i = 0; i < 8; i++)
		{
			buffer[offset + i] = (byte)(value >> (56 - (i * 8)));
		}
	}

	internal static ulong ReadUInt64(byte[] buffer, int offset)
	{
		ulong value = 0;
		for (var i = 0; i < 8; i++)
		{
			value = (value << 8) | buffer[offset + i];
		}

		return value;
	}

	/// <summary>Reads exactly <paramref name="count"/> bytes, returning null on EOF or when the timeout passes with nothing readable.</summary>
	internal static byte[]? ReadExactly(NetworkStream stream, int count, int timeoutMs)
	{
		var buffer = new byte[count];
		var read = 0;
		while (read < count)
		{
			if (timeoutMs > 0 && !stream.DataAvailable)
			{
				var deadline = Environment.TickCount + timeoutMs;
				while (!stream.DataAvailable && Environment.TickCount < deadline)
				{
					Thread.Sleep(10);
				}

				if (!stream.DataAvailable)
				{
					return null;
				}
			}

			var n = stream.Read(buffer, read, count - read);
			if (n == 0)
			{
				return null;
			}

			read += n;
		}

		return buffer;
	}

	/// <summary>A random 64-bit guest id that is never zero and never the host's reserved id.</summary>
	internal static ulong GenerateGuestPeerId()
	{
		var bytes = new byte[8];
		using (var rng = RandomNumberGenerator.Create())
		{
			rng.GetBytes(bytes);
		}

		var id = BitConverter.ToUInt64(bytes, 0);
		if (id == 0 || id == IpDirectTransport.HostPeerId)
		{
			id ^= 0x9E3779B97F4A7C15UL;
		}

		return id;
	}
}
