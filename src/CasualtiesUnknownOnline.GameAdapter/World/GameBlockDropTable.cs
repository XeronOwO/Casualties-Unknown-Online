using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.World;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// What a destroyed block may drop — the tables the host bounds a member's break
/// report against (<see cref="BlockDropBoundPolicy"/>). There are two, and they
/// are different KINDS of table:
/// <list type="bullet">
/// <item>the game's own, which is CODE: <c>WorldGeneration.DamageBlock</c>'s
/// loot switch (<c>reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:751-837</c>),
/// one arm per block id, mirrored here with the ids it names and the condition
/// window each arm rolls when it rolls one;</item>
/// <item>a CUO custom tile's, which is AUTHORED DATA:
/// <see cref="IModTileDefinition.Drops"/>, the entries
/// <c>GameAdapterTileContentProvider.TrySpawnDrops</c> rolls for a custom tile
/// index (36 and up — vanilla ids stop at 35).</item>
/// </list>
/// <para>
/// The mirror is deliberately one row per ROLL, not one row per item id: the
/// wood arm rolls <c>Random.Range(0, 3)</c> and creates exactly one of
/// woodscraps / stick / woodpanel, so it is one row naming three ids. Every
/// other arm is a chance-gated single id, so its row is the single id. See
/// <see cref="BlockDropBound"/> for why the shape matters.
/// </para>
/// <para>
/// A block id that is in NEITHER table is not a gap: the game's switch sends
/// every id it does not name to the no-loot path, and CUO's custom indices start
/// above the vanilla range, so "no rows" is the correct bound for both an
/// unnamed vanilla block (blocks 1, 2, 4, 5, 12…23, 25, 26, 28…33 and 27, whose
/// arm writes water into the cell instead of creating an item) and a custom
/// index this side has no definition for. Playing a game build whose switch
/// moved would show up as a refusal line naming the block id, which is what
/// makes the mirror diagnosable rather than silent.
/// </para>
/// </summary>
internal static class GameBlockDropTable
{
	private static readonly IReadOnlyList<BlockDropBound> NoDrops = [];

	private static readonly IReadOnlyList<BlockDropBound> ScrapMetalLowCondition = [new(["scrapmetal"], 0.05f, 0.2f)];

	private static readonly IReadOnlyList<BlockDropBound> ScrapMetalHighCondition = [new(["scrapmetal"], 0.5f, 1f)];

	private static readonly IReadOnlyList<BlockDropBound> GlassShards = [new(["glassshards"])];

	private static readonly IReadOnlyList<BlockDropBound> PlasticChunk = [new(["plasticchunk"])];

	/// <summary>Block 11 and block 24: ONE roll over three ids — never three rows (that would accept all three items from a single break).</summary>
	private static readonly IReadOnlyList<BlockDropBound> Wood = [new(["woodscraps", "stick", "woodpanel"])];

	private static readonly IReadOnlyList<BlockDropBound> RawCopper = [new(["rawcopper"])];

	private static readonly IReadOnlyList<BlockDropBound> IlmeniteChunk = [new(["ilmenitechunk"])];

	/// <summary>The game's own loot switch, block id for block id.</summary>
	internal static IReadOnlyList<BlockDropBound> ForVanilla(ushort block) => block switch
	{
		3 => ScrapMetalLowCondition,
		6 or 10 => ScrapMetalHighCondition,
		7 => GlassShards,
		8 or 9 => PlasticChunk,
		11 or 24 => Wood,
		34 => RawCopper,
		35 => IlmeniteChunk,
		_ => NoDrops,
	};

	/// <summary>
	/// A custom tile's authored drops, in the shape its own spawner produces
	/// them: an entry the spawner always skips (a non-positive chance, an empty
	/// id) contributes no row, because it can never create an item, and the
	/// condition window is the spawner's own clamping
	/// (<c>Clamp(Random.Range(0f, 1f), Clamp01(MinCondition), Max(Clamp01(MinCondition), Clamp01(MaxCondition)))</c>)
	/// so the bound matches what the author's declaration can actually produce.
	/// </summary>
	internal static IReadOnlyList<BlockDropBound> ForTile(IModTileDefinition definition)
	{
		var drops = ModDeclarationCollections.OrEmpty(definition.Drops);
		if (drops.Count == 0)
		{
			return NoDrops;
		}

		var bounds = new List<BlockDropBound>(drops.Count);
		foreach (var drop in drops)
		{
			if (drop is null || string.IsNullOrWhiteSpace(drop.ItemId) || Mathf.Clamp01(drop.Chance) <= 0f)
			{
				continue;
			}

			var min = Mathf.Clamp01(drop.MinCondition);
			var max = Mathf.Max(min, Mathf.Clamp01(drop.MaxCondition));
			bounds.Add(new BlockDropBound([drop.ItemId], min, max));
		}

		return bounds;
	}
}
