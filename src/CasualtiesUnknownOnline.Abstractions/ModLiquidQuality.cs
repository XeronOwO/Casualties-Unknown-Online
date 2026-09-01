using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One crafting-quality tag inside a <see cref="ModLiquidDefinition"/>.
/// </summary>
[DataContract]
public sealed class ModLiquidQuality
{
	/// <summary>The crafting-quality id.</summary>
	[DataMember(Order = 1)]
	public string Id { get; set; } = "";

	/// <summary>The quality amount.</summary>
	[DataMember(Order = 2)]
	public float Amount { get; set; } = 1f;
}
