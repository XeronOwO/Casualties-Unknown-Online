using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One static terrain-tile declaration. This is the contract a consumer reads:
/// the Game Adapter tile provider maps these members into the vanilla
/// <c>WorldGeneration.tiles</c> palette and <c>BlockInfo</c> behavior.
/// World-generation placement is deliberately not part of the declaration —
/// mods choose where static tiles appear.
///
/// <see cref="ModTileDefinition"/> is the framework's ready-made
/// implementation: use it when every value is a constant, and implement this
/// interface when one is computed.
/// </summary>
public interface IModTileDefinition : IModContentDefinition
{
	/// <summary>Player-facing tile name.</summary>
	string DisplayName { get; }

	/// <summary>Player-facing tile description.</summary>
	string Description { get; }

	/// <summary>
	/// Optional vanilla block index used as the visual base. When
	/// <see cref="SpritePath"/> is empty, the Game Adapter copies the sprite
	/// from this vanilla tile so a mod-authored declaration can reuse an
	/// existing tile's artwork without shipping a Unity asset.
	/// </summary>
	int? TemplateTileIndex { get; }

	/// <summary>
	/// Optional resource path to a <c>Sprite</c>. When set, the Game Adapter
	/// loads this sprite and it wins over <see cref="TemplateTileIndex"/>.
	/// Mod-local asset injection is a future Resource API concern; this member is
	/// the stable seam that such an API can feed.
	/// </summary>
	string SpritePath { get; }

	/// <summary>Optional explicit Unity object name for the generated tile asset. Defaults to the content id.</summary>
	string TileName { get; }

	/// <summary>Damage required to break the block.</summary>
	float Health { get; }

	/// <summary>Vanilla hit-sound reference used when the block is damaged.</summary>
	string HitSound { get; }

	/// <summary>Vanilla footstep-sound reference used when the block is walked on.</summary>
	string StepSound { get; }

	/// <summary>Rest quality while sleeping on the tile.</summary>
	ModTileSleepQuality SleepQuality { get; }

	/// <summary>Disables the game's visual tile variation for this tile.</summary>
	bool NoVariation { get; }

	/// <summary>Enables the vanilla metallic damage behavior for the tile.</summary>
	bool Metallic { get; }

	/// <summary>Vanilla toxirock radiation behavior value applied to the block.</summary>
	float Toxicity { get; }

	/// <summary>Enables the vanilla ice behavior for the tile.</summary>
	bool Slippery { get; }

	/// <summary>Tile tint red component (0..1).</summary>
	float ColorR { get; }

	/// <summary>Tile tint green component (0..1).</summary>
	float ColorG { get; }

	/// <summary>Tile tint blue component (0..1).</summary>
	float ColorB { get; }

	/// <summary>Tile tint alpha component (0..1).</summary>
	float ColorA { get; }

	/// <summary>Unity tile collider shape.</summary>
	ModTileColliderType ColliderType { get; }

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	Dictionary<string, string> CustomData { get; }

	/// <summary>
	/// Copper-relative world-generation multiplier. Zero disables automatic
	/// spawning; 2f means twice as much as copper, 0.5f means half as much.
	/// </summary>
	float SpawnAmount { get; }

	/// <summary>
	/// Bitmask of allowed world layers for automatic spawning. -1 means every
	/// layer; 0 disables automatic spawning. Layer N is bit N-1 (N starts at 1).
	/// </summary>
	int SpawnLayers { get; }

	/// <summary>Preset world-generation shapes used when <see cref="SpawnAmount"/> is greater than zero.</summary>
	ModTileGenerationStyle GenerationStyle { get; }

	/// <summary>Optional item drops spawned when the tile breaks.</summary>
	List<ModTileDrop> Drops { get; }
}
