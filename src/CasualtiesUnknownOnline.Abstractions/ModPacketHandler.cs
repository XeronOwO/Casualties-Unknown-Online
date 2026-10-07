using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One step of a declared packet's chain: a stage plus the delegate that runs
/// in it. Handlers are invoked on the frame's own thread of control — the
/// receive path or the mod's own send call — and every one of them is
/// exception-isolated: a throw is logged with the mod id, the packet id and
/// the stage, and the chain continues, so a broken handler can never wedge the
/// router or swallow the other members' delivery.
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public sealed class ModPacketHandler(ModPacketStage stage, Action<IModPacketContext> handler)
{
	/// <summary>The phase this handler runs in.</summary>
	public ModPacketStage Stage { get; } = stage;

	/// <summary>The handler itself.</summary>
	public Action<IModPacketContext> Handler { get; } = handler ?? throw new ArgumentNullException(nameof(handler));
}
