using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A fluid-presentation event (water push / waterflow sound) arrived from the
/// host — surface it to the adapter, which replays the transient effect on the
/// guest's local world. Host→guest one-way; the host never receives this.
/// </summary>
[PacketHandler(NetMsg.FluidPresentation, NetMessageDirection.HostToGuest)]
public sealed class FluidPresentationHandler(ILogger<FluidPresentationHandler> log) : PacketHandlerBase<FluidPresentationMsg, IWorldHandlerContext>
{
	private readonly ILogger<FluidPresentationHandler> _log = log;

	protected override void Handle(ulong sender, FluidPresentationMsg msg, IWorldHandlerContext ctx)
	{
		ctx.World.FireFluidPresentationReceived(msg);
		_log.LogInformation("[Fluid] presentation kind={Kind} at=({X},{Y}) from {Sender}.", msg.Kind, msg.X, msg.Y, sender);
	}
}
