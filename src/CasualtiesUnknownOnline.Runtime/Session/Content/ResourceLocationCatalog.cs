using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// The default resource-location catalog: merges every registered
/// <see cref="IResourceLocationSource"/> (built-in CUO ids, mod content, and
/// the Game Adapter's vanilla-content source) into one canonical-id-keyed view
/// and answers completion queries.
///
/// The merge is intentionally computed per call: sources are cheap
/// enumerations of already-loaded tables, the console only asks while a
/// resource argument is being typed, and a cached snapshot would need
/// invalidation for both late mod registration and the game's own late item
/// table. Duplicate canonical ids keep the first source's entry and are logged
/// at debug level (a real ownership conflict, not a crash).
/// </summary>
public sealed class ResourceLocationCatalog(
	IEnumerable<IResourceLocationSource> sources,
	ILogger<ResourceLocationCatalog> log) : IResourceLocationCatalog
{
	/// <summary>Maximum number of completion candidates returned for one prefix.</summary>
	public const int MaxSuggestions = 20;

	private readonly IReadOnlyList<IResourceLocationSource> _sources = [.. sources];
	private readonly ILogger<ResourceLocationCatalog> _log = log;

	public IReadOnlyList<ResourceLocationEntry> Entries
	{
		get
		{
			var merged = new Dictionary<ContentId, ResourceLocationEntry>();
			foreach (var source in _sources)
			{
				foreach (var entry in source.Entries)
				{
					if (entry.Id == default)
					{
						// A source must never contribute an uninitialised id; it
						// is not addressable and would break matching.
						_log.LogWarning(
							"[ContentId] {Source} contributed an entry with an uninitialised id — skipped.",
							source.GetType().Name);
						continue;
					}

					if (merged.ContainsKey(entry.Id))
					{
						_log.LogDebug(
							"[ContentId] duplicate resource entry {Id} from {Source} — the first source wins.",
							entry.Id, source.GetType().Name);
						continue;
					}

					merged.Add(entry.Id, entry);
				}
			}

			return [.. merged.Values.OrderBy(entry => entry.Id)];
		}
	}

	public IReadOnlyList<ResourceLocationEntry> Suggest(string prefix)
	{
		var entries = Entries;
		var normalized = (prefix ?? string.Empty).Trim().ToLowerInvariant();
		if (normalized.Length == 0)
		{
			return [.. entries.Take(MaxSuggestions)];
		}

		var ranked = new List<(int Rank, ResourceLocationEntry Entry)>();
		foreach (var entry in entries)
		{
			var rank = MatchRank(entry, normalized);
			if (rank >= 0)
			{
				ranked.Add((rank, entry));
			}
		}

		return
		[
			.. ranked
				.OrderBy(candidate => candidate.Rank)
				.ThenBy(candidate => candidate.Entry.Id)
				.Take(MaxSuggestions)
				.Select(candidate => candidate.Entry)
		];
	}

	/// <summary>
	/// 0 = exact canonical id, 1 = canonical id prefix, 2 = bare path prefix,
	/// 3 = display-name prefix, -1 = no match. The ordering is the console's
	/// ranking contract: an exact id always wins over a name that happens to
	/// start with the same text.
	/// </summary>
	private static int MatchRank(ResourceLocationEntry entry, string normalizedPrefix)
	{
		var canonical = entry.Id.ToString();
		if (string.Equals(canonical, normalizedPrefix, StringComparison.Ordinal))
		{
			return 0;
		}

		if (canonical.StartsWith(normalizedPrefix, StringComparison.Ordinal))
		{
			return 1;
		}

		if (normalizedPrefix.IndexOf(ContentId.Separator) < 0
			&& entry.Id.Path.StartsWith(normalizedPrefix, StringComparison.Ordinal))
		{
			return 2;
		}

		if ((entry.DisplayName ?? string.Empty).ToLowerInvariant().StartsWith(normalizedPrefix, StringComparison.Ordinal))
		{
			return 3;
		}

		return -1;
	}
}
