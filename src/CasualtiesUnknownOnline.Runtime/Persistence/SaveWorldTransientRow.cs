using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One row of <c>world-transients.json</c> (§3.4): the world facts that are
/// neither a block nor a kernel-domain entity and that no regeneration would
/// reproduce. Keypad codes and geyser liquid types are <b>decided</b> values —
/// they must be carried, never re-rolled (S3's approved decisions) — and the
/// radiation line's active flag and descent are host world state a late joiner
/// is already aligned to over the wire. The payloads are the wire DTOs the live
/// snapshot path sends, so a restore hands the existing appliers their own
/// shape; the entity's world position is the identity (both sides regenerate the
/// same object at the same place).
/// </summary>
public sealed class SaveWorldTransientRow
{
	/// <summary>The row kind: <c>keypad</c>, <c>geyser</c> or <c>radiation-line</c>.</summary>
	public string Kind { get; init; } = string.Empty;

	public KeypadEntryMsg? Keypad { get; init; }

	public GeyserStateEntryMsg? Geyser { get; init; }

	public RadiationLineStateMsg? RadiationLine { get; init; }

	public static SaveWorldTransientRow OfKeypad(KeypadEntryMsg code) =>
		new() { Kind = KeypadKind, Keypad = code };

	public static SaveWorldTransientRow OfGeyser(GeyserStateEntryMsg geyser) =>
		new() { Kind = GeyserKind, Geyser = geyser };

	public static SaveWorldTransientRow OfRadiationLine(RadiationLineStateMsg state) =>
		new() { Kind = RadiationLineKind, RadiationLine = state };

	/// <summary>The row's identity for the damage report: the entity's world position, or its kind when it has none.</summary>
	public string Describe() => Kind switch
	{
		KeypadKind => $"keypad at {Position(Keypad?.Position)}",
		GeyserKind => $"geyser at {Position(Geyser?.Position)}",
		RadiationLineKind => $"radiation-line active={RadiationLine?.Active.ToString() ?? "?"}",
		_ => $"<{Kind}>",
	};

	private static string Position(NetVector2Msg? position) =>
		position is null ? "<no position>" : $"({position.X},{position.Y})";

	public const string KeypadKind = "keypad";
	public const string GeyserKind = "geyser";
	public const string RadiationLineKind = "radiation-line";
}
