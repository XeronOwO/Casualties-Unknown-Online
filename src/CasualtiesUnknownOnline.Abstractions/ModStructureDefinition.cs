using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod-authored definition of one static multi-block structure. It is a
/// plain data object in Abstractions: no Unity type, no game type, no Runtime
/// dependency. A mod fills it in and registers it through
/// <see cref="IModContent"/>; the Game Adapter provider reads it and exposes
/// the structure to the mod-facing structure placement seam.
///
/// The grid is authored as rows from top to bottom. The first row is the visual
/// top of the structure; placement treats the supplied origin as the bottom-left
/// block coordinate of the grid. A cell marked <c>'.'</c> or <c>' '</c> is air.
/// Every other marker must map to exactly one of <see cref="VanillaBlocks"/> or
/// <see cref="TileIds"/>.
/// </summary>
public sealed class ModStructureDefinition : IModContentDefinition
{
	/// <summary>The mod-scoped content id: a canonical lower-case path segment, unique within the registering mod.</summary>
	public string Id { get; set; } = "";

	/// <summary>The content kind this definition registers under - fixed by its type, never chosen by a caller.</summary>
	public string Kind => ModContentKind.Structure;

	/// <summary>The mod-owned content schema version the framework stores verbatim (default 1).</summary>
	public int SchemaVersion { get; set; } = 1;

	/// <summary>Player-facing structure name.</summary>
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing structure description.</summary>
	public string Description { get; set; } = "";

	/// <summary>Structure width in blocks.</summary>
	public int Width { get; set; } = 1;

	/// <summary>Structure height in blocks.</summary>
	public int Height { get; set; } = 1;

	/// <summary>
	/// Visible grid rows from top to bottom. Each row must contain exactly
	/// <see cref="Width"/> characters; the whole list must contain exactly
	/// <see cref="Height"/> rows.
	/// </summary>
	public List<string> Rows
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// Marker character → vanilla block index. Markers are single-character
	/// strings; <c>'.'</c> and <c>' '</c> are always air. A marker must not
	/// appear in both this map and <see cref="TileIds"/>.
	/// </summary>
	public Dictionary<string, int> VanillaBlocks
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// Marker character → custom tile content id. The referenced tile must be
	/// registered by a shared-content mod through <see cref="ModContentKind.Tile"/>
	/// before the structure can be placed.
	/// </summary>
	public Dictionary<string, string> TileIds
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// Optional worldgen distribution counts, one per biome depth. An absent
	/// entry for a depth means the structure is not distributed at that depth;
	/// a present value is clamped to zero (negative authored counts are invalid
	/// and never placed).
	/// </summary>
	public List<int> SpawnCounts
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// Resolve the worldgen spawn count for one biome depth. Returns false when
	/// the structure has no spawn-count table for that depth; a present entry
	/// is clamped to zero (negative authored counts are invalid and never
	/// placed).
	/// </summary>
	public bool TryGetSpawnCount(int depth, out int count)
	{
		count = 0;
		if (depth < 0 || depth >= SpawnCounts.Count)
		{
			return false;
		}

		count = Math.Max(0, SpawnCounts[depth]);
		return true;
	}

}
