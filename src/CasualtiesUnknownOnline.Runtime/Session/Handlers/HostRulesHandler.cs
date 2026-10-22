using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → member: the host's host-rule values, absolute. The receiver stores the
/// whole set, so its screen, its console and its own local gestures read what the
/// host is actually enforcing instead of this process's own config entries under the
/// same names.
///
/// <para>
/// The announcement is host → member only, and the receiver says so: a frame that
/// arrives while this process is the host is dropped and named. The wire direction is
/// already enforced at the receiver (the message registry's declared direction), so
/// this branch is the invariant made observable rather than the boundary itself.
/// </para>
/// </summary>
[PacketHandler(NetMsg.HostRules, NetMessageDirection.HostToGuest)]
public sealed class HostRulesHandler(ILogger<HostRulesHandler> log) : PacketHandlerBase<HostRulesMsg, IHostRulesHandlerContext>
{
	protected override void Handle(ulong sender, HostRulesMsg msg, IHostRulesHandlerContext ctx)
	{
		if (ctx.Session.Role != SessionRole.Guest)
		{
			log.LogWarning("[Rules] a host-rule announcement arrived from {Sender} while this process is {Role} — dropped (the announcement is host → member only).",
				sender, ctx.Session.Role);
			return;
		}

		if (sender != ctx.Session.HostSteamId)
		{
			// The same sender-anchored rule the cheat roster gained in the same change: the
			// declared direction proves the local ROLE, never that the frame came from this
			// session's host, and a rules set is read by the member's local checks.
			log.LogWarning("[Rules] a host-rule announcement from {Sender} was dropped: it is not this session's host ({Host}).",
				sender, ctx.Session.HostSteamId);
			return;
		}

		if (msg.Malformed() is { } malformed)
		{
			// A value outside a closed set is refused WHOLE: the sender is calling itself a
			// host, so what it declares about the rest of the set is not worth guessing at,
			// and this client keeps the set it holds (or none) rather than half of a bad one.
			log.LogWarning("[Rules] the announcement from {Sender} was refused: {Reason} — this client keeps the set it holds.", sender, malformed);
			return;
		}

		var set = msg.ToSet();
		if (!ctx.HostRules.Apply(set))
		{
			// The world-entry group and the 60 s repair group re-send the same set on
			// purpose, so a repeat is the ordinary case, not an anomaly.
			log.LogDebug("[Rules] the announcement from {Sender} repeats the set this client already holds.", sender);
			return;
		}

		log.LogInformation("[Rules] the host's rules arrived from {Sender}: pvp={Pvp} autoContinue={AutoContinue} lateJoin={LateJoin} remoteAccess={RemoteAccess} memberCheatVerbs={MemberCheat} widenRunSettings={Widen} piggybackWeight={Piggyback:F2} parity={Parity} saveInventory={SaveInventory} reviveTrader={ReviveTrader} reviveNextLevel={ReviveNextLevel} permadeath={Permadeath}.",
			sender,
			set.PvpEnabled,
			set.AutoContinue,
			set.AllowLateJoin,
			set.RemoteBackpackAccess,
			set.AllowMemberCheatVerbs,
			set.WidenRunSettings,
			set.PiggybackWeightMultiplier,
			set.NativeBindingParity,
			set.SaveInventory,
			set.ReviveFromTrader,
			set.ReviveOnNextLevel,
			set.Permadeath);
	}
}
