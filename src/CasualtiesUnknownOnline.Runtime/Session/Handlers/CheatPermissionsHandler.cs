using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → member: the host's cheat-permission roster, absolute. The receiver stores
/// the whole list, so its own console refuses a cheat verb against what the host is
/// actually granting instead of a list this process invented.
///
/// <para>
/// The announcement is host → member only, and the receiver says so: a frame that
/// arrives while this process is the host is dropped and named. The wire direction is
/// already enforced at the receiver (the message registry's declared direction), so
/// this branch is the invariant made observable rather than the boundary itself.
/// </para>
///
/// <para>
/// A repeat is the ordinary case, not an anomaly: the entry group and the repair group
/// re-send the same roster on purpose. The line that matters is the one that names the
/// LOCAL player's standing, because that is the fact the console reads next.
/// </para>
/// </summary>
[PacketHandler(NetMsg.CheatPermissions, NetMessageDirection.HostToGuest)]
public sealed class CheatPermissionsHandler(ILogger<CheatPermissionsHandler> log)
	: PacketHandlerBase<CheatPermissionsMsg, ICheatPermissionHandlerContext>
{
	protected override void Handle(ulong sender, CheatPermissionsMsg msg, ICheatPermissionHandlerContext ctx)
	{
		if (ctx.Session.Role != SessionRole.Guest)
		{
			log.LogWarning("[Cheat] a roster announcement arrived from {Sender} while this process is {Role} — dropped (the announcement is host → member only).",
				sender, ctx.Session.Role);
			return;
		}

		if (sender != ctx.Session.HostSteamId)
		{
			// The direction check above only proves the LOCAL role. A frame whose sender is
			// not this session's host is a CLAIM, and honouring it would let one member
			// narrow another member's roster (the local gate reads this store, so a forged
			// closed list refuses verbs the host would admit). The host's own answer still
			// settles every request, so this is a griefing surface rather than an
			// authorization bypass — dropped by name all the same.
			log.LogWarning("[Cheat] a roster announcement from {Sender} was dropped: it is not this session's host ({Host}).",
				sender, ctx.Session.HostSteamId);
			return;
		}

		if (msg.Malformed() is { } malformed)
		{
			log.LogWarning("[Cheat] the roster from {Sender} was refused: {Reason} — this client keeps the list it holds.", sender, malformed);
			return;
		}

		var granted = msg.ToList();
		if (!ctx.CheatPermissions.Apply(granted))
		{
			log.LogDebug("[Cheat] the roster from {Sender} repeats the list this client already holds ({Count} member(s)).", sender, granted.Count);
			return;
		}

		log.LogInformation("[Cheat] the host's roster arrived from {Sender}: {Count} member(s) hold the cheat verbs, and this client {Standing}.",
			sender, granted.Count, ctx.CheatPermissions.Refuses(ctx.Session.LocalSteamId) ? "is not one of them" : "is");
	}
}
