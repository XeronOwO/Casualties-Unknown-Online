using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// The mod-content half of the resource vocabulary: every registration whose
/// owning mod declared a content namespace becomes <c>namespace:id</c>, with the
/// typed DTO's display name when the kind has one. Legacy registrations without
/// a namespace have no canonical id and are deliberately skipped — they are not
/// addressable by the namespaced vocabulary.
///
/// A bare content id that several mods registered for the same kind is skipped
/// as well: the bare id is the game-table key that content providers
/// materialise, so only one of those registrations can exist in the game. The
/// catalog reports that as a conflict; the completion vocabulary must not offer
/// an address the game cannot honour.
/// </summary>
public sealed class ModContentResourceLocationSource(
	IModContentControl content,
	ILogger<ModContentResourceLocationSource> log) : IResourceLocationSource
{
	public IReadOnlyList<ResourceLocationEntry> Entries
	{
		get
		{
			var registrations = content.Entries;
			var ambiguousBareIds = new HashSet<string>(
				registrations
					.GroupBy(entry => (entry.Definition.Kind, entry.Definition.Id))
					.Where(group => group.Count() > 1)
					.Select(group => BareKey(group.Key.Kind, group.Key.Id)),
				StringComparer.Ordinal);

			var entries = new List<ResourceLocationEntry>();
			foreach (var registration in registrations)
			{
				if (!registration.TryGetCanonicalId(out var id))
				{
					continue;
				}

				if (ambiguousBareIds.Contains(BareKey(registration.Definition.Kind, registration.Definition.Id)))
				{
					log.LogDebug(
						"[ContentId] {Id} is not advertised: its bare id '{BareId}' is registered by several mods, so the game table cannot hold them all.",
						id, registration.Definition.Id);
					continue;
				}

				entries.Add(new ResourceLocationEntry(
					id,
					registration.Definition.Kind,
					ModContentDisplayName.Resolve(registration.Definition)));
			}

			return entries;
		}
	}

	private static string BareKey(string kind, string id) => $"{kind}\u0000{id}";
}
