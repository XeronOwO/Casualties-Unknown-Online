using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The versioned, mod-authored data contract for one item content definition.
/// It is deliberately a plain data object in Abstractions: no game type, no
/// Unity type, no Runtime dependency. The payload is still registered through
/// the opaque <see cref="IModContent"/> channel; this type is the first
/// well-known schema that the Runtime content binder and a Game Adapter
/// provider can decode without inventing a private format.
/// </summary>
[DataContract]
public sealed class ModItemDefinition
{
	/// <summary>Player-facing item name.</summary>
	[DataMember(Order = 1)]
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing item description.</summary>
	[DataMember(Order = 2)]
	public string Description { get; set; } = "";

	/// <summary>Vanilla spawn/category tag; defaults to "nospawn" when empty.</summary>
	[DataMember(Order = 3)]
	public string Category { get; set; } = "nospawn";

	/// <summary>Item weight in vanilla units.</summary>
	[DataMember(Order = 4)]
	public float Weight { get; set; }

	/// <summary>Vanilla item value.</summary>
	[DataMember(Order = 5)]
	public int Value { get; set; }

	/// <summary>Whether the item can be used from the hand.</summary>
	[DataMember(Order = 6)]
	public bool Usable { get; set; }

	/// <summary>Whether the item can be used with the left mouse button.</summary>
	[DataMember(Order = 7)]
	public bool UsableWithLmb { get; set; }

	/// <summary>Whether the item can be worn on a body.</summary>
	[DataMember(Order = 8)]
	public bool Wearable { get; set; }

	/// <summary>Whether the item is destroyed when its condition reaches zero.</summary>
	[DataMember(Order = 9)]
	public bool DestroyAtZeroCondition { get; set; }

	/// <summary>Vanilla tag string, empty when none.</summary>
	[DataMember(Order = 10)]
	public string Tags { get; set; } = "";

	/// <summary>Relative spawn/trader/loot weighting.</summary>
	[DataMember(Order = 11)]
	public int SpawnFrequency { get; set; } = 1;

	/// <summary>
	/// The vanilla prefab id used as the runtime template base. Empty means the
	/// definition is static-item-info only; the Game Adapter cannot materialize
	/// a prefab for it.
	/// </summary>
	[DataMember(Order = 12)]
	public string TemplateId { get; set; } = "";

	/// <summary>
	/// Component type names (assembly-qualified or simple names) attached to the
	/// runtime template before it is instantiated. The Game Adapter resolves
	/// the types from loaded assemblies and refuses non-Component types.
	/// </summary>
	[DataMember(Order = 13)]
	public List<string> SpawnComponents { get; set; } = [];

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	[DataMember(Order = 14)]
	public Dictionary<string, string> CustomData { get; set; } = [];

	/// <summary>
	/// Average loose world-spawn count per worldgen chunk. Null or zero disables
	/// automatic world spawning; a positive value makes the Game Adapter scatter
	/// the item on ground inside the isolated generation stream. The existing
	/// generation-item snapshot synchronizes both sides — no new wire is needed.
	/// </summary>
	[DataMember(Order = 15)]
	public float? WorldSpawnPerChunk { get; set; }

	/// <summary>
	/// Optional explicit fixed drop-source pools. When set, the item is not added
	/// to the generic vanilla category loot pool; instead it is registered only in
	/// the selected source pools (corpse, built-in crates, trader stock). Leave
	/// null to use the vanilla category fallback.
	/// </summary>
	[DataMember(Order = 16)]
	public ModItemDropSource? DropSources { get; set; }

	/// <summary>Optional container behavior applied to the runtime item template.</summary>
	[DataMember(Order = 17)]
	public ModItemContainer? Container { get; set; }

	/// <summary>Optional battery behavior applied to the runtime item template.</summary>
	[DataMember(Order = 18)]
	public ModItemBattery? Battery { get; set; }

	/// <summary>Optional light behavior applied to the runtime item template.</summary>
	[DataMember(Order = 19)]
	public ModItemLight? Light { get; set; }

	/// <summary>Optional melee/tool behavior applied to the item's static use action.</summary>
	[DataMember(Order = 20)]
	public ModItemTool? Tool { get; set; }

	/// <summary>Optional firearm behavior applied to the runtime item template and static use action.</summary>
	[DataMember(Order = 21)]
	public ModItemGun? Gun { get; set; }

	/// <summary>
	/// Vanilla decay time in in-game minutes. Zero disables time-based decay;
	/// a positive value also sets the computed <c>rotSpeed</c> used by the
	/// vanilla decay path (including battery-powered drain when
	/// <see cref="Battery"/> is present).
	/// </summary>
	[DataMember(Order = 22)]
	public float DecayMinutes { get; set; }

	/// <summary>Serialize this definition into the opaque payload format.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModItemDefinition));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize an item definition payload. Returns null when the payload is not a valid item definition.</summary>
	public static ModItemDefinition? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModItemDefinition));
			return serializer.ReadObject(stream) as ModItemDefinition;
		}
		catch (Exception)
		{
			// Any deserialization failure means the payload is not a valid item
			// definition under the current contract; the binder should refuse it
			// rather than fail the whole mod discovery.
			return null;
		}
	}
}
