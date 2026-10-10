namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One static per-limb moodle routing entry for a limb-scoped status declaration.
/// This is the contract a consumer reads: <see cref="IModStatusDefinition.LimbMoodles"/>
/// is a list of these, so a mod that computes a binding hands over its own
/// implementation instead of filling in the framework's.
///
/// <see cref="ModLimbMoodleBinding"/> is the framework's ready-made
/// implementation: use it when every value is a constant, and implement this
/// interface when one is computed. Partial customisation is composition — an
/// implementation hands back a filled default for the members it does not touch —
/// never inheritance from the data class, which stays <c>sealed</c>.
///
/// The mapping is pure content data: resolving it against the local body, and the
/// case-insensitive limb comparison that does so, is the framework's rule
/// (<see cref="ModStatusPresentation.ResolveMoodleId"/>).
/// </summary>
public interface IModLimbMoodleBinding
{
	/// <summary>Stable vanilla limb name, case-insensitive (e.g. <c>Head</c>, <c>LeftArm</c>).</summary>
	string LimbName { get; }

	/// <summary>Id of the <see cref="ModMoodleDefinition"/> to show for this limb.</summary>
	string MoodleId { get; }
}
