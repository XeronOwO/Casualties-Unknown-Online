using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The conditions under which an item whose <see cref="IModItemDefinition.DecayMinutes"/>
/// is positive decays at all — the game's own condition flags, read by
/// <c>Item.HandleDecay</c> (<c>Item.cs:186-197</c>) out of the item's decay byte
/// and printed by the item tooltip (<c>PlayerCamera.cs:1147-1167</c>).
///
/// <para>
/// The member VALUES are the game's own <c>ItemInfo.DecayType</c> bits, because
/// that is the field they are written into; a mod reads this vocabulary, never the
/// number. The battery bit is deliberately absent: declaring
/// <see cref="IModItemDefinition.Battery"/> is what sets it, since the game's decay
/// tick drains the battery component behind it, and a flag without that component
/// is a null dereference rather than an effect. A value carrying a bit outside
/// this vocabulary is refused at registration.
/// </para>
/// </summary>
[Flags]
public enum ModItemDecayCondition
{
	/// <summary>No condition: the item decays wherever it is. The default — the game's own value for an item that declares none.</summary>
	None = 0,

	/// <summary>Decays only while it is a container that holds at least one item (<c>Item.cs:186-189</c>).</summary>
	OnlyWithContents = 1,

	/// <summary>Decays only while it is worn (<c>Item.cs:190-193</c>).</summary>
	OnlyWhenWorn = 2,

	/// <summary>Decays only while the player is moving (<c>Item.cs:194-197</c>).</summary>
	OnlyWhenMoving = 4,

	/// <summary>The defined bit mask — used to reject unknown and framework-owned bits.</summary>
	All = OnlyWithContents | OnlyWhenWorn | OnlyWhenMoving,
}
