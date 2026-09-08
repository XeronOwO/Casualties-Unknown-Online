using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// The console's content-driven resource vocabulary. It merges every
/// <see cref="IResourceLocationSource"/> and answers completion queries by
/// canonical id, bare path, and display name. The console maps the returned
/// entries to <c>CommandSuggestion</c> rows; it owns no matching policy itself.
/// </summary>
public interface IResourceLocationCatalog
{
	/// <summary>Every merged entry, deduplicated by canonical id and ordered by id.</summary>
	IReadOnlyList<ResourceLocationEntry> Entries { get; }

	/// <summary>
	/// Completion candidates for a partially typed resource argument. The
	/// returned entries always carry the canonical id — never the raw input
	/// alias — ranked exact id, id prefix, bare path prefix, display-name
	/// prefix, then id order, capped by the implementation's result limit.
	/// </summary>
	IReadOnlyList<ResourceLocationEntry> Suggest(string prefix);
}
