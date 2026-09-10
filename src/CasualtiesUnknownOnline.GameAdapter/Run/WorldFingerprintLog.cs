using System;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The one-shot world fingerprint logged at world entry: FNV-1a over the block
/// table, per-128-row blocks plus a total, so the two peers' logs show whether
/// the generated worlds match and, when they diverge, roughly where. Pure
/// computation + one log line, split out of <see cref="RunCoordinator"/> (the
/// run life-cycle machine is not the place for a hash).
/// </summary>
internal static class WorldFingerprintLog
{
	private const ulong FnvBasis = 14695981039346656037UL;
	private const ulong FnvPrime = 1099511628211UL;

	/// <summary>Hashes the live block table and logs it; a missing world logs nothing.</summary>
	internal static void Log(ILogger log)
	{
		var blocks = HarmonyTraverse.ReadWorldBlocks(WorldGeneration.world);
		if (blocks is null) // Unity object — ==
		{
			return;
		}

		var width = blocks.GetLength(0);
		var height = blocks.GetLength(1);
		var blockHashes = new ulong[8];
		for (var i = 0; i < blockHashes.Length; i++)
		{
			blockHashes[i] = FnvBasis;
		}

		var rowsPerBlock = Math.Max(1, height / 8);
		var total = FnvBasis;
		for (var y = 0; y < height; y++)
		{
			var b = Math.Min(7, y / rowsPerBlock);
			for (var x = 0; x < width; x++)
			{
				var v = blocks[x, y];
				total ^= v;
				total *= FnvPrime;
				blockHashes[b] ^= v;
				blockHashes[b] *= FnvPrime;
			}
		}

		log.LogInformation(
			"[WorldFingerprint] {W}x{H}: {B0:X16} {B1:X16} {B2:X16} {B3:X16} {B4:X16} {B5:X16} {B6:X16} {B7:X16} total {Total:X16}",
			width, height, blockHashes[0], blockHashes[1], blockHashes[2], blockHashes[3],
			blockHashes[4], blockHashes[5], blockHashes[6], blockHashes[7], total);
	}
}
