using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework's ready-made <see cref="IModItemDefinition"/>: a plain data
/// object in Abstractions with no game type, no Unity type and no Runtime
/// dependency, which is also why it cannot compute a member. Use it when every
/// value is a constant; implement the interface when one is computed.
///
/// It stays constructible with no arguments and every member stays settable on
/// purpose: the framework reads the interface, and a future data-driven loader
/// (a JSON content pack) would deserialize into this concrete type.
/// </summary>
public sealed class ModItemDefinition : IModItemDefinition
{
	/// <inheritdoc />
	public string Id { get; set; } = "";

	/// <inheritdoc />
	public string Kind => ModContentKind.Item;

	/// <inheritdoc />
	public int SchemaVersion { get; set; } = 1;

	/// <inheritdoc />
	public string DisplayName { get; set; } = "";

	/// <inheritdoc />
	public string Description { get; set; } = "";

	/// <inheritdoc />
	public string Category { get; set; } = "nospawn";

	/// <inheritdoc />
	public float Weight { get; set; }

	/// <inheritdoc />
	public int Value { get; set; }

	/// <inheritdoc />
	public bool Usable { get; set; }

	/// <inheritdoc />
	public bool UsableWithLmb { get; set; }

	/// <inheritdoc />
	public bool DestroyAtZeroCondition { get; set; }

	/// <inheritdoc />
	public string Tags { get; set; } = "";

	/// <inheritdoc />
	public int SpawnFrequency { get; set; } = 1;

	/// <inheritdoc />
	public string TemplateId { get; set; } = "";

	/// <inheritdoc />
	public List<string> SpawnComponents
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public float? WorldSpawnPerChunk { get; set; }

	/// <inheritdoc />
	public ModItemDropSource? DropSources { get; set; }

	/// <inheritdoc />
	public IModItemContainer? Container { get; set; }

	/// <inheritdoc />
	public IModItemBattery? Battery { get; set; }

	/// <inheritdoc />
	public IModItemLight? Light { get; set; }

	/// <inheritdoc />
	public IModItemTool? Tool { get; set; }

	/// <inheritdoc />
	public IModItemGun? Gun { get; set; }

	/// <inheritdoc />
	public IModItemWearable? Wearable { get; set; }

	/// <inheritdoc />
	public float DecayMinutes { get; set; }

	/// <inheritdoc />
	public IModItemVisual? Visual { get; set; }

	/// <inheritdoc />
	public List<IModCraftingQuality> Qualities
	{
		get;
		set => field = value ?? [];
	} = [];
}
