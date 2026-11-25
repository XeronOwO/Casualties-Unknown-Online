using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Acceptance;

/// <summary>
/// Records the minigame's own hand state once per frame while a batch holds a press, because a single reading at the end cannot
/// say WHEN the hand stopped: the bandage row held the attack for 34 s, read <c>handClicking: true, handStarted: false</c> and
/// got 0.9 units of bandaging, and neither the press census (<c>keyDowns 4</c> over 17,875 key questions) nor the final frame
/// says whether the start edge fired once and was cleared, never fired, or fired and the hold dropped on the next frame
/// (measured 2026-11-26, <c>docs/backlog/todo/1.0-the-bandage-hand-never-holds-the-wrap.md</c>).
/// </summary>
/// <remarks>
/// It only READS: a postfix on the game's own <c>MinigameBase.Update</c> copies three fields into a ring buffer that the
/// <c>minigame-frames</c> action returns. A frame is kept with its own index and the time since install, so a row can line the
/// presses it staged up against what the hand did.
/// </remarks>
internal static class MinigameFrameRecorder
{
	private const int Capacity = 400;

	private static readonly Queue<string> Frames = new();

	private static readonly Stopwatch Clock = Stopwatch.StartNew();

	private static FieldInfo? _handStartedField;

	private static FieldInfo? _holdingField;

	private static FieldInfo? _handField;

	private static bool _fieldLookupDone;

	/// <summary>The frames kept so far, oldest first, one per line.</summary>
	internal static string Read()
	{
		lock (Frames)
		{
			return Frames.Count == 0 ? string.Empty : string.Join("\n", Frames);
		}
	}

	/// <summary>Drops the frames kept so far, so a row reads only the frames it caused.</summary>
	internal static void Clear()
	{
		lock (Frames)
		{
			Frames.Clear();
		}
	}

	/// <summary>Takes one frame; called from the postfix below, never from a row.</summary>
	internal static void Note(MinigameBase minigame)
	{
		if (minigame == null)
		{
			return;
		}

		LookupFields();

		var started = _handStartedField != null ? _handStartedField.GetValue(minigame) as bool? ?? false : false;
		var holding = _holdingField != null ? _holdingField.GetValue(minigame) as bool? ?? false : false;
		var handTransform = _handField != null ? _handField.GetValue(minigame) as Transform : null;
		var line = Clock.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)
			+ " started=" + (started ? "true" : "false")
			+ " holding=" + (holding ? "true" : "false")
			+ " handAngle=" + (handTransform != null
				? handTransform.localEulerAngles.z.ToString("0.0#", CultureInfo.InvariantCulture)
				: "none")
			+ " minigame=" + (minigame.currentMinigame != null ? minigame.currentMinigame.GetType().Name : "none");

		lock (Frames)
		{
			Frames.Enqueue(line);
			while (Frames.Count > Capacity)
			{
				Frames.Dequeue();
			}
		}
	}

	private static void LookupFields()
	{
		if (_fieldLookupDone)
		{
			return;
		}

		_fieldLookupDone = true;
		_handStartedField = typeof(MinigameBase).GetField(
			"handStartedClicking",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		_holdingField = typeof(BandageMinigame).GetField(
			"holdingBandage",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		_handField = typeof(MinigameBase).GetField(
			"hand",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? typeof(MinigameBase).GetField(
				"handTransform",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	}

	/// <summary>
	/// Patches the game's own per-frame minigame update. A build that does not declare <c>Update</c> is reported, never guessed at.
	/// </summary>
	internal static string Install(Harmony harmony)
	{
		var update = AccessTools.Method(typeof(MinigameBase), "Update", Type.EmptyTypes);
		if (update == null)
		{
			return "MinigameBase.Update: no method on this build, so the hand is not recorded per frame";
		}

		try
		{
			harmony.Patch(update, postfix: new HarmonyMethod(typeof(MinigameFrameRecorder), nameof(PostfixUpdate)));
			return "MinigameBase.Update: patched (handStarted/holding/handAngle recorded per frame)";
		}
		catch (Exception exception)
		{
			return "MinigameBase.Update: FAILED " + exception.GetType().Name + ": " + exception.Message;
		}
	}

	private static void PostfixUpdate(MinigameBase __instance) => Note(__instance);
}
