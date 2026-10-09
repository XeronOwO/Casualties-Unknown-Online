using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The pure safety rails for the mod content registry. Definitions are typed and
/// process-local, but without caps a broken or hostile mod could fill the
/// registry without bound or poison it with unusable ids. Ids/kinds are bounded
/// by grammar/length/count, schema versions by sign — all errors are refused
/// with a log, never silently truncated.
/// </summary>
internal static class ModContentPolicy
{
	public const int MaxKindLength = 64;
	public const int MaxDefinitionsPerMod = 1024;

	/// <summary>
	/// Content ids must already be canonical <see cref="ContentId"/> path
	/// segments: lower-case ASCII <c>[a-z0-9][a-z0-9_.-]{0,94}</c>. Upper-case,
	/// whitespace, the namespace separator, and over-length ids are refused so
	/// the framework can always derive an unambiguous <c>namespace:id</c>.
	/// </summary>
	public static bool IsValidId(string? id) => ContentId.IsValidPath(id);

	/// <summary>Content kinds must be non-empty, not all whitespace, and within the length cap.</summary>
	public static bool IsValidKind(string? kind) =>
		!string.IsNullOrWhiteSpace(kind) && kind!.Length <= MaxKindLength;

	/// <summary>Content schema versions must be positive; the framework never invents a version.</summary>
	public static bool IsValidSchemaVersion(int schemaVersion) => schemaVersion > 0;

	/// <summary>Adding a brand-new definition must not exceed the per-mod count cap.</summary>
	public static bool CanAdd(int currentCount) => currentCount < MaxDefinitionsPerMod;
}
