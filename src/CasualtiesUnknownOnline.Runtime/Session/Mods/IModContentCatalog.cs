using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The runtime read-only content catalog. It is the base layer between the
/// mod-facing opaque <c>IModContent</c> registry and future native-content
/// binding: it can enumerate, filter, resolve, and report schema/ownership
/// conflicts without interpreting any mod payload. It is NOT a mod-facing
/// surface and never exposes Runtime internals back to mods.
/// </summary>
public interface IModContentCatalog
{
	/// <summary>A snapshot of every mod's registered content definitions (copy — safe to hold).</summary>
	IReadOnlyList<ModContentRegistration> Entries { get; }

	/// <summary>All content entries with the exact kind, in discovery/registration order.</summary>
	IReadOnlyList<ModContentRegistration> OfKind(string kind);

	/// <summary>True when the current catalog contains at least one conflict.</summary>
	bool HasConflicts { get; }

	/// <summary>The current conflict diagnostics (duplicate ids and schema-version mismatches).</summary>
	IReadOnlyList<ModContentConflict> Conflicts { get; }

	/// <summary>
	/// Resolve a kind + id to a single registration. Returns false when the id is
	/// absent OR ambiguous (multiple owners); ambiguity is reported through
	/// <see cref="Conflicts"/>.
	/// </summary>
	bool TryResolve(string kind, string id, out ModContentRegistration? entry);
}
