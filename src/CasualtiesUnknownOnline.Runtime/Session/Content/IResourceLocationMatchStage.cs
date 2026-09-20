namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// One pluggable completion-matching stage the catalog consults after its
/// built-in ranks (exact canonical id, id prefix, bare path prefix, display-name
/// prefix). A stage owns no ordering and no result limit — it only answers
/// "does this entry match this prefix?"; the catalog ranks every registered
/// stage after the built-ins in registration order and keeps
/// <see cref="ResourceLocationCatalog.MaxSuggestions"/> as the cap.
///
/// The pinyin stage is the first implementation: the alternative — teaching the
/// catalog about pinyin — would put a switch-dependent matcher inside the
/// ranking contract that every other completion rule shares.
/// </summary>
public interface IResourceLocationMatchStage
{
	/// <summary>
	/// True when <paramref name="entry"/> matches <paramref name="prefix"/> for
	/// this stage. The prefix is already trimmed and lower-cased by the catalog,
	/// and an empty prefix never reaches a stage (it takes the catalog's own
	/// "first entries" path).
	/// </summary>
	bool Matches(ResourceLocationEntry entry, string prefix);
}
