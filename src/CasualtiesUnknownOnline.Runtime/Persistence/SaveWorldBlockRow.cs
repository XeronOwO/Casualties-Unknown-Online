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
/// exactly these two tables (<c>WorldBlockState</c> / <c>BlockDamageSnapshot</c>),
/// so a restored host holds what a join would have handed it, and the restore
/// seam applies them through the same shapes the live path already validates.
/// The cell <c>(X,Y)</c> is the identity of both facts — no offset is stored.
/// </summary>
public sealed class SaveWorldBlockRow
{
	/// <summary>The row kind: <c>block-state</c> or <c>block-damage</c>.</summary>
	public string Kind { get; init; } = string.Empty;

	public BlockStateEntryMsg? BlockState { get; init; }

	public BlockDamageEntryMsg? BlockDamage { get; init; }

	public static SaveWorldBlockRow OfBlockState(int x, int y, ushort block) =>
		new() { Kind = BlockStateKind, BlockState = new BlockStateEntryMsg { X = x, Y = y, Block = block } };

	public static SaveWorldBlockRow OfBlockDamage(int x, int y, float damage) =>
		new() { Kind = BlockDamageKind, BlockDamage = new BlockDamageEntryMsg { X = x, Y = y, Damage = damage } };

	/// <summary>The row's identity for the damage report: its kind and block cell.</summary>
	public string Describe() => Kind switch
	{
		BlockStateKind => $"block-state at {Cell(BlockState?.X, BlockState?.Y)}",
		BlockDamageKind => $"block-damage at {Cell(BlockDamage?.X, BlockDamage?.Y)}",
		_ => $"<{Kind}>",
	};

	private static string Cell(int? x, int? y) =>
		x is null || y is null ? "<no cell>" : $"({x},{y})";

	public const string BlockStateKind = "block-state";
	public const string BlockDamageKind = "block-damage";
}
