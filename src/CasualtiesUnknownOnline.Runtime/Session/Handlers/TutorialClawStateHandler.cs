using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: the authoritative tutorial-claw presentation snapshot
/// (unreliable, seq-gated). Guests that are not running their own tutorial
/// course apply it to the local render claw.</summary>
[PacketHandler(NetMsg.TutorialClawState, NetMessageDirection.HostToGuest)]
public sealed class TutorialClawStateHandler : PacketHandlerBase<TutorialClawStateMsg, ITutorialSessionHandlerContext>
{
	protected override void Handle(ulong sender, TutorialClawStateMsg msg, ITutorialSessionHandlerContext ctx)
	{
		if (ctx.Session.Role != SessionRole.Guest)
		{
			return;
		}

		ctx.TutorialClaw.ApplyTutorialClawState(msg);
	}
}
