using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod-authored definition of one liquid content definition. It is a plain
/// DTO in Abstractions: no game assembly, no Unity type, no Runtime dependency.
/// The Game Adapter liquid provider reads it and maps the static fields into
/// the vanilla <c>LiquidType</c> registry.
/// </summary>
public sealed class ModLiquidDefinition : IModContentDefinition
{
	/// <summary>The mod-scoped content id: a canonical lower-case path segment, unique within the registering mod.</summary>
	public string Id { get; set; } = "";

	/// <summary>The content kind this definition registers under - fixed by its type, never chosen by a caller.</summary>
	public string Kind => ModContentKind.Liquid;

	/// <summary>The mod-owned content schema version the framework stores verbatim (default 1).</summary>
	public int SchemaVersion { get; set; } = 1;

	/// <summary>Player-facing liquid name.</summary>
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing liquid description.</summary>
	public string Description { get; set; } = "";

	/// <summary>Liquid tint red component (0..1).</summary>
	public float ColorR { get; set; } = 1f;

	/// <summary>Liquid tint green component (0..1).</summary>
	public float ColorG { get; set; } = 1f;

	/// <summary>Liquid tint blue component (0..1).</summary>
	public float ColorB { get; set; } = 1f;

	/// <summary>Liquid tint alpha component (0..1).</summary>
	public float ColorA { get; set; } = 1f;

	/// <summary>Value per liter in vanilla units.</summary>
	public float ValuePerLiter { get; set; }

	/// <summary>Whether the liquid can be used on skin.</summary>
	public bool HealthUsable { get; set; }

	/// <summary>Whether the liquid can be injected.</summary>
	public bool Injectable { get; set; }

	/// <summary>Sickness added by injection.</summary>
	public float InjectionSickness { get; set; } = 1f;

	/// <summary>Reuse locale text from an item registration.</summary>
	public bool LocaleFromItem { get; set; }

	/// <summary>Crafting-quality labels the liquid provides, matched by quality-based recipes.</summary>
	public List<ModCraftingQuality> Qualities
	{
		get;
		set => field = value ?? [];
	} = [];

}
