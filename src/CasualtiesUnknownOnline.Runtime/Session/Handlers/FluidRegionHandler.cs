using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's fluid-grid region arrived (unreliable state stream): apply it
/// onto the local grid (the game's own renderer then draws it). The host is
/// the fluid authority — the guest never simulates, it renders the streamed
/// regions; every region is an absolute RLE snapshot of its rectangle, so an
/// apply is idempotent and a lost message is healed by the next one.
///
/// <para>
/// This stream runs at 10 Hz (<c>AdaptiveStreamId.FluidRegionDiffStream</c>), so its
/// receive log is bounded the same way the sender's is: the FIRST regions of a
/// rectangle report at Information (the stream is demonstrably alive), a repeat of
/// the same rectangle inside the window drops to Debug — one line per arriving
/// region at Information is a per-tick log, which the level policy puts at
/// Verbose/Debug — and a rectangle whose BOUNDS move reports again. Two properties of
/// this window are deliberate and named rather than implied: its subject is the
/// rectangle, so fluid that changes INSIDE unchanged bounds does not report at
/// Information again, and the window belongs to the handler, a process-lifetime
/// singleton — after a session boundary that rectangle is Debug-only and the
/// held-back count keeps accumulating (bounded, and Debug is the level the policy asks
/// for a per-tick stream). The sender logs at Debug for the same reason
/// (<c>FluidSimulationAuthority</c>).
/// </para>
/// </summary>
[PacketHandler(NetMsg.FluidRegion, NetMessageDirection.HostToGuest)]
public sealed class FluidRegionHandler(ILogger<FluidRegionHandler> log) : PacketHandlerBase<FluidRegionMsg, IWorldHandlerContext>
{
	private readonly ILogger<FluidRegionHandler> _log = log;

	/// <summary>Per-rectangle window for the receive log (see the class note).</summary>
	private readonly LogRepetitionGuard _regions = new(suppressAfter: 2);

	protected override void Handle(ulong sender, FluidRegionMsg msg, IWorldHandlerContext ctx)
	{
		ctx.World.FireFluidRegionReceived(msg);

		// The rectangle IS the stream's subject: while it stands still every region is
		// the same message, and only a rectangle whose BOUNDS change is a new subject.
		var key = $"{msg.OriginX},{msg.OriginY},{msg.Width},{msg.Height}";
		if (_regions.TryLog(key, key, out var repeat))
		{
			_log.LogInformation(
				"[Fluid] region=(x={X},y={Y},w={W},h={H}) cells={Cells} seq={Seq} from {Sender} (repeat {Repeat}).",
				msg.OriginX, msg.OriginY, msg.Width, msg.Height, msg.Cells.Length, msg.Seq, sender, repeat);
			return;
		}

		_log.LogDebug(
			"[Fluid] region=(x={X},y={Y},w={W},h={H}) cells={Cells} seq={Seq} from {Sender} — the same rectangle is standing (10 Hz stream, {Suppressed} line(s) held back).",
			msg.OriginX, msg.OriginY, msg.Width, msg.Height, msg.Cells.Length, msg.Seq, sender, _regions.Suppressed(key));
	}
}
