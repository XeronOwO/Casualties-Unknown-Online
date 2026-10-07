using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One declared packet: the message identity a mod owns, the policy that says
/// who may send it and which copies run it, and the chain that runs on each of
/// them. The declaration is one object so that registering it is atomic — a
/// packet never exists half-declared. The same declaration is registered on
/// every side that runs the mod (the framework routes by mod id and packet id,
/// and the side that receives a frame looks its own declaration up), so the
/// packet's shape is part of what the mod's version identifies.
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public sealed class ModPacket(
	string id,
	ModPacketSender sender,
	ModPacketDelivery delivery,
	params ModPacketHandler[] handlers)
{
	/// <summary>
	/// The mod-owned packet id. It travels on the wire beside the mod id and
	/// is what the receiving side routes by, so it must be stable across the
	/// versions of this mod that share a session. The framework bounds it by
	/// the canonical id grammar (lower-case ASCII segments, the same grammar
	/// content ids use) and refuses anything else at registration.
	/// </summary>
	public string Id { get; } = id;

	/// <summary>Who may send this packet — see <see cref="ModPacketSender"/>.</summary>
	public ModPacketSender Sender { get; } = sender;

	/// <summary>Which copies run this packet's chain — see <see cref="ModPacketDelivery"/>.</summary>
	public ModPacketDelivery Delivery { get; } = delivery;

	/// <summary>
	/// The declared chain, in declaration order. The framework runs the stages
	/// in their fixed order (<see cref="ModPacketStage.Validate"/> then
	/// <see cref="ModPacketStage.Apply"/> then <see cref="ModPacketStage.Observe"/>)
	/// and keeps the declared order inside each stage. A packet needs at least
	/// one handler — a declaration nothing can run is refused at registration.
	/// </summary>
	public IReadOnlyList<ModPacketHandler> Handlers { get; } = handlers is null ? [] : [.. handlers];
}
