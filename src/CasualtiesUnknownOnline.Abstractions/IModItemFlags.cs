namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The optional 1.0 trade flags a definition may declare: <c>secret</c> hides an
/// item from the game's random-item rolls and <c>untradeable</c> keeps traders from
/// buying it. Both are read together by <c>Item.GlobalItems.PickRandom</c>'s
/// exclusion loop (<c>Item.cs:7932</c>), so an item that declares either must reach
/// the vanilla <c>ItemInfo</c> field.
/// </summary>
/// <remarks>
/// A separate interface rather than two more members on
/// <see cref="IModItemDefinition"/>: the flags are optional, and an implementation
/// that does not carry them (a test double, a module's own record) keeps compiling
/// and reads as the default (false).
/// </remarks>
public interface IModItemFlags
{
	/// <summary>Whether the item is hidden from the game's random-item rolls.</summary>
	bool Secret { get; }

	/// <summary>Whether traders refuse the item.</summary>
	bool Untradeable { get; }
}
