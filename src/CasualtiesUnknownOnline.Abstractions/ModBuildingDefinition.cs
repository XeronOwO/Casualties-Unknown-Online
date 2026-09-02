using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The versioned, mod-authored data contract for one building-entity content
/// definition. It is a plain data object in Abstractions: no game type, no
/// Unity type, no Runtime dependency. The payload is still registered through
/// the opaque <see cref="IModContent"/> channel; this type is the well-known
/// schema that the Runtime content binder and a Game Adapter provider can
/// decode into a runtime building prefab without inventing a private format.
/// </summary>
[DataContract]
public sealed class ModBuildingDefinition
{
	/// <summary>Player-facing building name.</summary>
	[DataMember(Order = 1)]
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing building description.</summary>
	[DataMember(Order = 2)]
	public string Description { get; set; } = "";

	/// <summary>
	/// The vanilla prefab id used as the runtime template base. The Game
	/// Adapter clones this prefab and renames the clone to the registered
	/// building id.
	/// </summary>
	[DataMember(Order = 3)]
	public string TemplateId { get; set; } = "";

	/// <summary>Optional override for the cloned building's health.</summary>
	[DataMember(Order = 4)]
	public float? Health { get; set; }

	/// <summary>Optional override for whether the building needs ground support.</summary>
	[DataMember(Order = 5)]
	public bool? RequireGround { get; set; }

	/// <summary>Optional override for the vanilla animal flag.</summary>
	[DataMember(Order = 6)]
	public bool? Animal { get; set; }

	/// <summary>Optional override for the vanilla cannot-be-hit flag.</summary>
	[DataMember(Order = 7)]
	public bool? CantHit { get; set; }

	/// <summary>Optional override for the vanilla metallic flag.</summary>
	[DataMember(Order = 8)]
	public bool? Metallic { get; set; }

	/// <summary>Optional override for the vanilla body-optimization suppression flag.</summary>
	[DataMember(Order = 9)]
	public bool? IgnoreBodyOptimize { get; set; }

	/// <summary>Optional override for the chance-based drop multiplier.</summary>
	[DataMember(Order = 10)]
	public float? DropChanceMultiplier { get; set; }

	/// <summary>Optional override for the number of guaranteed category drops.</summary>
	[DataMember(Order = 11)]
	public int? GuaranteedDropAmount { get; set; }

	/// <summary>
	/// Component type names (assembly-qualified or simple names) attached to the
	/// runtime template before it is instantiated. The Game Adapter resolves
	/// the types from loaded assemblies and refuses non-Component types.
	/// </summary>
	[DataMember(Order = 12)]
	public List<string> SpawnComponents { get; set; } = [];

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	[DataMember(Order = 13)]
	public Dictionary<string, string> CustomData { get; set; } = [];

	/// <summary>Serialize this definition into the opaque payload format.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModBuildingDefinition));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize a building definition payload. Returns null when the payload is not a valid building definition.</summary>
	public static ModBuildingDefinition? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModBuildingDefinition));
			return serializer.ReadObject(stream) as ModBuildingDefinition;
		}
		catch (Exception)
		{
			// Any deserialization failure means the payload is not a valid
			// building definition under the current contract; the binder should
			// refuse it rather than fail the whole mod discovery.
			return null;
		}
	}
}
