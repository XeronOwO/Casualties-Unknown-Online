using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The one rule behind a structure's world-generation distribution count. A
/// declaration owns the FACT (its <c>SpawnCounts</c> table); reading one depth
/// out of it, and clamping a negative authored count to zero, is the
/// framework's rule and lives here so an implementation that computes the table
/// does not have to restate it.
/// </summary>
public static class ModStructureDistribution
{
	extension(IModStructureDefinition definition)
	{
		/// <summary>
		/// Resolve the worldgen spawn count for one biome depth. Returns false when
		/// the structure has no spawn-count table for that depth; a present entry
		/// is clamped to zero (negative authored counts are invalid and never
		/// placed).
		/// </summary>
		public bool TryGetSpawnCount(int depth, out int count)
		{
			count = 0;
			var spawnCounts = ModDeclarationCollections.OrEmpty(definition.SpawnCounts);
			if (depth < 0 || depth >= spawnCounts.Count)
			{
				return false;
			}

			count = Math.Max(0, spawnCounts[depth]);
			return true;
		}
	}
}
