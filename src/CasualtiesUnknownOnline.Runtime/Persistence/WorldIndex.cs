using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// <c>index.json</c> (§3.1): a cache for the world picker plus the last-opened
/// pointer. It is never the source of truth — a world folder that exists on disk
/// but is missing from the index is still listed, and an entry whose folder is
/// gone is dropped (see <see cref="WorldRepository"/>).
/// </summary>
public sealed class WorldIndex
{
	/// <summary>The schema version this build reads and writes; an unrecognised index is rebuilt from disk rather than guessed.</summary>
	public const int CurrentSchemaVersion = 1;

	public int SchemaVersion { get; init; } = CurrentSchemaVersion;

	public List<WorldEntry> Worlds { get; init; } = [];

	/// <summary>The world the player opened last; empty when none has been opened yet.</summary>
	public string LastOpenedWorldId { get; init; } = string.Empty;

	/// <summary>One picker row. Every field is a copy of <c>world.json</c> plus the live counters.</summary>
	public sealed record WorldEntry(
		string WorldId,
		string DisplayName,
		string LastSavedUtc,
		string LastKind,
		int LayerIndex,
		int PlayerCount);
}
