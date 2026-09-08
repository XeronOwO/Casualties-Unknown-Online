using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The default read-only content catalog backed by <see cref="IModContentControl"/>.
/// It deliberately performs no interpretation and makes no ownership choice:
/// it enumerates the framework-wide view, supports kind/id lookup (canonical
/// <c>namespace:path</c> ids first, legacy bare ids second), and returns
/// conflict diagnostics for future binders. The underlying registry already
/// enforces per-mod id uniqueness; this catalog adds the cross-mod view.
///
/// Conflicts are keyed by the BARE <c>kind + id</c>, not the canonical id: the
/// bare id is still the game-table key that content providers materialise, so
/// two mods in different namespaces registering the same bare id cannot both
/// exist in the game even though their canonical addresses differ. A canonical
/// id therefore always resolves uniquely, while a bare id that spans several
/// owners fails closed.
/// </summary>
public sealed class ModContentCatalog(IModContentControl control, ILogger<ModContentCatalog> log) : IModContentCatalog
{
	public IReadOnlyList<ModContentRegistration> Entries => control.Entries;

	public IReadOnlyList<ModContentRegistration> OfKind(string kind)
	{
		if (kind is null)
		{
			throw new ArgumentNullException(nameof(kind));
		}

		return [.. Entries.Where(e => string.Equals(e.Definition.Kind, kind, StringComparison.Ordinal))];
	}

	public bool HasConflicts => Conflicts.Count > 0;

	public IReadOnlyList<ModContentConflict> Conflicts => BuildConflicts();

	public bool TryResolve(string kind, string id, out ModContentRegistration? entry)
	{
		if (kind is null)
		{
			throw new ArgumentNullException(nameof(kind));
		}

		if (id is null)
		{
			throw new ArgumentNullException(nameof(id));
		}

		// Canonical namespace:path resolution is exact and cannot be ambiguous:
		// discovery guarantees one owner per namespace.
		if (ContentId.TryParse(id, out var canonical))
		{
			var canonicalMatches = Entries
				.Where(e => string.Equals(e.Definition.Kind, kind, StringComparison.Ordinal)
					&& e.TryGetCanonicalId(out var candidate)
					&& candidate == canonical)
				.ToList();
			return ResolveUnique(canonicalMatches, kind, id, out entry);
		}

		// Legacy bare id: unique match over every entry (namespaced or not)
		// preserves the pre-namespace semantics.
		var matches = Entries
			.Where(e => string.Equals(e.Definition.Kind, kind, StringComparison.Ordinal)
				&& string.Equals(e.Definition.Id, id, StringComparison.Ordinal))
			.ToList();
		return ResolveUnique(matches, kind, id, out entry);
	}

	private bool ResolveUnique(
		List<ModContentRegistration> matches,
		string kind,
		string id,
		out ModContentRegistration? entry)
	{
		if (matches.Count == 1)
		{
			entry = matches[0];
			return true;
		}

		if (matches.Count > 1)
		{
			log.LogWarning(
				"[ModContentCatalog] content {Kind}/{Id} is ambiguous — {Count} mods own it; use Conflicts for details.",
				kind, id, matches.Count);
		}

		entry = null;
		return false;
	}

	private List<ModContentConflict> BuildConflicts()
	{
		var conflicts = new List<ModContentConflict>();
		foreach (var group in Entries.GroupBy(e => (e.Definition.Kind, e.Definition.Id)))
		{
			var groupEntries = group.ToList();
			if (groupEntries.Count < 2)
			{
				continue;
			}

			var ownerModIds = groupEntries.Select(e => e.ModId).ToList();
			conflicts.Add(new ModContentConflict(
				group.Key.Kind,
				group.Key.Item2,
				ModContentConflictKind.DuplicateId,
				ownerModIds,
				$"{ownerModIds.Count} mods registered the same {group.Key.Kind} content id '{group.Key.Item2}'."));

			var versions = groupEntries
				.Select(e => e.Definition.SchemaVersion)
				.Distinct()
				.ToList();
			if (versions.Count > 1)
			{
				conflicts.Add(new ModContentConflict(
					group.Key.Kind,
					group.Key.Item2,
					ModContentConflictKind.VersionMismatch,
					ownerModIds,
					$"The same {group.Key.Kind} content id '{group.Key.Item2}' uses incompatible schema versions {string.Join(", ", versions)}."));
			}
		}

		return conflicts;
	}
}
