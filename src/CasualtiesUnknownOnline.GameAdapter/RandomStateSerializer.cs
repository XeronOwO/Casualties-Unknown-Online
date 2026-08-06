using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Binary serialization of UnityEngine.Random.State. The struct is a sequential
/// fixed-buffer blob (256 uints + index), so a raw memory copy round-trips it —
/// the same trick KrokMP uses (SerializableRandomState + memcpy). Size is taken
/// from Marshal at runtime instead of hardcoding the layout.
/// </summary>
internal static class RandomStateSerializer
{
	private static readonly int Size = Marshal.SizeOf(typeof(Random.State));

	public static byte[] Serialize(Random.State state)
	{
		var bytes = new byte[Size];
		unsafe
		{
			fixed (byte* p = bytes)
				*(Random.State*)p = state;
		}
		return bytes;
	}

	public static Random.State Deserialize(byte[] bytes)
	{
		if (bytes.Length != Size)
			throw new ArgumentException($"Random.State size mismatch: expected {Size}, got {bytes.Length}.");
		unsafe
		{
			fixed (byte* p = bytes)
				return *(Random.State*)p;
		}
	}
}
