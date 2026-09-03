using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Container behavior for a custom item. The values are plain data in
/// Abstractions: no Unity type, no game type, no Runtime dependency. The Game
/// Adapter maps them onto the vanilla <c>Container</c> component when it builds
/// the runtime item template.
/// </summary>
[DataContract]
public sealed class ModItemContainer
{
	/// <summary>Maximum total weight the container can hold.</summary>
	[DataMember(Order = 1)]
	public float Capacity { get; set; } = 10f;

	/// <summary>Maximum weight allowed for one contained item.</summary>
	[DataMember(Order = 2)]
	public float MaxWeightPerItem { get; set; } = 5f;

	/// <summary>Encumbrance multiplier applied to contained items (1 = normal).</summary>
	[DataMember(Order = 3)]
	public float EncumbranceReduction { get; set; } = 1f;

	/// <summary>Whether contained items stay visually visible while inside.</summary>
	[DataMember(Order = 4)]
	public bool ItemsVisible { get; set; }

	/// <summary>Optional item-tag restriction. Empty means every item is accepted.</summary>
	[DataMember(Order = 5)]
	public List<string> TagRestriction { get; set; } = [];
}
