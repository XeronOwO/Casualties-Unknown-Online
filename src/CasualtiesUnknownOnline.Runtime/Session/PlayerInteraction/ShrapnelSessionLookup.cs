using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Resolves a shared shrapnel session by operation id. Sessions are keyed by
/// target limb because the domain allows multiple operators on one limb; the
/// operation id is the wire/public handle, so this small lookup is shared by
/// every host handler.
/// </summary>
internal static class ShrapnelSessionLookup
{
	internal static bool TryGet(
		IReadOnlyDictionary<(ulong Target, int Limb), ShrapnelOperationSession> sessions,
		ulong operationId,
		out ShrapnelOperationSession? shrapnel)
	{
		foreach (var session in sessions.Values)
		{
			if (session.OperationId == operationId)
			{
				shrapnel = session;
				return true;
			}
		}

		shrapnel = null;
		return false;
	}
}
