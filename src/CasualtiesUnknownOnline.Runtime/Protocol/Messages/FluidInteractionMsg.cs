using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A player's fluid interaction — a consumed grid cell (drinking).
/// Bidirectional, BlockPlaced semantics: the drinking side applies the full
/// local effect (the game's DrinkLiquid: the body effects land immediately
/// and its grid cell clears — immediate feedback) and reports; the host
/// executes on its own grid (the cell non-empty → clear → relay; already
/// empty → ignore — a previous event already cleared it and every side is
/// consistent, no correction message is ever needed) and relays to the other
/// members (the source excluded, it already applied locally). The type is not
/// carried: it is read from the host's grid (the authority). The body effects
/// (thirst, sickness) happen on the drinking side and ride the CharacterData
/// report. The bath-soiled water (LiquidAffect's SetLiquid(5)) is NOT
/// reported: it is low-frequency/low-perception and heals via the 1 Hz
/// viewport snapshot.
/// </summary>
[ProtoContract]
public sealed class FluidInteractionMsg
{
	/// <summary>Drinking — the cell at <see cref="X"/>,<see cref="Y"/> is consumed.</summary>
	public const byte KindDrink = 1;

	[ProtoMember(1)]
	public byte Kind { get; set; }

	/// <summary>The consumed grid cell (block coordinates).</summary>
	[ProtoMember(2)]
	public int X { get; set; }

	[ProtoMember(3)]
	public int Y { get; set; }
}
