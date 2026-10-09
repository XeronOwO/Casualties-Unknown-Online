using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework's ready-made <see cref="IModLiquidDefinition"/>: a plain data
/// object in Abstractions with no game assembly, no Unity type and no Runtime
/// dependency, which is also why it cannot compute a member. Use it when every
/// value is a constant; implement the interface when one is computed.
///
/// It stays constructible with no arguments and every member stays settable on
/// purpose: the framework reads the interface, and a future data-driven loader
/// would deserialize into this concrete type.
/// </summary>
public sealed class ModLiquidDefinition : IModLiquidDefinition
{
	/// <inheritdoc />
	public string Id { get; set; } = "";

	/// <inheritdoc />
	public string Kind => ModContentKind.Liquid;

	/// <inheritdoc />
	public int SchemaVersion { get; set; } = 1;

	/// <inheritdoc />
	public string DisplayName { get; set; } = "";

	/// <inheritdoc />
	public string Description { get; set; } = "";

	/// <inheritdoc />
	public float ColorR { get; set; } = 1f;

	/// <inheritdoc />
	public float ColorG { get; set; } = 1f;

	/// <inheritdoc />
	public float ColorB { get; set; } = 1f;

	/// <inheritdoc />
	public float ColorA { get; set; } = 1f;

	/// <inheritdoc />
	public float ValuePerLiter { get; set; }

	/// <inheritdoc />
	public bool HealthUsable { get; set; }

	/// <inheritdoc />
	public bool Injectable { get; set; }

	/// <inheritdoc />
	public float InjectionSickness { get; set; } = 1f;

	/// <inheritdoc />
	public bool LocaleFromItem { get; set; }

	/// <inheritdoc />
	public List<ModCraftingQuality> Qualities
	{
		get;
		set => field = value ?? [];
	} = [];
}
