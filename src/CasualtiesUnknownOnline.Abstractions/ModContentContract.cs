using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The nine KIND CONTRACTS of the content API and the kind each one fixes — the
/// one home of the fact that <see cref="IModItemDefinition"/> means
/// <see cref="ModContentKind.Item"/>.
///
/// A declaration's kind comes from the contract it implements
/// (<see cref="ModContentAttribute"/>), so a consumer that holds only the
/// contract type asks here rather than restating the mapping. The framework's
/// ready-made data classes and the optional per-kind base classes answer the
/// same question through their own <c>Kind</c> member, and a family test pins
/// the three answers together.
/// </summary>
public static class ModContentContract
{
	/// <summary>
	/// One row per constant of <see cref="ModContentKind"/>, in the same order.
	/// The table is the source of truth for both halves of the API: the scan
	/// reads it to know which contracts exist and which kind each fixes.
	/// </summary>
	private static readonly (Type Contract, string Kind)[] Kinds =
	[
		(typeof(IModItemDefinition), ModContentKind.Item),
		(typeof(IModRecipeDefinition), ModContentKind.Recipe),
		(typeof(IModLiquidDefinition), ModContentKind.Liquid),
		(typeof(IModLiquidTileDefinition), ModContentKind.LiquidTile),
		(typeof(IModTileDefinition), ModContentKind.Tile),
		(typeof(IModBuildingDefinition), ModContentKind.Building),
		(typeof(IModStructureDefinition), ModContentKind.Structure),
		(typeof(IModStatusDefinition), ModContentKind.Status),
		(typeof(IModMoodleDefinition), ModContentKind.Moodle),
	];

	/// <summary>
	/// The kind a contract fixes, or false when the type is not one of the nine
	/// kind contracts — which is how a declaration that implements none of them
	/// is answered rather than guessed at.
	/// </summary>
	public static bool TryGetKind(Type contract, out string kind)
	{
		foreach (var entry in Kinds)
		{
			if (entry.Contract == contract)
			{
				kind = entry.Kind;
				return true;
			}
		}

		kind = "";
		return false;
	}
}
