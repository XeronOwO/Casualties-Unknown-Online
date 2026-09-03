namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The read-only framework-wide content ownership surface. It lets a mod ask
/// which mod registered a given content id, the same way CUCoreLib's per-kind
/// registries expose <c>TryGetOwnerModGuid</c>. The query is only over the
/// static content that mods declared through <see cref="IModContent"/>; it does
/// not interpret payloads and does not expose Runtime internals.
/// </summary>
public interface IModContentOwnerQuery
{
	/// <summary>
	/// Resolve the owning mod id for one content kind + id. Returns false when
	/// the content is absent or ambiguous (multiple mods registered the same id
	/// under the same kind); on false <paramref name="modId"/> is empty.
	/// </summary>
	bool TryGetOwner(string kind, string id, out string modId);
}
