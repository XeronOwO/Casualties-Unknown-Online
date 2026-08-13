using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The craft-report classification — PURE (no state, no logging, no sends):
/// the host's world table and the sender's transfer-table membership decide
/// what each entry means to the apply. Destroyed → where the item lives
/// decides the removal; Changed → world vs carried decides the adopt path.
/// Extracted so the classification is testable without a network (切离方法论);
/// the apply (CraftSyncService) composes the verdicts. Unbound ids (0) are
/// skipped — nothing to arbitrate. Host-only: the guests' tables are empty,
/// their relay apply is positional (Destroyed → scene removal, stamped
/// WorldCorrection → scene correction).
/// </summary>
internal static class CraftReportJudge
{
	internal static List<(ulong ItemId, CraftVerdict Verdict)> Classify(
		CraftReportMsg msg, HashSet<ulong> worldIds, HashSet<ulong> transferredIds)
	{
		var verdicts = new List<(ulong, CraftVerdict)>(msg.Entries.Count);
		foreach (var entry in msg.Entries)
		{
			var id = entry.Item.InstanceId;
			if (id == 0)
			{
				continue; // unbound — nothing to arbitrate
			}

			verdicts.Add((id, entry.Disposition switch
			{
				CraftEntryDisposition.Destroyed =>
					worldIds.Contains(id) ? CraftVerdict.WorldDestroy
					: transferredIds.Contains(id) ? CraftVerdict.TransferredRemove
					: CraftVerdict.UnknownSkip,
				_ =>
					worldIds.Contains(id) ? CraftVerdict.WorldChange
					: CraftVerdict.AdoptChange,
			}));
		}

		return verdicts;
	}
}
