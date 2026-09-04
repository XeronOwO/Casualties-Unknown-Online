using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// A static per-limb moodle routing entry for a limb-scoped
/// <see cref="ModStatusDefinition"/>. It maps a stable vanilla limb name (the
/// same name used by the game's <c>Body.LimbByName</c> lookup) to a
/// <see cref="ModMoodleDefinition"/> id. The mapping is pure content data:
/// the Game Adapter resolves it against the local body at presentation time
/// without exposing <c>Limb</c> or any Unity type through Abstractions.
/// </summary>
[DataContract]
public sealed class ModLimbMoodleBinding
{
	/// <summary>Stable vanilla limb name, case-insensitive (e.g. <c>Head</c>, <c>LeftArm</c>).</summary>
	[DataMember(Order = 1)]
	public string LimbName { get; set; } = "";

	/// <summary>Id of the <see cref="ModMoodleDefinition"/> to show for this limb.</summary>
	[DataMember(Order = 2)]
	public string MoodleId { get; set; } = "";
}
