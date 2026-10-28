using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
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

	/// <summary>
	/// Keys a batch staged for the KEYBOARD (the inventory key, the console key, …): the game reads bindings through `Input.GetKeyDown(KeyCode)`, so a
	/// virtual KEY is the same mechanism as a virtual MOUSE button — one staged state per key, answered by the same three prefixes. Empty until a batch
	/// stages one: every other key is the real keyboard's.
	/// </summary>
	private static readonly Dictionary<KeyCode, ButtonState> Keys = [];
	private static readonly (string Name, string PrefixName)[] ButtonEntries =
	[
		(nameof(Input.GetMouseButton), nameof(PrefixHeld)),
		(nameof(Input.GetMouseButtonDown), nameof(PrefixDown)),
		(nameof(Input.GetMouseButtonUp), nameof(PrefixUp)),
	];

	/// <summary>
	/// The game's key read, replaced at its CALL SITE inside <c>PlayerCamera.HandleInput</c> (see <see cref="TranspileHandleInput"/>). The three questions are
	/// answered from the staged state, and every query is counted whether or not a key is staged, so a zero count means "the game never asked", never "the
	/// count looked at the wrong thing".
	/// </summary>
	private static int _handleInputReplacements;

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

		// The game's own key path: the CALL SITES inside PlayerCamera.HandleInput, not UnityEngine.Input itself. A detour on Input's own methods was installed
		// successfully and never invoked (0 queries in 300+ frames while the mouse internals answered 28), so the read is replaced where the game makes it:
		// one `call Input.GetKey*(KeyCode)` becomes one call to the probe below, and every query is counted whether or not a key is staged. The game's own
		// branch — GetBind → the query → HandleStartDragging/HandleReleaseDragging — still decides what happens; only the ANSWER to the query is staged.
		var handleInput = AccessTools.Method(typeof(PlayerCamera), "HandleInput", Type.EmptyTypes);
		if (handleInput == null)
		{
			report.Add("PlayerCamera.HandleInput: no method on this build");
		}
		else
		{
			try
			{
				_handleInputReplacements = 0;
				harmony.Patch(
					handleInput,
					prefix: new HarmonyMethod(typeof(VirtualInput), nameof(PrefixHandleInput)),
					transpiler: new HarmonyMethod(typeof(VirtualInput), nameof(TranspileHandleInput)));
				report.Add("PlayerCamera.HandleInput: patched, " + _handleInputReplacements.ToString(CultureInfo.InvariantCulture) + " call(s) replaced");
			}
			catch (Exception exception)
			{
				report.Add("PlayerCamera.HandleInput: FAILED " + DescribeFailure(exception));
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

			// The NEXT frame, not this one: the eval that stages a press can run AFTER the game's own Update in its frame (measured 2026-10-27: a
			// same-frame stamp was never seen by `PlayerCamera`'s `Input.GetKeyDown(attack)` and the click did nothing), so the press is stamped for
			// the frame the game will read next.
			state.DownFrame = UnityTime.frameCount + 1;
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
			state.UpFrame = UnityTime.frameCount + 1;
		}
	}

	/// <summary>Drops every staged button (the pointer keeps its own staged value).</summary>
	internal static void ClearButtons() => Buttons.Clear();

	/// <summary>
	/// Presses one KEY for the game's own reads: HELD from now on, DOWN on the frame the game reads next. Same stamping rule as a button, and for the
	/// same reason — the eval that stages a press can run after the game's own Update for that frame.
	/// </summary>
	internal static void PressKey(KeyCode key)
	{
		if (!Keys.TryGetValue(key, out var state))
		{
			state = new ButtonState();
			Keys[key] = state;
		}

		if (!state.Held)
		{
			state.Held = true;
			state.DownFrame = UnityTime.frameCount + 1;
			state.UpFrame = -1;
		}
	}

	/// <summary>Releases one staged key: UP on the frame the game reads next, then nothing.</summary>
	internal static void ReleaseKey(KeyCode key)
	{
		if (!Keys.TryGetValue(key, out var state))
		{
			state = new ButtonState();
			Keys[key] = state;
		}

		if (state.Held)
		{
			state.Held = false;
			state.UpFrame = UnityTime.frameCount + 1;
		}
	}

	/// <summary>Drops every staged key, so the real keyboard answers again.</summary>
	internal static void ClearKeys() => Keys.Clear();

	/// <summary>How many keys a batch currently stages (the install report and the acceptance reads quote it).</summary>
	internal static int StagedKeyCount => Keys.Count;

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

	private static bool PrefixKeyHeld(KeyCode key, ref bool __result) => AnswerKey(key, ButtonQuery.Held, ref __result);

	private static bool PrefixKeyDown(KeyCode key, ref bool __result) => AnswerKey(key, ButtonQuery.Down, ref __result);

	private static bool PrefixKeyUp(KeyCode key, ref bool __result) => AnswerKey(key, ButtonQuery.Up, ref __result);

	/// <summary>The same three questions, called from the REPLACED call sites inside <c>PlayerCamera.HandleInput</c>; every query is counted.</summary>
	private static bool PrefixWrapperHeld(KeyCode key, ref bool __result) => AnswerWrapperKey(key, ButtonQuery.Held, ref __result);

	private static bool PrefixWrapperDown(KeyCode key, ref bool __result) => AnswerWrapperKey(key, ButtonQuery.Down, ref __result);

	private static bool PrefixWrapperUp(KeyCode key, ref bool __result) => AnswerWrapperKey(key, ButtonQuery.Up, ref __result);

	private static bool AnswerWrapperKey(KeyCode key, ButtonQuery query, ref bool __result)
	{
		WrapperKeyQueries++;
		return AnswerKey(key, query, ref __result);
	}

	/// <summary>
	/// How many KEY questions the GAME asked for a staged key, and how many of those were answered "down". A batch reads these to separate "my press
	/// never reached the game" from "the game asked and the branch did nothing" — the two causes a single frame's state cannot tell apart
	/// (2026-10-28: `Input.GetKey(Tab)` answered true while `PlayerCamera.radialOpen` stayed false, and only the census could say which half failed).
	/// </summary>
	internal static int KeyQueries { get; private set; }

	/// <summary>How many of those KEY questions the staged state answered with "down".</summary>
	internal static int KeyDownAnswers { get; private set; }

	/// <summary>
	/// How many MOUSE-BUTTON questions the game asked, and how many were answered "down": the same census for the mouse internals, because the two
	/// families share one backend and a key patch that never runs next to a button patch that does is a fact about the TARGETS, not about the input
	/// (read 2026-10-28, when the key detour answered nothing while the mouse one was believed live).
	/// </summary>
	internal static int ButtonQueries { get; private set; }

	/// <summary>How many of those button questions the staged state answered with "down".</summary>
	internal static int ButtonDownAnswers { get; private set; }

	/// <summary>How many KEY questions arrived through the PUBLIC <c>GetKey(KeyCode)</c> wrappers (the counters above belong to the <c>*Int</c> internals).</summary>
	internal static int WrapperKeyQueries { get; private set; }

	/// <summary>
	/// Answers a KEY question for a staged KEY first, then for a staged MOUSE button (Mouse0 = 323, the value the game's `attack` bind answers): any
	/// other key is the real keyboard's, so a batch can never type for the game by accident.
	/// </summary>
	private static bool AnswerKey(KeyCode key, ButtonQuery query, ref bool __result)
	{
		if (Keys.TryGetValue(key, out var state))
		{
			var frame = UnityTime.frameCount;
			__result = query switch
			{
				ButtonQuery.Held => state.Held,
				ButtonQuery.Down => state.DownFrame == frame,
				ButtonQuery.Up => state.UpFrame == frame,
				_ => false,
			};

			KeyQueries++;
			if (query == ButtonQuery.Down && __result)
			{
				KeyDownAnswers++;
			}

			return false; // the real method is skipped for THIS key
		}

		var button = key switch
		{
			KeyCode.Mouse0 => 0,
			KeyCode.Mouse1 => 1,
			KeyCode.Mouse2 => 2,
			_ => -1,
		};

		return button < 0 || Answer(button, query, ref __result);
	}

	private static bool Answer(int button, ButtonQuery query, ref bool __result)
	{
		ButtonQueries++;
		if (!TryAnswer(button, query, out var value))
		{
			return true; // not staged: the real method answers
		}

		if (query == ButtonQuery.Down && value)
		{
			ButtonDownAnswers++;
		}

		__result = value;
		return false; // the real method is skipped
	}

	/// <summary>
	/// How many times <c>PlayerCamera.HandleInput</c> was entered, and how many <c>Input.GetKey*</c> call sites the transpiler replaced in it. The first
	/// separates "the method never runs" from "the method runs and the query is never reached"; the second is a fact about the patch, not about a frame.
	/// </summary>
	internal static int HandleInputEntries { get; private set; }

	/// <summary>The number of call sites the transpiler replaced when the patch was installed (0 means the install looked at the wrong method).</summary>
	internal static int HandleInputReplacements => _handleInputReplacements;

	private static void PrefixHandleInput() => HandleInputEntries++;

	/// <summary>
	/// Replaces the game's own <c>Input.GetKey/GetKeyDown/GetKeyUp(KeyCode)</c> calls inside <c>PlayerCamera.HandleInput</c> with the probes below. The
	/// instruction keeps its labels and exception-block markers (only the operand changes), so the game's control flow — <c>KeyBinds.GetBind</c>, the branch,
	/// <c>HandleStartDragging</c>/<c>HandleReleaseDragging</c> — is the game's own; only the answer to the query is staged.
	/// </summary>
	private static IEnumerable<CodeInstruction> TranspileHandleInput(IEnumerable<CodeInstruction> instructions)
	{
		var get = AccessTools.Method(typeof(Input), nameof(Input.GetKey), [typeof(KeyCode)]);
		var down = AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), [typeof(KeyCode)]);
		var up = AccessTools.Method(typeof(Input), nameof(Input.GetKeyUp), [typeof(KeyCode)]);
		var probeGet = AccessTools.Method(typeof(VirtualInput), nameof(ProbeGetKey), [typeof(KeyCode)]);
		var probeDown = AccessTools.Method(typeof(VirtualInput), nameof(ProbeGetKeyDown), [typeof(KeyCode)]);
		var probeUp = AccessTools.Method(typeof(VirtualInput), nameof(ProbeGetKeyUp), [typeof(KeyCode)]);

		var replaced = 0;
		foreach (var instruction in instructions)
		{
			if (instruction.Calls(get))
			{
				instruction.operand = probeGet;
				replaced++;
			}
			else if (instruction.Calls(down))
			{
				instruction.operand = probeDown;
				replaced++;
			}
			else if (instruction.Calls(up))
			{
				instruction.operand = probeUp;
				replaced++;
			}

			yield return instruction;
		}

		_handleInputReplacements = replaced;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool ProbeGetKey(KeyCode key) => Probe(key, ButtonQuery.Held);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool ProbeGetKeyDown(KeyCode key) => Probe(key, ButtonQuery.Down);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool ProbeGetKeyUp(KeyCode key) => Probe(key, ButtonQuery.Up);

	/// <summary>
	/// One query from the replaced call site: counted ALWAYS, answered from the staged state when that key is staged, and handed to the real
	/// <c>UnityEngine.Input</c> otherwise. The count is what makes a zero honest.
	/// </summary>
	private static bool Probe(KeyCode key, ButtonQuery query)
	{
		KeyQueries++;
		if (Keys.TryGetValue(key, out var state))
		{
			var frame = UnityTime.frameCount;
			var value = query switch
			{
				ButtonQuery.Held => state.Held,
				ButtonQuery.Down => state.DownFrame == frame,
				ButtonQuery.Up => state.UpFrame == frame,
				_ => false,
			};

			if (query == ButtonQuery.Down && value)
			{
				KeyDownAnswers++;
			}

			return value;
		}

		return query switch
		{
			ButtonQuery.Held => Input.GetKey(key),
			ButtonQuery.Down => Input.GetKeyDown(key),
			_ => Input.GetKeyUp(key),
		};
	}

	private static string Describe(MethodInfo method) =>
		method.GetMethodBody() == null ? "no IL body — native backend" : "managed body";

	private static string DescribeFailure(Exception exception) =>
		exception.GetType().Name + ": " + exception.Message;
}
