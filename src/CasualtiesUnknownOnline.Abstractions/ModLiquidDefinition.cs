using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The versioned, mod-authored data contract for one liquid content
/// definition. It is a plain DTO in Abstractions: no game assembly, no Unity
/// type, no Runtime dependency. The Game Adapter liquid provider decodes it and
/// maps the static fields into the vanilla <c>LiquidType</c> registry.
/// </summary>
[DataContract]
public sealed class ModLiquidDefinition
{
	/// <summary>Player-facing liquid name.</summary>
	[DataMember(Order = 1)]
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing liquid description.</summary>
	[DataMember(Order = 2)]
	public string Description { get; set; } = "";

	/// <summary>Liquid tint red component (0..1).</summary>
	[DataMember(Order = 3)]
	public float ColorR { get; set; } = 1f;

	/// <summary>Liquid tint green component (0..1).</summary>
	[DataMember(Order = 4)]
	public float ColorG { get; set; } = 1f;

	/// <summary>Liquid tint blue component (0..1).</summary>
	[DataMember(Order = 5)]
	public float ColorB { get; set; } = 1f;

	/// <summary>Liquid tint alpha component (0..1).</summary>
	[DataMember(Order = 6)]
	public float ColorA { get; set; } = 1f;

	/// <summary>Value per liter in vanilla units.</summary>
	[DataMember(Order = 7)]
	public float ValuePerLiter { get; set; }

	/// <summary>Whether the liquid can be used on skin.</summary>
	[DataMember(Order = 8)]
	public bool HealthUsable { get; set; }

	/// <summary>Whether the liquid can be injected.</summary>
	[DataMember(Order = 9)]
	public bool Injectable { get; set; }

	/// <summary>Sickness added by injection.</summary>
	[DataMember(Order = 10)]
	public float InjectionSickness { get; set; } = 1f;

	/// <summary>Reuse locale text from an item registration.</summary>
	[DataMember(Order = 11)]
	public bool LocaleFromItem { get; set; }

	/// <summary>Crafting-quality tags associated with the liquid.</summary>
	[DataMember(Order = 12)]
	public List<ModLiquidQuality> Qualities { get; set; } = [];

	/// <summary>Serialize this definition into the opaque payload format.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModLiquidDefinition));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize a liquid definition payload. Returns null when the payload is not a valid liquid definition.</summary>
	public static ModLiquidDefinition? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModLiquidDefinition));
			return serializer.ReadObject(stream) as ModLiquidDefinition;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
