// eval declaration: frame-seed
// declares: AcceptanceFrameSeed
// serves: unhooked-item-and-body-sound-families row 6 (the water shake) and any row whose game routine needs a state HELD, not set once
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AcceptanceFrameSeed
{
	public static int Frames;
	public static bool Running;
	public static string Mode = "";

	public static string Start(string mode, int frames)
	{
		var camera = PlayerCamera.main;
		if (camera == null || camera.body == null) { return "no-body"; }
		Mode = mode == null ? "" : mode;
		camera.StartCoroutine(Seed(frames));
		return "started";
	}

	// Why per frame: `Body.HandleDogWaterShaking` resets its twelve-second timer the moment `wetness > 30 && temperature < 36.6 && !wasWater` stops holding, and the
	// body's own simulation moves both numbers, so a one-shot seed never reaches the routine (measured 2026-11-01: sixteen seconds of watching answered plays=0 on
	// the acting client itself). Re-asserting the condition every frame is the same shape `bandage-ring2` uses for its own timed gesture.
	public static IEnumerator Seed(int frames)
	{
		Running = true;
		var camera = PlayerCamera.main;
		var body = camera.body;
		var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		var affectField = body.GetType().GetField("bodyAffect", flags);
		var affect = affectField == null ? null : affectField.GetValue(body);
		var wasWaterField = affect == null ? null : affect.GetType().GetField("wasWater", flags);

		for (var frame = 0; frame < frames; frame++)
		{
			if (Mode == "wet")
			{
				body.wetness = 100f;
				body.temperature = 36f;
				if (wasWaterField != null) { wasWaterField.SetValue(affect, false); }
			}

			Frames++;
			yield return null;
		}

		Running = false;
	}
}
