namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The pure safety rails for the mod content registry. Content definitions are
/// opaque and process-local, but without caps a broken or hostile mod could
/// grow the framework's memory without bound or poison the registry with
/// unusable ids. Ids/kinds are bounded by length/count, payloads by size — all
/// errors are refused with a log, never silently truncated.
/// </summary>
internal static class ModContentPolicy
{
	public const int MaxIdLength = 128;
	public const int MaxKindLength = 64;
	public const int MaxDefinitionBytes = 64 * 1024;
	public const int MaxDefinitionsPerMod = 1024;

	/// <summary>Content ids must be non-empty, not all whitespace, and within the length cap.</summary>
	public static bool IsValidId(string? id) =>
		!string.IsNullOrWhiteSpace(id) && id!.Length <= MaxIdLength;

	/// <summary>Content kinds must be non-empty, not all whitespace, and within the length cap.</summary>
	public static bool IsValidKind(string? kind) =>
		!string.IsNullOrWhiteSpace(kind) && kind!.Length <= MaxKindLength;

	/// <summary>Payloads must be non-null and within the per-definition size cap (empty byte[] is valid).</summary>
	public static bool IsValidData(byte[]? data) =>
		data is not null && data.Length <= MaxDefinitionBytes;

	/// <summary>Content schema versions must be positive; the framework never invents a version.</summary>
	public static bool IsValidSchemaVersion(int schemaVersion) => schemaVersion > 0;

	/// <summary>Adding a brand-new definition must not exceed the per-mod count cap.</summary>
	public static bool CanAdd(int currentCount) => currentCount < MaxDefinitionsPerMod;
}
