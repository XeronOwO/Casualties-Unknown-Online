namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The categories of content-catalog conflicts the runtime can detect without
/// interpreting mod payloads. The catalog is a read-only base: it reports
/// ambiguity but does not choose an owner or drop entries.
/// </summary>
public enum ModContentConflictKind
{
	/// <summary>Two or more mods registered the same kind + id.</summary>
	DuplicateId,

	/// <summary>The same kind + id was registered with different schema versions.</summary>
	VersionMismatch,
}
