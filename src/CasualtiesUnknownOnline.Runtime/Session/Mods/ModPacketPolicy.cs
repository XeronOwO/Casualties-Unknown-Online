using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The pure safety rails for one mod's declared-packet registry. A declaration
/// is mod-authored, so without caps a broken or hostile mod could grow the
/// framework's per-frame work without bound or poison the registry with an id
/// the router cannot address. Ids are bounded by grammar, chains by length,
/// declarations by count, and the value a packet carries by the framework's own
/// encoder (<see cref="ModValueCodec"/>, the one validator) — every refusal is a
/// log line naming the reason, never a silent truncation.
/// </summary>
internal static class ModPacketPolicy
{
	/// <summary>The handler cap of one packet's chain — the per-frame work a single frame can buy.</summary>
	public const int MaxHandlersPerPacket = 16;

	/// <summary>The declared-packet cap of one mod.</summary>
	public const int MaxPacketsPerMod = 64;

	/// <summary>
	/// Packet ids use the canonical content-id path grammar (lower-case ASCII
	/// <c>[a-z0-9][a-z0-9_.-]{0,94}</c>). The id rides the wire and is matched
	/// ordinally on both ends, so a second grammar would only add a way to
	/// spell an id the other side cannot address.
	/// </summary>
	public static bool IsValidId(string? id) => ContentId.IsValidPath(id);

	/// <summary>
	/// A declaration must carry a runnable chain: at least one handler, at most
	/// <see cref="MaxHandlersPerPacket"/>, none of them null. A declaration
	/// nothing can run would make every frame for it an invisible drop.
	/// </summary>
	public static bool IsValidChain(ModPacket? packet)
	{
		if (packet?.Handlers is null || packet.Handlers.Count is 0 or > MaxHandlersPerPacket)
		{
			return false;
		}

		foreach (var handler in packet.Handlers)
		{
			if (handler is null)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>Declaring a brand-new packet must not exceed the per-mod count cap.</summary>
	public static bool CanAdd(int currentCount) => currentCount < MaxPacketsPerMod;
}
