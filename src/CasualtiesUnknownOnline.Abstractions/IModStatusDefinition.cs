using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One static status-descriptor declaration. This is the contract a consumer
/// reads: it describes the status type and its presentation/save metadata; the
/// per-player/per-limb runtime values belong to the mod-data domain and are
/// deliberately not part of this contract.
///
/// The derived rules a declaration feeds — whether it asks for one moodle row
/// per affected limb, and which moodle one limb resolves to — are computed by
/// the framework from these members (<see cref="ModStatusPresentation"/>), so
/// every implementation gets them without restating them.
///
/// <see cref="ModStatusDefinition"/> is the framework's ready-made
/// implementation: use it when every value is a constant, and implement this
/// interface when one is computed.
/// </summary>
public interface IModStatusDefinition : IModContentDefinition
{
	/// <summary>Player-facing status name.</summary>
	string DisplayName { get; }

	/// <summary>Player-facing status description.</summary>
	string Description { get; }

	/// <summary>Whether the status is body-level or per-limb.</summary>
	ModStatusScope Scope { get; }

	/// <summary>
	/// Whether a future status runtime may persist this status value in a
	/// mod-owned save payload. Static metadata only; no save is implemented by
	/// this seam.
	/// </summary>
	bool SaveEnabled { get; }

	/// <summary>Optional id of a <see cref="ModMoodleDefinition"/> used to present this status.</summary>
	string MoodleId { get; }

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	Dictionary<string, string> CustomData { get; }

	/// <summary>
	/// When true for a limb-scoped status, the local vanilla moodle row shows
	/// one moodle per affected limb instead of collapsing all limb presences
	/// into a single row. Only meaningful with
	/// <see cref="ModStatusScope.Limb"/>.
	/// </summary>
	bool ShowPerLimbMoodles { get; }

	/// <summary>
	/// Optional static per-limb moodle routing entries. Each maps a vanilla
	/// limb name to a <see cref="ModMoodleDefinition"/> id. Entries are only
	/// used when <see cref="ShowPerLimbMoodles"/> is true and the status is
	/// limb-scoped; a limb without a matching entry falls back to
	/// <see cref="MoodleId"/>.
	/// </summary>
	List<ModLimbMoodleBinding> LimbMoodles { get; }
}
