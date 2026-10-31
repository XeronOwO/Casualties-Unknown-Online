using System;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The ONE place a typed item id becomes the definition id the game's own tables take. A player types either form — the bare id the game itself names
/// (<c>bandage</c>) or the canonical <c>namespace:path</c> every other surface shows (<c>cu:bandage</c>, a mod's <c>mymod:dressing</c>) — and both must
/// reach the same object, because a list that teaches the canonical form while the verb refuses it is the defect this resolves.
/// </summary>
/// <remarks>
/// The built-in namespace (<see cref="ContentId.BuiltInNamespace"/>) names the GAME's own content, whose tables are keyed by the bare id: <c>cu:waterbottle</c>
/// resolves to <c>waterbottle</c>. Any other namespace is a mod's content, registered under its canonical id, so it travels unchanged.
/// </remarks>
internal static class ItemDefinitionId
{
	/// <summary>
	/// Resolves <paramref name="token"/> to the definition id to look up, or reports why it is not one. A bare token is returned as typed (the game's table
	/// decides whether it exists, and its refusal names the id the player typed).
	/// </summary>
	internal static bool TryResolve(string token, out string definitionId, out string? error)
	{
		definitionId = token;
		error = null;
		if (string.IsNullOrWhiteSpace(token))
		{
			error = "No item id was given.";
			return false;
		}

		if (token.IndexOf(ContentId.Separator) < 0)
		{
			return true;
		}

		if (!ContentId.TryParse(token, out var id))
		{
			error = $"'{token}' is not a namespaced id — the form is namespace:path, e.g. cu:bandage.";
			return false;
		}

		definitionId = string.Equals(id.Namespace, ContentId.BuiltInNamespace, StringComparison.OrdinalIgnoreCase)
			? id.Path
			: id.ToString();
		return true;
	}
}
