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

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	[DataMember(Order = 12)]
	public Dictionary<string, string> CustomData { get; set; } = [];

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
