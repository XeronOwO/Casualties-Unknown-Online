using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One logical liquid declaration. This is the contract a consumer reads: the
/// Game Adapter liquid provider maps these members into the vanilla
/// <c>LiquidType</c> registry.
///
/// <see cref="ModLiquidDefinition"/> is the framework's ready-made
/// implementation: use it when every value is a constant, and implement this
/// interface when one is computed.
/// </summary>
public interface IModLiquidDefinition : IModContentDefinition
{
	/// <summary>Player-facing liquid name.</summary>
	string DisplayName { get; }

	/// <summary>Player-facing liquid description.</summary>
	string Description { get; }

	/// <summary>Liquid tint red component (0..1).</summary>
	float ColorR { get; }

	/// <summary>Liquid tint green component (0..1).</summary>
	float ColorG { get; }

	/// <summary>Liquid tint blue component (0..1).</summary>
	float ColorB { get; }

	/// <summary>Liquid tint alpha component (0..1).</summary>
	float ColorA { get; }

	/// <summary>Value per liter in vanilla units.</summary>
	float ValuePerLiter { get; }

	/// <summary>Whether the liquid can be used on skin.</summary>
	bool HealthUsable { get; }

	/// <summary>Whether the liquid can be injected.</summary>
	bool Injectable { get; }

	/// <summary>Sickness added by injection.</summary>
	float InjectionSickness { get; }

	/// <summary>Reuse locale text from an item registration.</summary>
	bool LocaleFromItem { get; }

	/// <summary>Crafting-quality labels the liquid provides, matched by quality-based recipes.</summary>
	List<ModCraftingQuality> Qualities { get; }
}
