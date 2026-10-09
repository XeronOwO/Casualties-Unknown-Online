using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod-authored definition of one static terrain tile. It is a plain data
/// object in Abstractions: no Unity type, no game type, no Runtime dependency.
/// A mod fills it in and registers it through <see cref="IModContent"/>; the
/// Game Adapter provider reads it and
/// maps the static fields into the vanilla <c>WorldGeneration.tiles</c> palette
/// and <c>BlockInfo</c> behavior. World-generation placement is intentionally
/// not part of this DTO — mods choose where static tiles appear.
/// </summary>
public sealed class ModTileDefinition : IModContentDefinition
{
	/// <summary>The mod-scoped content id: a canonical lower-case path segment, unique within the registering mod.</summary>
	public string Id { get; set; } = "";

	/// <summary>The content kind this definition registers under - fixed by its type, never chosen by a caller.</summary>
	public string Kind => ModContentKind.Tile;

	/// <summary>The mod-owned content schema version the framework stores verbatim (default 1).</summary>
	public int SchemaVersion { get; set; } = 1;

	/// <summary>Player-facing tile name.</summary>
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing tile description.</summary>
	public string Description { get; set; } = "";

	/// <summary>
	/// Optional vanilla block index used as the visual base. When
	/// <see cref="SpritePath"/> is empty, the Game Adapter copies the sprite
	/// from this vanilla tile so a mod-authored definition can reuse an
	/// existing tile's artwork without shipping a Unity asset.
	/// </summary>
	public int? TemplateTileIndex { get; set; }

	/// <summary>
	/// Optional resource path to a <c>Sprite</c>. When set, the Game Adapter
	/// loads this sprite and it wins over <see cref="TemplateTileIndex"/>.
	/// Mod-local asset injection is a future Resource API concern; this field is
	/// the stable seam that such an API can feed.
	/// </summary>
	public string SpritePath { get; set; } = "";

	/// <summary>Optional explicit Unity object name for the generated tile asset. Defaults to the content id.</summary>
	public string TileName { get; set; } = "";

	/// <summary>Damage required to break the block.</summary>
	public float Health { get; set; } = 100f;

	/// <summary>Vanilla hit-sound reference used when the block is damaged.</summary>
	public string HitSound { get; set; } = "rock";

	/// <summary>Vanilla footstep-sound reference used when the block is walked on.</summary>
	public string StepSound { get; set; } = "Rock";

	/// <summary>Rest quality while sleeping on the tile.</summary>
	public ModTileSleepQuality SleepQuality { get; set; } = ModTileSleepQuality.Bad;

	/// <summary>Disables the game's visual tile variation for this tile.</summary>
	public bool NoVariation { get; set; }

	/// <summary>Enables the vanilla metallic damage behavior for the tile.</summary>
	public bool Metallic { get; set; }

	/// <summary>Vanilla toxirock radiation behavior value applied to the block.</summary>
	public float Toxicity { get; set; }

	/// <summary>Enables the vanilla ice behavior for the tile.</summary>
	public bool Slippery { get; set; }

	/// <summary>Tile tint red component (0..1).</summary>
	public float ColorR { get; set; } = 1f;

	/// <summary>Tile tint green component (0..1).</summary>
	public float ColorG { get; set; } = 1f;

	/// <summary>Tile tint blue component (0..1).</summary>
	public float ColorB { get; set; } = 1f;

	/// <summary>Tile tint alpha component (0..1).</summary>
	public float ColorA { get; set; } = 1f;

	/// <summary>Unity tile collider shape.</summary>
	public ModTileColliderType ColliderType { get; set; } = ModTileColliderType.Grid;

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// Copper-relative world-generation multiplier. Zero disables automatic
	/// spawning; 2f means twice as much as copper, 0.5f means half as much.
	/// </summary>
	public float SpawnAmount { get; set; }

	/// <summary>
	/// Bitmask of allowed world layers for automatic spawning. -1 means every
	/// layer; 0 disables automatic spawning. Layer N is bit N-1 (N starts at 1).
	/// </summary>
	public int SpawnLayers { get; set; } = AllSpawnLayers;

	/// <summary>Preset world-generation shapes used when <see cref="SpawnAmount"/> is greater than zero.</summary>
	public ModTileGenerationStyle GenerationStyle { get; set; } = ModTileGenerationStyle.Vein;

	/// <summary>Optional item drops spawned when the tile breaks. Empty means no custom drops.</summary>
	public List<ModTileDrop> Drops
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>Bitmask that allows spawning on every world layer.</summary>
	public const int AllSpawnLayers = -1;

	/// <summary>Build a layer bitmask from one-based layer numbers.</summary>
	public static int LayersToMask(params int[] layerNumbers)
	{
		if (layerNumbers is null)
		{
			return 0;
		}

		var mask = 0;
		foreach (var layer in layerNumbers)
		{
			if (layer > 0 && layer <= 31)
			{
				mask |= 1 << (layer - 1);
			}
		}

		return mask;
	}

	/// <summary>Build an all-layers bitmask excluding one-based layer numbers.</summary>
	public static int AllLayersExcept(params int[] excludedLayerNumbers)
	{
		if (excludedLayerNumbers is null || excludedLayerNumbers.Length == 0)
		{
			return AllSpawnLayers;
		}

		var mask = AllSpawnLayers;
		foreach (var layer in excludedLayerNumbers)
		{
			if (layer > 0 && layer <= 31)
			{
				mask &= ~(1 << (layer - 1));
			}
		}

		return mask;
	}

	/// <summary>
	/// Whether this tile is permitted to spawn automatically on a zero-based
	/// biome depth. Depth 0 is layer 1; a negative or too-large depth has no
	/// layer bit and returns false.
	/// </summary>
	public bool CanSpawnInLayer(int biomeDepth)
	{
		if (SpawnLayers == 0 || biomeDepth < 0)
		{
			return false;
		}

		if (SpawnLayers == AllSpawnLayers)
		{
			return true;
		}

		var layerNumber = biomeDepth + 1;
		return layerNumber > 0 && layerNumber <= 31 && (SpawnLayers & (1 << (layerNumber - 1))) != 0;
	}

}
