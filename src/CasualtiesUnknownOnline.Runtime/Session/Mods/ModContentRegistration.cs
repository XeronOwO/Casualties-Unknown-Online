using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One content entry in the framework-wide read view: the owning mod id, the
/// mod's declared content-id namespace (null for a legacy mod that declared
/// none), and the mod-scoped definition. Returning this to consumers keeps the
/// per-mod namespace explicit without leaking the ModService internals.
/// </summary>
public sealed record ModContentRegistration(string ModId, ModContentDefinition Definition, string? Namespace = null)
{
	/// <summary>
	/// The canonical <c>namespace:path</c> id of this entry. False when the mod
	/// declared no namespace, or when the registered id cannot form a valid
	/// path (discovery/registration refuse that, so this is a defensive check).
	/// </summary>
	public bool TryGetCanonicalId(out ContentId id)
	{
		id = default;
		return Namespace is not null && ContentId.TryCreate(Namespace, Definition.Id, out id);
	}
}
