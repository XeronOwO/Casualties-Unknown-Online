using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One row of <c>world-blocks.json</c> (§3.4): the host's in-layer block diff.
/// The world-block domain holds two shapes of fact — a block state that differs
/// from the generated baseline (mined, destroyed, built, reverted), and the
/// partial damage accumulated on a block that has NOT broken yet — so the file
/// is a typed row list and §6's salvage skips a bad row by itself.
///
/// The payloads are the WIRE DTOs on purpose: a late-joining guest is given
/// exactly those shapes (<c>WorldBlockState</c> / <c>BlockDamageSnapshot</c>), so
/// a restored host holds what a join would have handed it, and the restore seam
/// applies them through the same shapes the live path already validates. The
/// cell <c>(X,Y)</c> is the identity of every fact — no offset is stored.
///
/// The damage row carries its own kind because it is NOT a Runtime-owned table:
/// CUO's partial block damage has no table of its own. It lives only in the
/// GAME's <c>WorldGeneration.world.blockDamages</c> list, which the adapter reads
/// and writes through the native world-fact port, so a restore routes the row
/// there and never into a CUO table. Keeping a second bounded registry around
/// was exactly what let the two sets drift apart — same damage shape, different
/// cap, different eviction.
/// </summary>
public sealed class SaveWorldBlockRow
{
	/// <summary>The row kind: <c>block-state</c> or <c>native-block-damage</c>.</summary>
	public string Kind { get; init; } = string.Empty;

	public BlockStateEntryMsg? BlockState { get; init; }

	/// <summary>One entry of the game's own <c>blockDamages</c> list — read and written by the adapter, never by a CUO table.</summary>
	public BlockDamageEntryMsg? NativeBlockDamage { get; init; }

	public static SaveWorldBlockRow OfBlockState(int x, int y, ushort block) =>
		new() { Kind = BlockStateKind, BlockState = new BlockStateEntryMsg { X = x, Y = y, Block = block } };

	/// <summary>One entry of the game's own <c>blockDamages</c> list (see <see cref="NativeBlockDamage"/>).</summary>
	public static SaveWorldBlockRow OfNativeBlockDamage(int x, int y, float damage) =>
		new() { Kind = NativeBlockDamageKind, NativeBlockDamage = new BlockDamageEntryMsg { X = x, Y = y, Damage = damage } };

	/// <summary>The row's identity for the damage report: its kind and block cell.</summary>
	public string Describe() => Kind switch
	{
		BlockStateKind => $"block-state at {Cell(BlockState?.X, BlockState?.Y)}",
		NativeBlockDamageKind => $"native-block-damage at {Cell(NativeBlockDamage?.X, NativeBlockDamage?.Y)}",
		_ => $"<{Kind}>",
	};

	private static string Cell(int? x, int? y) =>
		x is null || y is null ? "<no cell>" : $"({x},{y})";

	public const string BlockStateKind = "block-state";

	/// <summary>The game's own <c>WorldGeneration.world.blockDamages</c> table — the only partial-damage table there is.</summary>
	public const string NativeBlockDamageKind = "native-block-damage";
}
