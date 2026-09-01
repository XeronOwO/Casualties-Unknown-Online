using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The versioned, mod-authored data contract for one recipe content
/// definition. It is a plain DTO in Abstractions: no game assembly, no Unity
/// type, no Runtime dependency. The Game Adapter recipe provider decodes it and
/// materializes the corresponding vanilla <c>Recipe</c> object once the game's
/// recipe table is ready.
/// </summary>
[DataContract]
public sealed class ModRecipeDefinition
{
	/// <summary>The item/liquid id produced by the recipe.</summary>
	[DataMember(Order = 1)]
	public string ResultItemId { get; set; } = "";

	/// <summary>True when the result is a liquid (not an item prefab).</summary>
	[DataMember(Order = 2)]
	public bool ResultIsLiquid { get; set; }

	/// <summary>Result stack amount (defaults to 1).</summary>
	[DataMember(Order = 3)]
	public int ResultAmount { get; set; } = 1;

	/// <summary>Result condition/amount fraction applied at spawn time.</summary>
	[DataMember(Order = 4)]
	public float ResultCondition { get; set; } = 1f;

	/// <summary>True when the result should keep its container-liquid contents.</summary>
	[DataMember(Order = 5)]
	public bool DontDrainResultLiquid { get; set; }

	/// <summary>The intelligence requirement for this recipe (0 makes it visible early).</summary>
	[DataMember(Order = 6)]
	public int Intelligence { get; set; }

	/// <summary>The recipe category (see <see cref="ModRecipeCategory"/>).</summary>
	[DataMember(Order = 7)]
	public string Category { get; set; } = ModRecipeCategory.Materials;

	/// <summary>True when this is a repair recipe (the result id is allowed as an ingredient).</summary>
	[DataMember(Order = 8)]
	public bool IsRepair { get; set; }

	/// <summary>The ordered ingredient requirements.</summary>
	[DataMember(Order = 9)]
	public List<ModRecipeIngredient> Ingredients { get; set; } = [];

	/// <summary>Serialize this definition into the opaque payload format.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModRecipeDefinition));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize a recipe definition payload. Returns null when the payload is not a valid recipe definition.</summary>
	public static ModRecipeDefinition? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModRecipeDefinition));
			return serializer.ReadObject(stream) as ModRecipeDefinition;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
