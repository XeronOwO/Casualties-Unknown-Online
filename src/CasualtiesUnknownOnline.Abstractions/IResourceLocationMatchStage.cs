namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One pluggable completion stage a mod contributes to the console's resource
/// vocabulary. The catalog consults it only after its four built-in ranks
/// (exact canonical id, id prefix, bare path prefix, display-name prefix), so a
/// stage is purely additive: it can widen what completes, never displace what
/// the framework already completes. A stage owns no ordering and no result
/// limit — it answers one question about one entry and one prefix; the catalog
/// ranks every registered stage after the built-ins in registration order and
/// keeps its own suggestion cap.
///
/// A mod registers a stage through <see cref="IModContext.ResourceCompletion"/>,
/// which scopes it to the registering mod and owns its lifetime. A stage runs on
/// the console's own completion path, so an exception it throws is isolated by
/// the catalog: the entry it was answering for counts as "no match" and the
/// other stages still run.
///
/// The pinyin completion stage is the first consumer (it completes a Chinese
/// display name by full pinyin, initials, or mixed input).
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
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
