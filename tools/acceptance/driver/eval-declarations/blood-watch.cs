// eval declaration: blood-watch
// declares: AcceptanceBloodWatch
// serves: remote-medical-native-minigame-parity (a reported +/-0.01 jitter on the remote panel's blood line)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AcceptanceBloodWatch
{
	public static List<float> Samples = new List<float>();
	public static bool Running;
	public static string Error = "";

	public static string Start(int frames)
	{
		var view = WoundView.view;
		if (view == null || view.body == null) { return "no-wound-view"; }
		Samples = new List<float>();
		Error = "";
		PlayerCamera.main.StartCoroutine(Watch(frames));
		return "started";
	}

	public static IEnumerator Watch(int frames)
	{
		Running = true;
		var view = WoundView.view;
		for (var frame = 0; frame < frames; frame++)
		{
			if (view == null || view.body == null) { Error = "view-gone"; break; }
			Samples.Add(view.body.bloodVolume);
			yield return null;
		}

		Running = false;
	}

	public static string Report()
	{
		var min = float.MaxValue;
		var max = float.MinValue;
		foreach (var value in Samples)
		{
			if (value < min) { min = value; }
			if (value > max) { max = value; }
		}

		var series = "";
		for (var i = 0; i < Samples.Count && i < 24; i++)
		{
			series += Samples[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + (i == 23 ? "" : "|");
		}

		// A falling value is not a jitter: what the player reports is the frame-to-frame oscillation ON TOP of the trend, so the
		// report carries the reversals (sign changes of the delta) and the largest single-frame step.
		var reversals = 0;
		var maxDelta = 0f;
		var lastSign = 0;
		for (var i = 1; i < Samples.Count; i++)
		{
			var delta = Samples[i] - Samples[i - 1];
			var magnitude = delta < 0f ? -delta : delta;
			if (magnitude > maxDelta) { maxDelta = magnitude; }
			var sign = delta > 0f ? 1 : (delta < 0f ? -1 : lastSign);
			if (sign != 0 && lastSign != 0 && sign != lastSign) { reversals++; }
			if (sign != 0) { lastSign = sign; }
		}

		return "{\"reversals\":" + reversals
			+ ",\"maxDelta\":" + maxDelta.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"ok\":true,\"running\":" + (Running ? "true" : "false")
			+ ",\"samples\":" + Samples.Count
			+ ",\"min\":" + (Samples.Count == 0 ? "0" : min.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
			+ ",\"max\":" + (Samples.Count == 0 ? "0" : max.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
			+ ",\"series\":\"" + series + "\",\"error\":\"" + Error + "\"}";
	}
}