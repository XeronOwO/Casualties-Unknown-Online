namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One addressable resource/content entry in the console's resource vocabulary:
/// what a completion stage is asked about, and what the console turns into a
/// suggestion row. <see cref="Id"/> is always the canonical
/// <c>namespace:path</c> id and never the text the player typed, so an accepted
/// suggestion is always canonical; <see cref="DisplayName"/> is whatever the
/// owning source knows (for vanilla items that is the game-localised item name,
/// so a Chinese client can complete by its own language), and it may be empty.
///
/// It is a read-only value object the framework builds and a stage only reads,
/// and it deliberately carries no game/Unity type.
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public sealed class ResourceLocationEntry(ContentId id, string kind, string displayName)
{
	/// <summary>The canonical <c>namespace:path</c> id — never the typed alias.</summary>
	public ContentId Id { get; } = id;

	/// <summary>The owning content kind (for example <see cref="ModContentKind.Item"/>).</summary>
	public string Kind { get; } = kind;

	/// <summary>The source's display name, or an empty string when the source has none.</summary>
	public string DisplayName { get; } = displayName;
}
