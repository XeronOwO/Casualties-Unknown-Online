using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework's ready-made <see cref="IModStructureDefinition"/>: a plain
/// data object in Abstractions with no Unity type, no game type and no Runtime
/// dependency, which is also why it cannot compute a member. Use it when every
/// value is a constant; implement the interface when one is computed.
///
/// It stays constructible with no arguments and every member stays settable on
/// purpose: the framework reads the interface, and a future data-driven loader
/// would deserialize into this concrete type. The rule that reads the depth
/// table is <see cref="ModStructureDistribution"/>.
/// </summary>
public sealed class ModStructureDefinition : IModStructureDefinition
{
	/// <inheritdoc />
	public string Id { get; set; } = "";

	/// <inheritdoc />
	public string Kind => ModContentKind.Structure;

	/// <inheritdoc />
	public int SchemaVersion { get; set; } = 1;

	/// <inheritdoc />
	public string DisplayName { get; set; } = "";

	/// <inheritdoc />
	public string Description { get; set; } = "";

	/// <inheritdoc />
	public int Width { get; set; } = 1;

	/// <inheritdoc />
	public int Height { get; set; } = 1;

	/// <inheritdoc />
	public List<string> Rows
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public Dictionary<string, int> VanillaBlocks
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public Dictionary<string, string> TileIds
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public List<int> SpawnCounts
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
}
