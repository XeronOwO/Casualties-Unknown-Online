using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework's ready-made <see cref="IModMoodleDefinition"/>: a plain data
/// object in Abstractions with no Unity type, no game type and no Runtime
/// dependency, which is also why it cannot compute a member. Use it when every
/// value is a constant; implement the interface when one is computed.
///
/// It stays constructible with no arguments and every member stays settable on
/// purpose: the framework reads the interface, and a future data-driven loader
/// would deserialize into this concrete type. The limb-text rules the templates
/// feed are <see cref="ModStatusPresentation"/>.
/// </summary>
public sealed class ModMoodleDefinition : IModMoodleDefinition
{
	/// <inheritdoc />
	public string Id { get; set; } = "";

	/// <inheritdoc />
	public string Kind => ModContentKind.Moodle;

	/// <inheritdoc />
	public int SchemaVersion { get; set; } = 1;

	/// <inheritdoc />
	public string DisplayName { get; set; } = "";

	/// <inheritdoc />
	public string Description { get; set; } = "";

	/// <inheritdoc />
	public int Intensity { get; set; } = 1;

	/// <inheritdoc />
	public string IconId { get; set; } = "";

	/// <inheritdoc />
	public bool Critical { get; set; }

	/// <inheritdoc />
	public bool ChippedOnly { get; set; }

	/// <inheritdoc />
	public bool Important { get; set; } = true;

	/// <inheritdoc />
	public float HoldSeconds { get; set; } = 0.75f;

	/// <inheritdoc />
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public IModMoodleAnimation? IconAnimation { get; set; }

	/// <inheritdoc />
	public string LimbDisplayNameFormat { get; set; } = "";

	/// <inheritdoc />
	public string LimbDescriptionFormat { get; set; } = "";
}
