using System;
using Microsoft.Extensions.Logging;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The host's admission questions for member-reported world events: could the reporter's own body have been there?
/// <para>
/// It sits in its own type because the world-event class keeps accreting bounds and sits at the class-size cap; the
/// questions are one concern and belong together. An ABSENT position is not a refusal — accepting on absent evidence is
/// this family's model — so only a measured distance beyond <see cref="WorldReach.WorldUnits"/> refuses.
/// </para>
/// </summary>
internal sealed class BuildingReportAdmission(
	ISessionControl session,
	IEntitySyncControl entities,
	ILogger log)
{
	/// <summary>Could the reporter have reached this world point? The reach is shared, so a hand that can pick a lock is also one that can break the wall beside it.</summary>
	internal bool IsWithinReach(ulong sender, NetVector2 pos)
	{
		if (sender == session.LocalSteamId)
		{
			return true; // the host never receives its own report through this path
		}

		var entity = entities.GetRemotePlayer(sender);
		if (entity is null || entity.StateReceivedMs == int.MinValue)
		{
			return true; // no position for this peer yet — a state never received still reads as the origin
		}

		var dx = entity.Position.X - pos.X;
		var dy = entity.Position.Y - pos.Y;
		return Math.Sqrt((dx * dx) + (dy * dy)) <= WorldReach.WorldUnits;
	}

	/// <summary>
	/// The single admission seam for a reported OPEN, asked before anything is applied or recorded. An open is a lock
	/// being worked, so it is a gesture: the reporter's own body has to reach it. Only the host admits — a guest applying
	/// the host's broadcast is applying a fact already judged.
	/// </summary>
	internal bool IsOpenAdmissible(ulong sender, NetVector2 pos)
	{
		if (session.Role != SessionRole.Host || IsWithinReach(sender, pos))
		{
			return true;
		}

		log.LogWarning("[EntitySync] {Sender}'s open at ({X},{Y}) is further than {Reach} world units from its own body — refused: nothing applied, nothing recorded, nothing relayed.",
			sender, pos.X, pos.Y, WorldReach.WorldUnits);
		return false;
	}
}
