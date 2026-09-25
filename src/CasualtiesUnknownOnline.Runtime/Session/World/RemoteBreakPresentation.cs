namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// What a RECEIVED block write does to this side's world, and whether it owes
/// this player the break presentation.
///
/// A player's break reaches the other sides as two facts of ONE break: the air
/// write that makes the cell air (sent the instant the block is gone) and the
/// break report that carries the drops one frame later (the drops'
/// <c>Item.Start</c> has to fold in first). The air write therefore always
/// lands first, the receiving side's cell is already air when the break report
/// arrives, and the report's native <c>DamageBlock</c> roll — the roll that
/// plays the block's own hit/step sounds and spawns its break particles — never
/// runs there. The side that computed the break heard it; every other side saw
/// the block vanish in silence (user report 2026-09-21).
///
/// The ordering is STRUCTURAL, not incidental: the break's <c>SetBlock(0)</c> runs
/// inside the roll itself and its postfix broadcasts the air write synchronously
/// there, while the break report is written by the OUTER overload's postfix and
/// then held one frame for the drops — and one sender's reports share one ordered
/// reliable path, so the air write cannot arrive second. A change that split the
/// two facts onto different paths would have to re-establish that order for this
/// rule to keep holding.
///
/// So the write itself is applied through the game's OWN damage roll whenever
/// it is the block-removal half of a break somebody else computed
/// (<see cref="Route"/>), with the locally derived lethal remainder as that
/// roll's damage (<see cref="LethalDamage"/>). The presentation is never
/// rebuilt from clip names here: it is the same native path the source side
/// ran, so the sounds, the pitch randomization, the mixer group and the break
/// particles stay the game's. Every other write — a placement, an
/// earthquake/environment break (<c>SetBlock</c> inside
/// <c>WorldGeneration.Update</c>, silent on the side that ran it), a state
/// snapshot, a correction, the echo of this side's own write — stays a raw
/// write: its source played no break presentation, and inventing one here would
/// put a sound where the source had none.
///
/// Pure: no Unity, no world, no clock. The Game Adapter reads the cell's state
/// and executes the route verbatim.
/// </summary>
public static class RemoteBreakPresentation
{
	/// <summary>How a received block write is applied to this side's world.</summary>
	public enum Action
	{
		/// <summary>Write the cell as received — a placement, an environment break, a snapshot row, a correction, or a break this side already presented.</summary>
		WriteOnly,

		/// <summary>Break the standing block through the game's own damage roll: its hit/step sounds, its break particles, and the air write the roll itself performs.</summary>
		NativeBreak,
	}

	/// <summary>
	/// The decision the Game Adapter executes. <paramref name="playerBreak"/> is
	/// the write's own claim, carried on the wire: the source computed this air
	/// write inside a damage roll. <paramref name="writtenBlock"/> is the value
	/// the write carries (0 = an air write; any block value is a placement).
	/// <paramref name="cellHeldBlock"/> is whether THIS side's cell still holds a
	/// block, and it is the once-only guard: the echo of this side's own break,
	/// the relay of a break this side computed, a repeated report and a duplicate
	/// air write all find the cell already air and write only.
	/// </summary>
	public static Action Route(bool playerBreak, ushort writtenBlock, bool cellHeldBlock) =>
		playerBreak && writtenBlock == 0 && cellHeldBlock ? Action.NativeBreak : Action.WriteOnly;

	/// <summary>
	/// The damage that breaks THIS side's block through the game's own roll: the
	/// remainder of the cell's health in the game's accumulated units, because the
	/// roll adds to the damage row this side already holds. The received report's
	/// own damage is deliberately not the input — this side's row can be behind
	/// the source's by a lost hit report, while the fact being applied is "this
	/// block is gone", not the arithmetic that got there. A row that already
	/// holds more than the block's health (a snapshot row CUO wrote without
	/// breaking the block) makes any positive damage lethal, and the block's own
	/// health is returned for it; a cell with no row breaks on its own health.
	/// </summary>
	public static float LethalDamage(float health, float currentDamage)
	{
		var remaining = health - currentDamage;
		return remaining > 0f ? remaining : health;
	}
}
