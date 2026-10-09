using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod-authored definition of one static moodle/presentation descriptor.
/// It is a plain data object in Abstractions: no Unity type, no game type, no
/// Runtime dependency. A mod fills it in and registers it through
/// <see cref="IModContent"/>; its <see cref="Kind"/> is
/// <see cref="ModContentKind.Moodle"/>. This seam carries the presentation
/// metadata; actually feeding the vanilla moodle row is a GameAdapter/local UI
/// concern and is not implemented by this static content contract.
/// </summary>
public sealed class ModMoodleDefinition : IModContentDefinition
{
	/// <summary>The mod-scoped content id: a canonical lower-case path segment, unique within the registering mod.</summary>
	public string Id { get; set; } = "";

	/// <summary>The content kind this definition registers under - fixed by its type, never chosen by a caller.</summary>
	public string Kind => ModContentKind.Moodle;

	/// <summary>The mod-owned content schema version the framework stores verbatim (default 1).</summary>
	public int SchemaVersion { get; set; } = 1;

	/// <summary>Player-facing moodle title.</summary>
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing moodle description.</summary>
	public string Description { get; set; } = "";

	/// <summary>Vanilla moodle background tier.</summary>
	public int Intensity { get; set; } = 1;

	/// <summary>
	/// Stable icon resource/id key. CUCoreLib accepts either a Sprite or an
	/// existing MoodleManager icon id; Abstractions cannot contain Unity types,
	/// so mods use a stable string key that a later local resource/GameAdapter
	/// binding can resolve.
	/// </summary>
	public string IconId { get; set; } = "";

	/// <summary>Whether the vanilla critical glow overlay is shown.</summary>
	public bool Critical { get; set; }

	/// <summary>Whether the moodle is shown only when the player has a chip.</summary>
	public bool ChippedOnly { get; set; }

	/// <summary>Whether the moodle belongs in the main row instead of the side row.</summary>
	public bool Important { get; set; } = true;

	/// <summary>Default display hold duration when a future moodle surface consumes this definition.</summary>
	public float HoldSeconds { get; set; } = 0.75f;

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// Optional frame animation for the moodle icon. When present, the Game
	/// Adapter registers the first frame as the static icon and drives the
	/// vanilla moodle UI image from the ordered resource-path frames.
	/// </summary>
	public ModMoodleAnimation? IconAnimation { get; set; }

	/// <summary>
	/// Optional <see cref="ModStatusDefinition.ShowPerLimbMoodles"/> name
	/// template. Only used when the status feeds one row per affected limb.
	/// <c>{name}</c> is replaced by <see cref="DisplayName"/> and
	/// <c>{limb}</c> by the affected vanilla limb's short display name.
	/// Empty means the moodle title is used unchanged.
	/// </summary>
	public string LimbDisplayNameFormat { get; set; } = "";

	/// <summary>
	/// Optional per-limb description template used when a limb-scoped status
	/// shows one row per affected limb. <c>{description}</c> is replaced by
	/// <see cref="Description"/> and <c>{limb}</c> by the affected vanilla
	/// limb's short display name. Empty means the moodle description is used
	/// unchanged.
	/// </summary>
	public string LimbDescriptionFormat { get; set; } = "";

	/// <summary>
	/// Format the moodle title for one affected limb using
	/// <see cref="LimbDisplayNameFormat"/>. When no format is authored the
	/// plain <see cref="DisplayName"/> is returned. <c>{name}</c> and
	/// <c>{limb}</c> tokens are replaced by the title and the limb name.
	/// </summary>
	public string FormatLimbDisplayName(string limbName)
	{
		if (string.IsNullOrWhiteSpace(limbName) || string.IsNullOrWhiteSpace(LimbDisplayNameFormat))
		{
			return DisplayName;
		}

		return LimbDisplayNameFormat
			.Replace("{name}", DisplayName)
			.Replace("{limb}", limbName);
	}

	/// <summary>
	/// Format the moodle description for one affected limb using
	/// <see cref="LimbDescriptionFormat"/>. When no format is authored the
	/// plain <see cref="Description"/> is returned. <c>{description}</c> and
	/// <c>{limb}</c> tokens are replaced by the description and the limb name.
	/// </summary>
	public string FormatLimbDescription(string limbName)
	{
		if (string.IsNullOrWhiteSpace(limbName) || string.IsNullOrWhiteSpace(LimbDescriptionFormat))
		{
			return Description;
		}

		return LimbDescriptionFormat
			.Replace("{description}", Description)
			.Replace("{limb}", limbName);
	}

}
