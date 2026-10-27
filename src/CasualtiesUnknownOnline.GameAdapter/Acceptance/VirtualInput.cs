using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityTime = UnityEngine.Time;

namespace CasualtiesUnknownOnline.GameAdapter.Acceptance;

/// <summary>
/// The acceptance channel's VIRTUAL MOUSE: a Harmony postfix on <c>UnityEngine.Input.mousePosition</c> plus prefixes on the three mouse-button
/// methods, answering a staged state while a batch has one and the real input otherwise. It exists because OS-level input costs the user a permission
/// every batch, and because a row that must see a click or a drag the batch cannot produce with a real mouse has no other instrument.
///
/// <para>
/// WHAT IS PATCHABLE AND WHY (measured on this build's <c>UnityEngine.InputLegacyModule</c>):
/// <c>mousePosition</c> is a MANAGED getter (<c>Input.get_mousePosition_Injected(out Vector3)</c>) — a body the ordinary managed patcher detours.
/// <c>GetMouseButton</c>, <c>GetMouseButtonDown</c> and <c>GetMouseButtonUp</c> are <c>extern</c> + <c>[MethodImpl(InternalCall)]</c>: no IL body, which the
/// managed patcher cannot detour. HarmonyX ships a NATIVE detour backend for exactly that shape, so "no IL" is not a verdict — it is a reason to try the
/// native path and RECORD what the backend answers. <see cref="Install"/> therefore patches each entry separately and reports per method, so a refusal is
/// named (which method, which exception) rather than guessed from the absence of a body.
/// </para>
///
/// <para>
/// FRAME SEMANTICS: a press is DOWN for exactly the frame it is pressed in and HELD afterwards; a release is UP for exactly the frame it is released in and
/// nothing afterwards. Every query in the same frame answers the same value (the state is stamped, never consumed), because a click that reports down to
/// the game but not to the UI would be a defect the batch could never see. The batch advances the frame itself: <see cref="Press"/> and
/// <see cref="Release"/> stamp the CURRENT <c>Time.frameCount</c>.
/// </para>
///
/// <para>
/// OFF BY DEFAULT AND NEVER A PRODUCT FEATURE: nothing calls <see cref="Install"/> except the acceptance driver's recipe, <see cref="Uninstall"/> puts the
/// real input back, and every prefix answers "not mine" whenever no batch staged a state. A player's session never reaches this type.
/// </para>
/// </summary>
internal static class VirtualInput
{
	private const string HarmonyId = "CasualtiesUnknownOnline.GameAdapter.VirtualInput";

	/// <summary>One button's staged state, with the frame each transition happened in.</summary>
	private sealed class ButtonState
	{
		internal bool Held;
		internal int DownFrame = -1;
		internal int UpFrame = -1;
	}

	private static readonly Dictionary<int, ButtonState> Buttons = [];
	private static readonly (string Name, string PrefixName)[] ButtonEntries =
	[
		(nameof(Input.GetMouseButton), nameof(PrefixHeld)),
		(nameof(Input.GetMouseButtonDown), nameof(PrefixDown)),
		(nameof(Input.GetMouseButtonUp), nameof(PrefixUp)),
	];

	private static Harmony? _harmony;
	private static Vector3? _pointer;
	private static int _buttonCensus;

	/// <summary>True while a staged pointer is in force.</summary>
	internal static bool Enabled => _pointer.HasValue;

	/// <summary>True once the pointer patch is installed.</summary>
	internal static bool Installed => _harmony != null;

	/// <summary>How many button methods the backend accepted (0 — the pointer still works — up to 3).</summary>
	internal static int ButtonPatches => _buttonCensus;

	/// <summary>
	/// Installs the patches and answers one report line per target: the pointer's outcome and each button method's own. The report IS the result — a method
	/// the backend refuses must be named with its exception, so the next step is decided on evidence.
	/// </summary>
	internal static string Install(out bool installed)
	{
		installed = false;
		if (_harmony != null)
		{
			installed = true;
			return "already installed";
		}

		var report = new List<string>();
		var harmony = new Harmony(HarmonyId);
		var failures = 0;

		var getter = AccessTools.PropertyGetter(typeof(Input), nameof(Input.mousePosition));
		if (getter == null)
		{
			report.Add("mousePosition: no getter on this build");
			failures++;
		}
		else
		{
			try
			{
				harmony.Patch(getter, postfix: new HarmonyMethod(typeof(VirtualInput), nameof(PostfixPointer)));
				report.Add("mousePosition: patched (" + Describe(getter) + ")");
			}
			catch (Exception exception)
			{
				report.Add("mousePosition: FAILED " + DescribeFailure(exception));
				failures++;
			}
		}

		_buttonCensus = 0;
		foreach (var entry in ButtonEntries)
		{
			var method = AccessTools.Method(typeof(Input), entry.Name, [typeof(int)]);
			if (method == null)
			{
				report.Add(entry.Name + ": no method on this build");
				continue;
			}

			try
			{
				harmony.Patch(method, prefix: new HarmonyMethod(typeof(VirtualInput), entry.PrefixName));
				_buttonCensus++;
				report.Add(entry.Name + ": patched (" + Describe(method) + ")");
			}
			catch (Exception exception)
			{
				report.Add(entry.Name + ": FAILED " + DescribeFailure(exception));
			}
		}

		_harmony = harmony;
		installed = failures == 0;
		return string.Join(" | ", report);
	}

	/// <summary>Removes every patch and the staged state in one step; the real input answers again immediately.</summary>
	internal static void Uninstall()
	{
		_pointer = null;
		Buttons.Clear();
		_buttonCensus = 0;
		if (_harmony == null)
		{
			return;
		}

		_harmony.UnpatchSelf();
		_harmony = null;
	}

	/// <summary>Stages the pointer at a screen position (the game's own coordinates: origin bottom-left).</summary>
	internal static void MoveTo(float x, float y) => _pointer = new Vector3(x, y, 0f);

	/// <summary>Drops the staged pointer, so the real one answers without uninstalling the patch.</summary>
	internal static void Clear() => _pointer = null;

	/// <summary>
	/// Presses a button: HELD from now on, DOWN for this frame only. A second press of an already-held button keeps its original press frame, so a
	/// repeated call cannot turn one click into two.
	/// </summary>
	internal static void Press(int button)
	{
		if (!Buttons.TryGetValue(button, out var state))
		{
			state = new ButtonState();
			Buttons[button] = state;
		}

		if (!state.Held)
		{
			state.Held = true;
			state.DownFrame = UnityTime.frameCount;
			state.UpFrame = -1;
		}
	}

	/// <summary>Releases a button: UP for this frame only, then nothing.</summary>
	internal static void Release(int button)
	{
		if (!Buttons.TryGetValue(button, out var state))
		{
			state = new ButtonState();
			Buttons[button] = state;
		}

		if (state.Held)
		{
			state.Held = false;
			state.UpFrame = UnityTime.frameCount;
		}
	}

	/// <summary>Drops every staged button (the pointer keeps its own staged value).</summary>
	internal static void ClearButtons() => Buttons.Clear();

	/// <summary>The staged pointer, when a batch has one.</summary>
	internal static bool TryGetStaged(out Vector3 position)
	{
		if (_pointer is { } staged)
		{
			position = staged;
			return true;
		}

		position = default;
		return false;
	}

	/// <summary>What the staged state answers for a button and a query, when a batch staged that button at all.</summary>
	internal static bool TryAnswer(int button, ButtonQuery query, out bool value)
	{
		if (!Buttons.TryGetValue(button, out var state))
		{
			value = false;
			return false;
		}

		var frame = UnityTime.frameCount;
		value = query switch
		{
			ButtonQuery.Held => state.Held,
			ButtonQuery.Down => state.DownFrame == frame,
			ButtonQuery.Up => state.UpFrame == frame,
			_ => false,
		};
		return true;
	}

	/// <summary>Which of the three questions a prefix is answering.</summary>
	internal enum ButtonQuery
	{
		Held,
		Down,
		Up,
	}

	/// <summary>The postfix Harmony calls for the pointer: replaces the getter's answer while a batch has staged one.</summary>
	private static void PostfixPointer(ref Vector3 __result)
	{
		if (_pointer is { } staged)
		{
			__result = staged;
		}
	}

	private static bool PrefixHeld(int button, ref bool __result) => Answer(button, ButtonQuery.Held, ref __result);

	private static bool PrefixDown(int button, ref bool __result) => Answer(button, ButtonQuery.Down, ref __result);

	private static bool PrefixUp(int button, ref bool __result) => Answer(button, ButtonQuery.Up, ref __result);

	private static bool Answer(int button, ButtonQuery query, ref bool __result)
	{
		if (!TryAnswer(button, query, out var value))
		{
			return true; // not staged: the real method answers
		}

		__result = value;
		return false; // the real method is skipped
	}

	private static string Describe(MethodInfo method) =>
		method.GetMethodBody() == null ? "no IL body — native backend" : "managed body";

	private static string DescribeFailure(Exception exception) =>
		exception.GetType().Name + ": " + exception.Message;
}
