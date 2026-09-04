using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The versioned, mod-authored data contract for one static moodle/presentation
/// descriptor. It is a plain data object in Abstractions: no Unity type, no
/// game type, no Runtime dependency. The payload is registered through the
/// opaque <see cref="IModContent"/> channel with
/// <see cref="ModContentKind.Moodle"/>. This seam carries the presentation
/// metadata; actually feeding the vanilla moodle row is a GameAdapter/local UI
/// concern and is not implemented by this static content contract.
/// </summary>
[DataContract]
public sealed class ModMoodleDefinition
{
	/// <summary>Player-facing moodle title.</summary>
	[DataMember(Order = 1)]
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing moodle description.</summary>
	[DataMember(Order = 2)]
	public string Description { get; set; } = "";

	/// <summary>Vanilla moodle background tier.</summary>
	[DataMember(Order = 3)]
	public int Intensity { get; set; } = 1;

	/// <summary>
	/// Stable icon resource/id key. CUCoreLib accepts either a Sprite or an
	/// existing MoodleManager icon id; Abstractions cannot contain Unity types,
	/// so mods use a stable string key that a later local resource/GameAdapter
	/// binding can resolve.
	/// </summary>
	[DataMember(Order = 4)]
	public string IconId { get; set; } = "";

	/// <summary>Whether the vanilla critical glow overlay is shown.</summary>
	[DataMember(Order = 5)]
	public bool Critical { get; set; }

	/// <summary>Whether the moodle is shown only when the player has a chip.</summary>
	[DataMember(Order = 6)]
	public bool ChippedOnly { get; set; }

	/// <summary>Whether the moodle belongs in the main row instead of the side row.</summary>
	[DataMember(Order = 7)]
	public bool Important { get; set; } = true;

	/// <summary>Default display hold duration when a future moodle surface consumes this definition.</summary>
	[DataMember(Order = 8)]
	public float HoldSeconds { get; set; } = 0.75f;

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	[DataMember(Order = 9)]
	public Dictionary<string, string> CustomData { get; set; } = [];

	/// <summary>
	/// Optional frame animation for the moodle icon. When present, the Game
	/// Adapter registers the first frame as the static icon and drives the
	/// vanilla moodle UI image from the ordered resource-path frames.
	/// </summary>
	[DataMember(Order = 10)]
	public ModMoodleAnimation? IconAnimation { get; set; }

	/// <summary>
	/// Optional <see cref="ModStatusDefinition.ShowPerLimbMoodles"/> name
	/// template. Only used when the status feeds one row per affected limb.
	/// <c>{name}</c> is replaced by <see cref="DisplayName"/> and
	/// <c>{limb}</c> by the affected vanilla limb's short display name.
	/// Empty means the moodle title is used unchanged.
	/// </summary>
	[DataMember(Order = 11)]
	public string LimbDisplayNameFormat { get; set; } = "";

	/// <summary>
	/// Optional per-limb description template used when a limb-scoped status
	/// shows one row per affected limb. <c>{description}</c> is replaced by
	/// <see cref="Description"/> and <c>{limb}</c> by the affected vanilla
	/// limb's short display name. Empty means the moodle description is used
	/// unchanged.
	/// </summary>
	[DataMember(Order = 12)]
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

	/// <summary>Serialize this definition into the opaque payload format.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModMoodleDefinition));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize a moodle definition payload. Returns null when the payload is not a valid moodle definition.</summary>
	public static ModMoodleDefinition? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModMoodleDefinition));
			return serializer.ReadObject(stream) as ModMoodleDefinition;
		}
		catch (Exception)
		{
			// Any deserialization failure means the payload is not a valid
			// moodle definition under the current contract; the binder should
			// refuse it rather than fail the whole mod discovery.
			return null;
		}
	}
}
