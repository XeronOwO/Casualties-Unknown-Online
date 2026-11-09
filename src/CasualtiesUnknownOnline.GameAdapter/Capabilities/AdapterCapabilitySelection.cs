using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.GameAdapter.Capabilities;

/// <summary>
/// The stage-3 install-unit selection: which capabilities a configuration asks
/// to leave out at install, and which requests are refused because they name an
/// unknown id or a Required capability.
///
/// A required system cannot be turned off — the vanilla game owns it, and a
/// session without it is not the same game — so such a request is refused loudly
/// and the capability installs. An optional capability's request is honoured as a
/// whole unit: every patch container it owns is unpatch after the assembly-wide
/// install, so no half-installed unit runs.
/// </summary>
internal static class AdapterCapabilitySelection
{
	/// <summary>The disabled ids and the refused requests, both in the request's order.</summary>
	internal static (IReadOnlyList<string> Disabled, IReadOnlyList<string> Refused) Resolve(
		IReadOnlyCollection<string>? requested)
	{
		if (requested is null || requested.Count == 0)
		{
			return ([], []);
		}

		var optional = new HashSet<string>(AdapterCapabilityCatalog.OptionalCapabilityIds, StringComparer.Ordinal);
		var known = new HashSet<string>(
			AdapterCapabilityCatalog.All.Select(definition => definition.Id),
			StringComparer.Ordinal);

		var disabled = new List<string>();
		var refused = new List<string>();
		foreach (var id in requested)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				continue;
			}

			if (!known.Contains(id) || !optional.Contains(id))
			{
				refused.Add(id);
				continue;
			}

			disabled.Add(id);
		}

		return (disabled, refused);
	}
}
