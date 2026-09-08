using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The <see cref="IModContentOwnerQuery"/> implementation. It reads the same
/// framework-wide <see cref="IModContentControl"/> view used by
/// <see cref="ModContentCatalog"/>, so ownership resolution follows the same
/// rules: a canonical <c>namespace:path</c> id resolves by canonical identity,
/// a legacy bare id resolves only while it is unique across owners, and an
/// absent or ambiguous id returns false. The adapter is created per
/// <see cref="ModContext"/> and holds no state of its own.
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

		List<ModContentRegistration> matches;
		if (ContentId.TryParse(id, out var canonical))
		{
			matches =
			[
				.. _control.Entries.Where(e => string.Equals(e.Definition.Kind, kind, StringComparison.Ordinal)
					&& e.TryGetCanonicalId(out var candidate)
					&& candidate == canonical)
			];
		}
		else
		{
			matches =
			[
				.. _control.Entries.Where(e => string.Equals(e.Definition.Kind, kind, StringComparison.Ordinal)
					&& string.Equals(e.Definition.Id, id, StringComparison.Ordinal))
			];
		}

		if (matches.Count == 1)
		{
			modId = matches[0].ModId;
			return true;
		}

		modId = string.Empty;
		return false;
	}
}
