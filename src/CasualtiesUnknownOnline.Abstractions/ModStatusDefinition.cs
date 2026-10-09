using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod-authored definition of one static status descriptor. It is a plain
/// data object in Abstractions: no Unity type, no game type, no Runtime
/// dependency. A mod fills it in and registers it through
/// <see cref="IModContent"/>; its <see cref="Kind"/> is
/// <see cref="ModContentKind.Status"/>. This seam describes the status type
/// and its presentation/save metadata; the per-player/per-limb runtime values
/// belong to a future typed mod-data domain and are deliberately not part of
/// this contract.
/// </summary>
public sealed class ModStatusDefinition : IModContentDefinition
{
	/// <summary>The mod-scoped content id: a canonical lower-case path segment, unique within the registering mod.</summary>
	public string Id { get; set; } = "";

	/// <summary>The content kind this definition registers under - fixed by its type, never chosen by a caller.</summary>
	public string Kind => ModContentKind.Status;

	/// <summary>The mod-owned content schema version the framework stores verbatim (default 1).</summary>
	public int SchemaVersion { get; set; } = 1;

	/// <summary>Player-facing status name.</summary>
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing status description.</summary>
	public string Description { get; set; } = "";

	/// <summary>Whether the status is body-level or per-limb.</summary>
	public ModStatusScope Scope { get; set; } = ModStatusScope.Body;

	/// <summary>
	/// Whether a future status runtime may persist this status value in a
	/// mod-owned save payload. Static metadata only; no save is implemented by
	/// this seam.
	/// </summary>
	public bool SaveEnabled { get; set; } = true;

	/// <summary>Optional id of a <see cref="ModMoodleDefinition"/> used to present this status.</summary>
	public string MoodleId { get; set; } = "";

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// When true for a limb-scoped status, the local vanilla moodle row shows
	/// one moodle per affected limb instead of collapsing all limb presences
	/// into a single row. Only meaningful with
	/// <see cref="ModStatusScope.Limb"/>.
	/// </summary>
	public bool ShowPerLimbMoodles { get; set; }

	/// <summary>
	/// Optional static per-limb moodle routing entries. Each maps a vanilla
	/// limb name to a <see cref="ModMoodleDefinition"/> id. Entries are only
	/// used when <see cref="ShowPerLimbMoodles"/> is true and the status is
	/// limb-scoped; a limb without a matching entry falls back to
	/// <see cref="MoodleId"/>.
	/// </summary>
	public List<ModLimbMoodleBinding> LimbMoodles
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// Resolve the moodle id that should be presented for one affected limb.
	/// Returns <see cref="MoodleId"/> for body-scoped statuses, when
	/// per-limb rows are disabled, or when no authored
	/// <see cref="LimbMoodles"/> entry matches the limb name.
	/// </summary>
	public string ResolveMoodleId(string? limbName)
	{
		if (!ShowsPerLimbMoodles || limbName is null)
		{
			return MoodleId;
		}

		foreach (var binding in LimbMoodles)
		{
			if (binding is null)
			{
				continue;
			}

			if (string.Equals(binding.LimbName, limbName, StringComparison.OrdinalIgnoreCase))
			{
				return binding.MoodleId;
			}
		}

		return MoodleId;
	}

	/// <summary>Whether this status asks for one moodle row per affected limb.</summary>
	public bool ShowsPerLimbMoodles => Scope == ModStatusScope.Limb && ShowPerLimbMoodles;

}
