using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;
using CommitStatus = CasualtiesUnknownOnline.GameAdapter.Items.ItemReportCommitter.CommitStatus;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The Heater cooker's operation side (Heater.cs:41-49): the native collision
/// already ran in the host/solo scene — this domain verifies the created steak
/// fingerprint, stamps its instance id BEFORE Item.Start, commits ONE
/// ItemCook report (one operation = one message) and claims the raw meat's
/// end-of-frame destroy so the generic hooks never decompose the conversion
/// into an ItemDestroy + ItemSpawn pair. The patch stays thin; the capture
/// rules live in <see cref="HeaterCookRule"/>.
/// </summary>
internal sealed class HeaterCookSync(
	IItemControl items,
	ItemIdAllocator ids,
	ItemReportCommitter reports,
	OperationTrace trace,
	ILogger<HeaterCookSync> log)
{
	private readonly IItemControl _items = items;
	private readonly ItemIdAllocator _ids = ids;
	private readonly ItemReportCommitter _reports = reports;
	private readonly OperationTrace _trace = trace;
	private readonly ILogger<HeaterCookSync> _log = log;

	/// <summary>Raw-meat ids whose destroy fact already rode the ItemCook report — consumed exactly once by ShouldSuppressDestroy.</summary>
	private readonly HashSet<ulong> _claimedSources = [];

	/// <summary>
	/// The patch prefix verified the game's cook predicate and the native
	/// original is about to run. Return the raw item's instance id (allocating
	/// one when a generation-time meat enters the domain through the cooker) —
	/// 0 means "not reportable", and the generic item hooks stay the fallback.
	/// </summary>
	internal ulong OnCookCandidate(Item item)
	{
		if (CallContext.Current == CallContext.Origin.RemoteApply || HarmonyTraverse.IsGenerating())
		{
			return 0;
		}

		return _ids.EnsureId(item);
	}

	/// <summary>
	/// The patch postfix found the steak the native original just created. The
	/// verification is the complete fingerprint: the exact game id, the exact
	/// condition product, the exact spawn position and not yet registered in
	/// Item.allItems (its Start has not run in the same physics callback). A
	/// failed fingerprint is NOT reported — the generic destroy/spawn path
	/// remains the self-healing fallback.
	/// </summary>
	internal void OnCookCompleted(ulong sourceItemId, Item steak, float sourceCondition, Vector2 sourcePosition)
	{
		if (sourceItemId == 0 || steak == null // Unity object — ==
			|| steak.id != HeaterCookRule.CookedItemId
			|| Item.allItems.Contains(steak)
			|| !HeaterCookRule.IsCookedCondition(steak.condition, sourceCondition)
			|| !HeaterCookRule.IsCookedSpawnAt(steak.transform.position.x, steak.transform.position.y, sourcePosition.x, sourcePosition.y))
		{
			_log.LogWarning("[HeaterCook] capture verification failed for source {Source} — falling back to the generic item hooks.", sourceItemId);
			return;
		}

		var cookedId = _ids.Allocate(steak);
		var capture = ItemStateCodec.CaptureItem(steak, -1);
		var pos = steak.transform.position;
		var vel = steak.rb.velocity;
		var op = _trace.NextOperationId();
		_claimedSources.Add(sourceItemId);

		_reports.CommitReport(cookedId, op, "HeaterCook", CommitStatus.Committed,
			() =>
			{
				_items.SendItemCooked(sourceItemId, cookedId, capture,
					new NetVector2(pos.x, pos.y),
					new NetVector2(vel.x, vel.y),
					steak.transform.eulerAngles.z,
					steak.rb.angularVelocity);
				return 1;
			},
			"Cook");
	}

	/// <summary>The patch postfix could not identify the created steak — log it; the generic hooks stay the fallback and the operation remains visible.</summary>
	internal void OnCaptureFailed(ulong sourceItemId) =>
		_log.LogWarning("[HeaterCook] no matching created steak found for source {Source} — leaving the generic item hooks to report the conversion.", sourceItemId);

	/// <summary>True consumes the claim: this raw-meat destroy's fact already rode the ItemCook report.</summary>
	internal bool ShouldSuppressDestroy(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		return idComp != null && idComp.Id != 0 && _claimedSources.Remove(idComp.Id); // Unity object — ==
	}

	/// <summary>Session/world ended — stale claims die with the scene.</summary>
	internal void Reset() => _claimedSources.Clear();
}
