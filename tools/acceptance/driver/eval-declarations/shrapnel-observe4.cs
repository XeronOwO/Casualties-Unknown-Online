// eval declaration: shrapnel-observe4
// declares: AcceptanceShrapnelObserve4
// serves: remote-medical-stage-2-shrapnel-multiplayer row 4 (a third party sees authoritative piece positions).
//
// Reads the piece rows of the shrapnel screen this client is showing. The host marks a removed piece by writing
// `8000 + index` into its y (`RemoteShrapnelOperationHandler.ApplyStateToMinigame`), so the observer's own screen
// shows the authority when a removed piece reads in the 8000s and a live piece keeps its own y.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AcceptanceShrapnelObserve4
{
	static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	/// <summary>One line per piece: `index:y`, or the reason there is nothing to read.</summary>
	public static string Positions()
	{
		try
		{
			if (MinigameBase.main == null)
			{
				return "no-main";
			}

			var minigame = MinigameBase.main.currentMinigame;
			if (minigame == null)
			{
				return "no-minigame";
			}

			if (!(minigame is ShrapnelMinigame))
			{
				return "kind:" + minigame.GetType().Name;
			}

			var field = minigame.GetType().GetField("objects", Flags);
			if (field == null)
			{
				return "no-objects-field";
			}

			var objects = field.GetValue(minigame) as List<RectTransform>;
			if (objects == null)
			{
				return "no-objects";
			}

			var parts = new List<string>();
			for (var i = 0; i < objects.Count; i++)
			{
				var y = objects[i].anchoredPosition.y;
				parts.Add(i.ToString() + ":" + y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
			}

			return string.Join(",", parts.ToArray());
		}
		catch (Exception ex)
		{
			return "threw:" + ex.GetType().Name + ":" + ex.Message;
		}
	}
}
