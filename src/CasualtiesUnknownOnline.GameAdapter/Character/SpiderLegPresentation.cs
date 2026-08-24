using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Spider-leg IK presentation bridge. The host's <see cref="SpiderHandler.Update"/>
/// writes <c>IKHandle.rootPos</c>/<c>targetPos</c> every frame
/// (SpiderHandler.cs:57-58) before the IK renders the leg; a frozen render copy
/// skips that update, so the guest has no crawl. This helper captures the
/// host-side world-space target positions into the enemy stream and mirrors
/// them (plus the leg root) onto the frozen copy.
/// </summary>
internal static class SpiderLegPresentation
{
	/// <summary>
	/// Capture the host's current spider leg IK targets. Returns null for
	/// non-spiders or when no leg target data exists; the receiver then leaves
	/// its legs at their prefab/initial pose.
	/// </summary>
	internal static List<NetVector2>? Capture(SpiderHandler spider)
	{
		if (spider == null || spider.legs == null || spider.legs.Length == 0) // Unity object — ==
		{
			return null;
		}

		var targets = new List<NetVector2>(spider.legs.Length);
		foreach (var leg in spider.legs)
		{
			if (leg == null) // Unity object — ==
			{
				continue;
			}

			targets.Add(new NetVector2(leg.targetPos.x, leg.targetPos.y));
		}

		return targets.Count == 0 ? null : targets;
	}

	/// <summary>
	/// Apply the host-captured leg targets to a frozen spider copy. The root is
	/// re-derived from the copy's own leg transform (the entity transform is
	/// already host-driven), so only the target positions need to travel.
	/// </summary>
	internal static void Apply(BuildingEntity entity, IReadOnlyList<NetVector2>? targets)
	{
		if (targets == null || targets.Count == 0)
		{
			return;
		}

		var spider = entity.GetComponentInChildren<SpiderHandler>();
		if (spider == null || spider.legs == null || spider.legs.Length != targets.Count) // Unity object — ==
		{
			return;
		}

		for (var i = 0; i < spider.legs.Length; i++)
		{
			var leg = spider.legs[i];
			if (leg == null) // Unity object — ==
			{
				continue;
			}

			var root = leg.transform.position;
			leg.rootPos = new Vector2(root.x, root.y);
			leg.targetPos = new Vector2(targets[i].X, targets[i].Y);
		}
	}
}
