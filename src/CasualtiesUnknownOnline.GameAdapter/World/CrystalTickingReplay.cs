using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Replays the unstable crystal's 5 s pre-explosion ticking on a receiving
/// side (the peer saw the crystal start to tick): the glow ramp
/// (crystal.light.intensity += dt * 4, CrystalUnstable.cs:45) and the jitter
/// (position = origPos + Random.insideUnitCircle * timer * 0.07f,
/// CrystalUnstable.cs:46) driven from THIS side's OWN elapsed clock — the
/// game's private timerStarted/timer latches are deliberately NOT written
/// (a written timerStarted would make the local CrystalUnstable.Update count
/// down and explode the crystal naturally, double-applying the world effects
/// that the CrystalUnstableExploded event already replays — the same rule as
/// MinePressReplayMarker for the mine's `pressed` latch). The tick sound and
/// the transient guard ride alongside: the component's presence IS the
/// duplicate guard (a second Ticked event for the same crystal drops); the
/// 5 s clock mirrors the native countdown so the visual ends exactly when the
/// explosion replay destroys the crystal.
/// </summary>
internal sealed class CrystalTickingReplay : MonoBehaviour
{
	private const float TickSeconds = 5f; // CrystalUnstable.cs:47 — the pre-explosion window

	private const float LightRampPerSecond = 4f; // CrystalUnstable.cs:45

	private const float JitterPerSecond = 0.07f; // CrystalUnstable.cs:46

	private Vector2 _origin;

	private float _elapsed;

	private object? _light; // the crystal's private Light2D — read through reflection (URP assembly, not referenceable)

	/// <summary>True when the ticking visual is already replaying on this crystal (the duplicate guard).</summary>
	internal static bool IsPresent(CrystalBehaviour crystal) => crystal.GetComponent<CrystalTickingReplay>() != null; // Unity object — ==

	/// <summary>Start the 5 s ticking visual on a crystal (called from the shared replay action).</summary>
	internal static void Begin(CrystalBehaviour crystal)
	{
		var replay = crystal.gameObject.AddComponent<CrystalTickingReplay>();
		replay._origin = crystal.transform.position;
		var lightField = Traverse.Create(crystal).Field("light");
		replay._light = lightField.FieldExists() ? lightField.GetValue() : null;
	}

	private void Update()
	{
		if (_elapsed >= TickSeconds)
		{
			transform.position = _origin; // the native crystal is gone at 5 s — restore the resting position before the explosion replay destroys it
			Destroy(this); // the visual is done; the CrystalUnstableExploded replay owns the destruction
			return;
		}

		_elapsed += Time.deltaTime;
		RampLight();
		transform.position = _origin + Random.insideUnitCircle * (_elapsed * JitterPerSecond);
	}

	private void RampLight()
	{
		if (_light == null)
		{
			return;
		}

		var intensity = Traverse.Create(_light).Property("intensity");
		if (intensity.PropertyExists())
		{
			intensity.SetValue(intensity.GetValue<float>() + (Time.deltaTime * LightRampPerSecond));
		}
	}
}
