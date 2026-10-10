using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One static moodle/presentation declaration. This is the contract a consumer
/// reads: it carries the presentation metadata; feeding the vanilla moodle row
/// is a GameAdapter/local UI concern and is not implemented by this static
/// content contract.
///
/// The two limb-text rules the templates feed are computed by the framework
/// from these members (<see cref="ModStatusPresentation.FormatLimbDisplayName"/>
/// and <see cref="ModStatusPresentation.FormatLimbDescription"/>), so every
/// implementation gets them without restating them.
///
/// <see cref="ModMoodleDefinition"/> is the framework's ready-made
/// implementation: use it when every value is a constant, and implement this
/// interface when one is computed.
/// </summary>
public interface IModMoodleDefinition : IModContentDefinition
{
	/// <summary>Player-facing moodle title.</summary>
	string DisplayName { get; }

	/// <summary>Player-facing moodle description.</summary>
	string Description { get; }

	/// <summary>Vanilla moodle background tier.</summary>
	int Intensity { get; }

	/// <summary>
	/// Stable icon resource/id key. CUCoreLib accepts either a Sprite or an
	/// existing MoodleManager icon id; Abstractions cannot contain Unity types,
	/// so mods use a stable string key that a later local resource/GameAdapter
	/// binding can resolve.
	/// </summary>
	string IconId { get; }

	/// <summary>Whether the vanilla critical glow overlay is shown.</summary>
	bool Critical { get; }

	/// <summary>Whether the moodle is shown only when the player has a chip.</summary>
	bool ChippedOnly { get; }

	/// <summary>Whether the moodle belongs in the main row instead of the side row.</summary>
	bool Important { get; }

	/// <summary>Default display hold duration when a future moodle surface consumes this declaration.</summary>
	float HoldSeconds { get; }

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	Dictionary<string, string> CustomData { get; }

	/// <summary>
	/// Optional frame animation for the moodle icon. When present, the Game
	/// Adapter registers the first frame as the static icon and drives the
	/// vanilla moodle UI image from the ordered resource-path frames.
	/// </summary>
	IModMoodleAnimation? IconAnimation { get; }

	/// <summary>
	/// Optional limb-name template. Only used when the status feeds one row per
	/// affected limb. <c>{name}</c> is replaced by <see cref="DisplayName"/> and
	/// <c>{limb}</c> by the affected vanilla limb's short display name.
	/// Empty means the moodle title is used unchanged.
	/// </summary>
	string LimbDisplayNameFormat { get; }

	/// <summary>
	/// Optional per-limb description template used when a limb-scoped status
	/// shows one row per affected limb. <c>{description}</c> is replaced by
	/// <see cref="Description"/> and <c>{limb}</c> by the affected vanilla
	/// limb's short display name. Empty means the moodle description is used
	/// unchanged.
	/// </summary>
	string LimbDescriptionFormat { get; }
}
