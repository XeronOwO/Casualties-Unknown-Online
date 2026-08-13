namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// How the host applies ONE craft-report entry (CraftReportJudge's output).
/// The classification is pure table membership — the apply (CraftSyncService)
/// executes the verdict's side effects.
/// </summary>
internal enum CraftVerdict
{
	/// <summary>A world item was consumed — remove it from the world table (the relay notifies the peers' scene copies).</summary>
	WorldDestroy,

	/// <summary>A carried item was destroyed — remove it from the sender's transfer table (the ghost would otherwise resurrect on reconnect).</summary>
	TransferredRemove,

	/// <summary>No table membership — the sender consumed something we never tracked (a race with another guest's pickup, or never registered): skip + warn. Never rejected — the consumption is irreversible on the sender.</summary>
	UnknownSkip,

	/// <summary>A world item's state changed (condition/liquids — a destroyItem=false floor material) — adopt into the world table + local correction.</summary>
	WorldChange,

	/// <summary>A carried item's state changed — adopt over the transfer-table entry (the sender is the fact source for its own inventory).</summary>
	AdoptChange,
}
