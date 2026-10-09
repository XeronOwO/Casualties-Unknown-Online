using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One multi-block structure declaration. This is the contract a consumer
/// reads: the Game Adapter provider exposes the structure to the mod-facing
/// structure placement seam.
///
/// The grid is authored as rows from top to bottom. The first row is the visual
/// top of the structure; placement treats the supplied origin as the bottom-left
/// block coordinate of the grid. A cell marked <c>'.'</c> or <c>' '</c> is air.
/// Every other marker must map to exactly one of <see cref="VanillaBlocks"/> or
/// <see cref="TileIds"/>.
///
/// <see cref="ModStructureDefinition"/> is the framework's ready-made
/// implementation: use it when every value is a constant, and implement this
/// interface when one is computed.
/// </summary>
public interface IModStructureDefinition : IModContentDefinition
{
	/// <summary>Player-facing structure name.</summary>
	string DisplayName { get; }

	/// <summary>Player-facing structure description.</summary>
	string Description { get; }

	/// <summary>Structure width in blocks.</summary>
	int Width { get; }

	/// <summary>Structure height in blocks.</summary>
	int Height { get; }

	/// <summary>
	/// Visible grid rows from top to bottom. Each row must contain exactly
	/// <see cref="Width"/> characters; the whole list must contain exactly
	/// <see cref="Height"/> rows.
	/// </summary>
	List<string> Rows { get; }

	/// <summary>
	/// Marker character → vanilla block index. Markers are single-character
	/// strings; <c>'.'</c> and <c>' '</c> are always air. A marker must not
	/// appear in both this map and <see cref="TileIds"/>.
	/// </summary>
	Dictionary<string, int> VanillaBlocks { get; }

	/// <summary>
	/// Marker character → custom tile content id. The referenced tile must be
	/// registered by a shared-content mod through <see cref="ModContentKind.Tile"/>
	/// before the structure can be placed.
	/// </summary>
	Dictionary<string, string> TileIds { get; }

	/// <summary>
	/// Optional worldgen distribution counts, one per biome depth. An absent
	/// entry for a depth means the structure is not distributed at that depth;
	/// a present value is clamped to zero (negative authored counts are invalid
	/// and never placed).
	/// </summary>
	List<int> SpawnCounts { get; }

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	Dictionary<string, string> CustomData { get; }
}
