namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// <c>world.json</c> (§3.3): the player-facing metadata that must survive
/// without reading a snapshot. The folder key is <see cref="WorldId"/> and never
/// changes; <see cref="DisplayName"/> is renamed freely.
/// </summary>
public sealed class WorldMetadata
{
	public string WorldId { get; init; } = string.Empty;

	public string DisplayName { get; init; } = string.Empty;

	/// <summary>Derived from the world id's date component, which is what the id was created from.</summary>
	public string CreatedAtUtc { get; init; } = string.Empty;

	/// <summary>Starts as the creation instant and advances on every committed snapshot.</summary>
	public string LastSavedUtc { get; init; } = string.Empty;

	public string LastKind { get; init; } = string.Empty;

	public int LayerIndex { get; init; }

	public int PlayerCount { get; init; }

	public string RunEpoch { get; init; } = string.Empty;

	public int SaveCount { get; init; }

	public int BackupCount { get; init; }
}
