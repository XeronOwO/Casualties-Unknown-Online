using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework's ready-made <see cref="IModStatusDefinition"/>: a plain data
/// object in Abstractions with no Unity type, no game type and no Runtime
/// dependency, which is also why it cannot compute a member. Use it when every
/// value is a constant; implement the interface when one is computed.
///
/// It stays constructible with no arguments and every member stays settable on
/// purpose: the framework reads the interface, and a future data-driven loader
/// would deserialize into this concrete type. The presentation rules a status
/// feeds are <see cref="ModStatusPresentation"/>.
/// </summary>
public sealed class ModStatusDefinition : IModStatusDefinition
{
	/// <inheritdoc />
	public string Id { get; set; } = "";

	/// <inheritdoc />
	public string Kind => ModContentKind.Status;

	/// <inheritdoc />
	public int SchemaVersion { get; set; } = 1;

	/// <inheritdoc />
	public string DisplayName { get; set; } = "";

	/// <inheritdoc />
	public string Description { get; set; } = "";

	/// <inheritdoc />
	public ModStatusScope Scope { get; set; } = ModStatusScope.Body;

	/// <inheritdoc />
	public bool SaveEnabled { get; set; } = true;

	/// <inheritdoc />
	public string MoodleId { get; set; } = "";

	/// <inheritdoc />
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public bool ShowPerLimbMoodles { get; set; }

	/// <inheritdoc />
	public List<IModLimbMoodleBinding> LimbMoodles
	{
		get;
		set => field = value ?? [];
	} = [];
}
