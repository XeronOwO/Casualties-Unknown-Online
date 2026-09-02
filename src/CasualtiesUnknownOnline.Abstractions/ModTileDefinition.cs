using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The versioned, mod-authored data contract for one static terrain tile
/// definition. It is a plain data object in Abstractions: no Unity type, no
/// game type, no Runtime dependency. The payload is registered through the
/// opaque <see cref="IModContent"/> channel; the Game Adapter decodes it and
/// maps the static fields into the vanilla <c>WorldGeneration.tiles</c> palette
/// and <c>BlockInfo</c> behavior. World-generation placement is intentionally
/// not part of this DTO — mods choose where static tiles appear.
/// </summary>
[DataContract]
public sealed class ModTileDefinition
{
	/// <summary>Player-facing tile name.</summary>
	[DataMember(Order = 1)]
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing tile description.</summary>
	[DataMember(Order = 2)]
	public string Description { get; set; } = "";

	/// <summary>
	/// Optional vanilla block index used as the visual base. When
	/// <see cref="SpritePath"/> is empty, the Game Adapter copies the sprite
	/// from this vanilla tile so a mod-authored definition can reuse an
	/// existing tile's artwork without shipping a Unity asset.
	/// </summary>
	[DataMember(Order = 3)]
	public int? TemplateTileIndex { get; set; }

	/// <summary>
	/// Optional resource path to a <c>Sprite</c>. When set, the Game Adapter
	/// loads this sprite and it wins over <see cref="TemplateTileIndex"/>.
	/// Mod-local asset injection is a future Resource API concern; this field is
	/// the stable seam that such an API can feed.
	/// </summary>
	[DataMember(Order = 4)]
	public string SpritePath { get; set; } = "";

	/// <summary>Optional explicit Unity object name for the generated tile asset. Defaults to the content id.</summary>
	[DataMember(Order = 5)]
	public string TileName { get; set; } = "";

	/// <summary>Damage required to break the block.</summary>
	[DataMember(Order = 6)]
	public float Health { get; set; } = 100f;

	/// <summary>Vanilla hit-sound reference used when the block is damaged.</summary>
	[DataMember(Order = 7)]
	public string HitSound { get; set; } = "rock";

	/// <summary>Vanilla footstep-sound reference used when the block is walked on.</summary>
	[DataMember(Order = 8)]
	public string StepSound { get; set; } = "Rock";

	/// <summary>Rest quality while sleeping on the tile.</summary>
	[DataMember(Order = 9)]
	public ModTileSleepQuality SleepQuality { get; set; } = ModTileSleepQuality.Bad;

	/// <summary>Disables the game's visual tile variation for this tile.</summary>
	[DataMember(Order = 10)]
	public bool NoVariation { get; set; }

	/// <summary>Enables the vanilla metallic damage behavior for the tile.</summary>
	[DataMember(Order = 11)]
	public bool Metallic { get; set; }

	/// <summary>Vanilla toxirock radiation behavior value applied to the block.</summary>
	[DataMember(Order = 12)]
	public float Toxicity { get; set; }

	/// <summary>Enables the vanilla ice behavior for the tile.</summary>
	[DataMember(Order = 13)]
	public bool Slippery { get; set; }

	/// <summary>Tile tint red component (0..1).</summary>
	[DataMember(Order = 14)]
	public float ColorR { get; set; } = 1f;

	/// <summary>Tile tint green component (0..1).</summary>
	[DataMember(Order = 15)]
	public float ColorG { get; set; } = 1f;

	/// <summary>Tile tint blue component (0..1).</summary>
	[DataMember(Order = 16)]
	public float ColorB { get; set; } = 1f;

	/// <summary>Tile tint alpha component (0..1).</summary>
	[DataMember(Order = 17)]
	public float ColorA { get; set; } = 1f;

	/// <summary>Unity tile collider shape.</summary>
	[DataMember(Order = 18)]
	public ModTileColliderType ColliderType { get; set; } = ModTileColliderType.Grid;

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	[DataMember(Order = 19)]
	public Dictionary<string, string> CustomData { get; set; } = [];

	/// <summary>Serialize this definition into the opaque payload format.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModTileDefinition));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize a tile definition payload. Returns null when the payload is not a valid tile definition.</summary>
	public static ModTileDefinition? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModTileDefinition));
			return serializer.ReadObject(stream) as ModTileDefinition;
		}
		catch (Exception)
		{
			// Any deserialization failure means the payload is not a valid tile
			// definition under the current contract; the binder should refuse it
			// rather than fail the whole mod discovery.
			return null;
		}
	}
}
