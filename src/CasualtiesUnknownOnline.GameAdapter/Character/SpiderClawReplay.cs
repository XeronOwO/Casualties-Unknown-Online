using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Replays the spider bite claw animation (SpiderHandler.CheckForLimbDamage,
/// SpiderHandler.cs:201-208) outside the native collision path. A frozen
/// spider copy on a guest never runs <c>CheckForLimbDamage</c>, and a host
/// ordering a remote bite never has the guest's local collision contact normal,
/// so both sides replay the same one-shot visual from the enemy-to-victim
/// direction.
/// </summary>
internal static class SpiderClawReplay
{
	/// <summary>Play the ClawAnim prefab at the spider, aimed at the victim direction.</summary>
	internal static void Play(SpiderHandler spider, Vector2 direction)
	{
		if (spider == null || !spider.clawAnim || direction.sqrMagnitude < 0.0001f) // Unity object — ==
		{
			return;
		}

		var prefab = Resources.Load<GameObject>("ClawAnim");
		if (prefab == null) // Unity object — ==
		{
			return;
		}

		var anim = Object.Instantiate(prefab);
		anim.transform.eulerAngles = new Vector3(0f, 0f, Vector2.SignedAngle(Vector2.right, direction.normalized));
		anim.transform.position = spider.transform.position;
		anim.transform.SetParent(spider.transform);
		Object.Destroy(anim, 5f);
	}
}
