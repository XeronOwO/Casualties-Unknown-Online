using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// CrystalEnemy wind-up telegraph presentation bridge. The host's
/// <see cref="CrystalEnemy.Update"/> draws the pre-lunge LineRenderer beam from
/// the native <c>timeBeforeAttack</c> state (CrystalEnemy.cs:66-90), but a
/// frozen render copy skips that update, so the guest has no warning line.
/// This helper captures the host's wind-up amount and line end point into the
/// enemy stream and mirrors them (plus the native fade/width math) onto the
/// frozen copy's LineRenderer.
/// </summary>
internal static class CrystalWindupPresentation
{
	private const string TimeBeforeAttackFieldName = "timeBeforeAttack";
	private const string LineRendererFieldName = "rend";
	private const string SpriteRendererFieldName = "sprite";

	/// <summary>
	/// Host capture: how many seconds into the wind-up the crystal is
	/// (<c>-timeBeforeAttack</c>, 0 when idle). The native line becomes visible
	/// when this value is positive.
	/// </summary>
	internal static float CaptureAmount(CrystalEnemy? crystal)
	{
		if (crystal == null) // Unity object — ==
		{
			return 0f;
		}

		var timeBeforeAttack = Traverse.Create(crystal).Field(TimeBeforeAttackFieldName).GetValue<float>();
		return timeBeforeAttack < 0f ? -timeBeforeAttack : 0f;
	}

	/// <summary>
	/// Host capture: the current world-space end point of the telegraph line.
	/// Returns null when no line is active or the renderer is unavailable; the
	/// receiver keeps its own line clear in that case.
	/// </summary>
	internal static NetVector2? CaptureLineEnd(CrystalEnemy? crystal)
	{
		if (crystal == null) // Unity object — ==
		{
			return null;
		}

		var rend = GetLineRenderer(crystal);
		if (rend == null) // Unity object — ==
		{
			return null;
		}

		var end = rend.GetPosition(1);
		return new NetVector2(end.x, end.y);
	}

	/// <summary>
	/// Guest apply: reproduce the host's telegraph line (or clear it when the
	/// wind-up is over/reset). Returns true only when the visible/invisible
	/// state crossed a threshold so the caller can log the transition once.
	/// </summary>
	internal static bool Apply(BuildingEntity entity, float amount, NetVector2? lineEnd)
	{
		var crystal = entity.GetComponentInChildren<CrystalEnemy>();
		if (crystal == null) // Unity object — ==
		{
			return false;
		}

		var rend = GetLineRenderer(crystal);
		if (rend == null) // Unity object — ==
		{
			return false;
		}

		var driver = entity.GetComponent<RemoteEnemyDriver>();
		var wasActive = driver != null && driver.CrystalWindupAmount > 0f;
		var isActive = amount > 0f;
		if (driver != null) // Unity object — ==
		{
			driver.CrystalWindupAmount = amount;
		}

		if (!isActive)
		{
			rend.startColor = Color.clear;
			rend.endColor = Color.clear;
			return wasActive;
		}

		rend.SetPosition(0, entity.transform.position);
		if (lineEnd is { } end)
		{
			rend.SetPosition(1, new Vector3(end.X, end.Y, 0f));
		}

		var isStuck = driver != null && driver.Stunned;
		if (isStuck)
		{
			// Native stuck branch (CrystalEnemy.cs:97-110): after the lunge the
			// beam fades from the sprite color toward clear using
			// -timeBeforeAttack (= amount here).
			rend.startColor = Color.clear;
			rend.endColor = Color.Lerp(ReadSpriteColor(crystal), Color.clear, Mathf.Clamp01(amount));
			return !wasActive;
		}

		// Native math (CrystalEnemy.cs:83-84): alpha = -timeBeforeAttack * 2,
		// width = timeBeforeAttack * 2. With amount = -timeBeforeAttack this is
		// alpha = clamp(amount * 2), width = -alpha.
		var progress = Mathf.Clamp01(amount * 2f);
		rend.startColor = Color.Lerp(Color.clear, ReadSpriteColor(crystal), progress);
		rend.endColor = Color.clear;
		rend.widthMultiplier = -progress;
		return !wasActive;
	}

	private static LineRenderer? GetLineRenderer(CrystalEnemy crystal)
	{
		var rend = Traverse.Create(crystal).Field(LineRendererFieldName).GetValue();
		return rend as LineRenderer;
	}

	private static Color ReadSpriteColor(CrystalEnemy crystal)
	{
		var sprite = Traverse.Create(crystal).Field(SpriteRendererFieldName).GetValue();
		if (sprite == null) // engine object — ==
		{
			return Color.white;
		}

		var color = Traverse.Create(sprite).Property("color");
		return color.PropertyExists() ? color.GetValue<Color>() : Color.white;
	}
}
