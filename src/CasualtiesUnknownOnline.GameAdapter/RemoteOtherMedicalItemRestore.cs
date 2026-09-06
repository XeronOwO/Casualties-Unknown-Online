using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Tracks which Stage 3 remote item conditions have already been overwritten by
/// an authoritative host State/End. Cancel/rejection before the first
/// authoritative update restores the local original; after it, the local copy
/// must stay host-aligned.
/// </summary>
internal static class RemoteOtherMedicalItemRestore
{
	private static readonly HashSet<ulong> AuthoritativeItemIds = [];

	internal static void MarkApplied(ulong itemInstanceId) =>
		AuthoritativeItemIds.Add(itemInstanceId);

	internal static void Clear(ulong itemInstanceId) =>
		AuthoritativeItemIds.Remove(itemInstanceId);

	internal static void Restore(RemoteOtherUseSession session)
	{
		if (session.HasItem && !AuthoritativeItemIds.Contains(session.ItemInstanceId))
		{
			session.Item!.condition = session.OriginalCondition;
		}
	}
}
