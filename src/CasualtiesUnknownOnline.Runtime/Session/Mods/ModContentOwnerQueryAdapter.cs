using System;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The <see cref="IModContentOwnerQuery"/> implementation. It reads the same
/// framework-wide <see cref="IModContentControl"/> view used by
/// <see cref="ModContentCatalog"/>, so ownership resolution follows the same
/// ordinal id/kind rules and ambiguity policy as the runtime catalog: a unique
/// kind + id resolves to exactly one owning mod, while an absent or duplicated
/// id returns false. The adapter is created per <see cref="ModContext"/> and
/// holds no state of its own.
/// </summary>
internal sealed class ModContentOwnerQueryAdapter(IModContentControl control) : IModContentOwnerQuery
{
	private readonly IModContentControl _control = control;

	public bool TryGetOwner(string kind, string id, out string modId)
	{
		if (kind is null)
		{
			throw new ArgumentNullException(nameof(kind));
		}

		if (id is null)
		{
			throw new ArgumentNullException(nameof(id));
		}

		var matches = _control.Entries
			.Where(e => string.Equals(e.Definition.Kind, kind, StringComparison.Ordinal)
				&& string.Equals(e.Definition.Id, id, StringComparison.Ordinal))
			.ToList();

		if (matches.Count == 1)
		{
			modId = matches[0].ModId;
			return true;
		}

		modId = string.Empty;
		return false;
	}
}
