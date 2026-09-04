namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The abstraction-safe request passed to a mod-authored runtime moodle
/// resolver. It never exposes a game <c>Limb</c>, <c>Body</c>, or Unity type;
/// the mod receives its own opaque status payload plus the stable status/player/
/// limb identity and returns a static moodle id (or null to fall back).
/// </summary>
public sealed class ModStatusMoodleRequest
{
	/// <summary>The mod id that owns this runtime status.</summary>
	public string ModId { get; set; } = "";

	/// <summary>The stable runtime status id this presence belongs to.</summary>
	public string StatusId { get; set; } = "";

	/// <summary>The player whose body/limb carries the status.</summary>
	public ulong PlayerSteamId { get; set; }

	/// <summary>Whether the presence is body-level or limb-level.</summary>
	public ModStatusScope Scope { get; set; }

	/// <summary>Zero-based vanilla limb slot for limb-scoped statuses; -1 for body statuses.</summary>
	public int LimbSlot { get; set; } = -1;

	/// <summary>Stable vanilla/short limb name when available, otherwise null for body statuses.</summary>
	public string? LimbName { get; set; }

	/// <summary>The mod-owned status payload (a defensive copy; may be empty when the status value is empty).</summary>
	public byte[] Payload { get; set; } = [];
}
