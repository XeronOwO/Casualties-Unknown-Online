using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The default read-only content catalog backed by <see cref="IModContentControl"/>.
/// It deliberately performs no interpretation and makes no ownership choice:
/// it enumerates the framework-wide view, supports kind/id lookup, and returns
/// conflict diagnostics for future binders. The underlying registry already
/// enforces per-mod id uniqueness; this catalog adds the cross-mod view.
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

		var matches = Entries
			.Where(e => string.Equals(e.Definition.Kind, kind, StringComparison.Ordinal)
				&& string.Equals(e.Definition.Id, id, StringComparison.Ordinal))
			.ToList();

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
				group.Key.Id,
				ModContentConflictKind.DuplicateId,
				ownerModIds,
				$"{ownerModIds.Count} mods registered the same {group.Key.Kind} content id '{group.Key.Id}'."));

			var versions = groupEntries
				.Select(e => e.Definition.SchemaVersion)
				.Distinct()
				.ToList();
			if (versions.Count > 1)
			{
				conflicts.Add(new ModContentConflict(
					group.Key.Kind,
					group.Key.Id,
					ModContentConflictKind.VersionMismatch,
					ownerModIds,
					$"The same {group.Key.Kind} content id '{group.Key.Id}' uses incompatible schema versions {string.Join(", ", versions)}."));
			}
		}

		return conflicts;
	}
}
