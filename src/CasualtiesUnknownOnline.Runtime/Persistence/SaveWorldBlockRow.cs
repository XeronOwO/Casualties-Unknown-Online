using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One row of <c>world-blocks.json</c> (§3.4): the host's in-layer block diff.
/// The world-block domain holds three shapes of fact — a block state that
/// differs from the generated baseline (mined, destroyed, built, reverted), the
/// partial damage accumulated on a block that has NOT broken yet in CUO's own
/// table, and the same partial damage in the GAME's own
/// <c>WorldGeneration.world.blockDamages</c> list — so the file is a typed row
/// list and §6's salvage skips a bad row by itself.
///
/// The payloads are the WIRE DTOs on purpose: a late-joining guest is given
/// exactly the CUO tables (<c>WorldBlockState</c> / <c>BlockDamageSnapshot</c>),
/// so a restored host holds what a join would have handed it, and the restore
/// seam applies them through the same shapes the live path already validates.
/// The cell <c>(X,Y)</c> is the identity of every fact — no offset is stored.
///
/// The two damage rows carry the same <c>{x, y, damage}</c> shape on purpose:
/// both are "damage accumulated on a surviving block at this cell". They are
/// kept apart by their KIND, never merged, because the two tables are bounded
/// differently and owned differently — CUO's registry is the host's wire table
/// (cap 256), the game's list is the live gameplay table (cap 128) that also
/// carries damage the CUO report hooks never saw. Routing a row back into the
/// wrong table would both overflow the wrong cap and lose the fact.
/// </summary>
public sealed class SaveWorldBlockRow
{
	/// <summary>The row kind: <c>block-state</c>, <c>block-damage</c> or <c>native-block-damage</c>.</summary>
	public string Kind { get; init; } = string.Empty;

	public BlockStateEntryMsg? BlockState { get; init; }

	public BlockDamageEntryMsg? BlockDamage { get; init; }

	/// <summary>The game's own partial-damage entry — the same shape as <see cref="BlockDamage"/>, a different table (see the type summary).</summary>
	public BlockDamageEntryMsg? NativeBlockDamage { get; init; }

	public static SaveWorldBlockRow OfBlockState(int x, int y, ushort block) =>
		new() { Kind = BlockStateKind, BlockState = new BlockStateEntryMsg { X = x, Y = y, Block = block } };

	public static SaveWorldBlockRow OfBlockDamage(int x, int y, float damage) =>
		new() { Kind = BlockDamageKind, BlockDamage = new BlockDamageEntryMsg { X = x, Y = y, Damage = damage } };

	/// <summary>One entry of the game's own <c>blockDamages</c> list — written back by the adapter, never by CUO's registry.</summary>
	public static SaveWorldBlockRow OfNativeBlockDamage(int x, int y, float damage) =>
		new() { Kind = NativeBlockDamageKind, NativeBlockDamage = new BlockDamageEntryMsg { X = x, Y = y, Damage = damage } };

	/// <summary>The row's identity for the damage report: its kind and block cell.</summary>
	public string Describe() => Kind switch
	{
		BlockStateKind => $"block-state at {Cell(BlockState?.X, BlockState?.Y)}",
		BlockDamageKind => $"block-damage at {Cell(BlockDamage?.X, BlockDamage?.Y)}",
		NativeBlockDamageKind => $"native-block-damage at {Cell(NativeBlockDamage?.X, NativeBlockDamage?.Y)}",
		_ => $"<{Kind}>",
	};

	private static string Cell(int? x, int? y) =>
		x is null || y is null ? "<no cell>" : $"({x},{y})";

	public const string BlockStateKind = "block-state";
	public const string BlockDamageKind = "block-damage";

	/// <summary>The game's own <c>WorldGeneration.world.blockDamages</c> table — a second, separately capped table of the same damage shape.</summary>
	public const string NativeBlockDamageKind = "native-block-damage";
}
