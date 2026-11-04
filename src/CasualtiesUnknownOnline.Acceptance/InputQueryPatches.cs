using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Acceptance;

/// <summary>
/// The acceptance channel's Harmony entry points on the game's own input QUERIES: the postfix on
/// <c>UnityEngine.Input.mousePosition</c>, the prefixes on the three mouse-button methods, and the replacement of the game's
/// <c>Input.GetKey*</c> call sites.
///
/// <para>
/// It is the ANSWERING half of <see cref="VirtualInput"/>: that type owns the staged state a batch writes and the lifecycle that
/// installs and removes these patches, while this type holds the methods Harmony calls, the call-site replacement itself, and the
/// counters that say whether the game ever asked. Nothing here stages anything; the only state it keeps is the answering census and
/// the install census that belongs to it.
/// </para>
///
/// <para>
/// WHY THE CALL SITES AND NOT <c>UnityEngine.Input</c>'s OWN METHODS: measured on this build, a detour on <c>Input.GetKey*</c>
/// installs successfully and is never invoked (0 queries in 300+ frames while the mouse internals answered 28), so the read is
/// replaced where the game makes it. The same transpiler serves four targets — <c>PlayerCamera.HandleInput</c>,
/// <c>MinigameBase.UpdateHandVisuals</c>, <c>PlayerCamera.HandleDragging</c> (through <see cref="DragInputPatch"/>) and
/// <c>SettingsMenu.WaitForKeyPress</c> (through <see cref="KeybindCapturePatch"/>).
/// </para>
/// </summary>
internal static class InputQueryPatches
{
	private static readonly (string Name, string PrefixName)[] ButtonEntries =
	[
		(nameof(Input.GetMouseButton), nameof(PrefixHeld)),
		(nameof(Input.GetMouseButtonDown), nameof(PrefixDown)),
		(nameof(Input.GetMouseButtonUp), nameof(PrefixUp)),
	];

	private static int _handleInputReplacements;
	private static int _buttonCensus;

	/// <summary>How many button methods the backend accepted (0 — the pointer still works — up to 3).</summary>
	internal static int ButtonPatches => _buttonCensus;

	/// <summary>The number of call sites the last replacement rewrote (0 means the install looked at the wrong method).</summary>
	internal static int HandleInputReplacements => _handleInputReplacements;

	/// <summary>
	/// The call-site count the next transpile will replace: an install reads it straight after its own patch, so a zero is honest.
	/// </summary>
	internal static void ResetReplacementCount() => _handleInputReplacements = 0;

	internal static int TakeReplacementCount() => _handleInputReplacements;

	/// <summary>
	/// Drops the install census, so a report taken after <see cref="VirtualInput.Uninstall"/> reads the same zero it read before the
	/// first install. The answering counters are NOT reset: they measure what the game asked over its life, and a batch compares them
	/// across an install.
	/// </summary>
	internal static void ResetInstallCensus() => _buttonCensus = 0;

	/// <summary>
	/// Patches the game's own input queries and answers the report line each target contributed: the pointer's outcome, each button
	/// method's own, and the two call-site replacements. The report IS the result — a target the backend refuses must be named with its
	/// exception, so the next step is decided on evidence rather than guessed from a missing body. <paramref name="failures"/> counts
	/// the targets a batch cannot use at all, which is what <see cref="VirtualInput.Install"/> turns into its own verdict.
	/// </summary>
	internal static string Install(Harmony harmony, out int failures)
	{
		var report = new List<string>();
		failures = 0;

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
				harmony.Patch(getter, postfix: new HarmonyMethod(typeof(InputQueryPatches), nameof(PostfixPointer)));
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
				harmony.Patch(method, prefix: new HarmonyMethod(typeof(InputQueryPatches), entry.PrefixName));
				_buttonCensus++;
				report.Add(entry.Name + ": patched (" + Describe(method) + ")");
			}
			catch (Exception exception)
			{
				report.Add(entry.Name + ": FAILED " + DescribeFailure(exception));
			}
		}

		// The game's own key path: the CALL SITES inside PlayerCamera.HandleInput, not UnityEngine.Input itself (see the type's header).
		// The game's own branch — GetBind → the query → HandleStartDragging/HandleReleaseDragging — still decides what happens; only the
		// ANSWER to the query is staged.
		var handleInput = AccessTools.Method(typeof(PlayerCamera), "HandleInput", Type.EmptyTypes);
		if (handleInput == null)
		{
			report.Add("PlayerCamera.HandleInput: no method on this build");
		}
		else
		{
			try
			{
				ResetReplacementCount();
				harmony.Patch(
					handleInput,
					prefix: new HarmonyMethod(typeof(InputQueryPatches), nameof(PrefixHandleInput)),
					transpiler: new HarmonyMethod(typeof(InputQueryPatches), nameof(TranspileKeyReads)));
				report.Add("PlayerCamera.HandleInput: patched, " + _handleInputReplacements.ToString(CultureInfo.InvariantCulture) + " call(s) replaced, target "
					+ handleInput.DeclaringType?.FullName + "#" + handleInput.MetadataToken.ToString(CultureInfo.InvariantCulture)
					+ ", applied " + (Harmony.GetPatchInfo(handleInput) is null ? "no" : "yes"));
			}
			catch (Exception exception)
			{
				report.Add("PlayerCamera.HandleInput: FAILED " + DescribeFailure(exception));
			}
		}

		// The minigame's own hand read. `MinigameBase.UpdateHandVisuals` asks `Input.GetKey(attack)` DIRECTLY — outside
		// `PlayerCamera.HandleInput`, so the replacement above never reaches it — and a detour on Input's own methods is never invoked on
		// this build (see the type's header). The same call-site replacement therefore has to be applied to it as well: without this a
		// staged press can hold a world gesture and never the hand of a minigame (measured 2026-11-01: `minigame-live` read
		// handClicking=False with a press staged at the ring).
		var updateHandVisuals = AccessTools.Method(typeof(MinigameBase), "UpdateHandVisuals", [typeof(bool)])
			?? AccessTools.Method(typeof(MinigameBase), "UpdateHandVisuals")
			?? FirstDeclaredMethod(typeof(MinigameBase), "UpdateHandVisuals");
		if (updateHandVisuals == null)
		{
			report.Add("MinigameBase.UpdateHandVisuals: no method on this build (" + DescribeDeclaredMethods(typeof(MinigameBase)) + ")");
		}
		else
		{
			try
			{
				ResetReplacementCount();
				harmony.Patch(updateHandVisuals, transpiler: new HarmonyMethod(typeof(InputQueryPatches), nameof(TranspileKeyReads)));
				report.Add("MinigameBase.UpdateHandVisuals: patched, " + _handleInputReplacements.ToString(CultureInfo.InvariantCulture) + " call(s) replaced");
			}
			catch (Exception exception)
			{
				report.Add("MinigameBase.UpdateHandVisuals: FAILED " + DescribeFailure(exception));
			}
		}

		return string.Join(" | ", report);
	}

	/// <summary>
	/// How many KEY questions the GAME asked for a staged key, and how many of those were answered "down". A batch reads these to
	/// separate "my press never reached the game" from "the game asked and the branch did nothing" — the two causes a single frame's
	/// state cannot tell apart (2026-10-28: `Input.GetKey(Tab)` answered true while `PlayerCamera.radialOpen` stayed false, and only the
	/// census could say which half failed).
	/// </summary>
	internal static int KeyQueries { get; private set; }

	/// <summary>How many of those KEY questions the staged state answered with "down".</summary>
	internal static int KeyDownAnswers { get; private set; }

	/// <summary>
	/// How many MOUSE-BUTTON questions the game asked, and how many were answered "down": the same census for the mouse internals,
	/// because the two families share one backend and a key patch that never runs next to a button patch that does is a fact about the
	/// TARGETS, not about the input (read 2026-10-28, when the key detour answered nothing while the mouse one was believed live).
	/// </summary>
	internal static int ButtonQueries { get; private set; }

	/// <summary>How many of those button questions the staged state answered with "down".</summary>
	internal static int ButtonDownAnswers { get; private set; }

	/// <summary>How many KEY questions arrived through the PUBLIC <c>GetKey(KeyCode)</c> wrappers (the counters above belong to the <c>*Int</c> internals).</summary>
	internal static int WrapperKeyQueries { get; private set; }

	/// <summary>
	/// How many times <c>PlayerCamera.HandleInput</c> was entered. It separates "the method never runs" from "the method runs and the query
	/// is never reached".
	/// </summary>
	internal static int HandleInputEntries { get; private set; }

	/// <summary>The postfix Harmony calls for the pointer: replaces the getter's answer while a batch has staged one.</summary>
	private static void PostfixPointer(ref Vector3 __result)
	{
		if (VirtualInput.TryGetStaged(out var staged))
		{
			__result = staged;
		}
	}

	private static bool PrefixHeld(int button, ref bool __result) => Answer(button, VirtualInput.ButtonQuery.Held, ref __result);

	private static bool PrefixDown(int button, ref bool __result) => Answer(button, VirtualInput.ButtonQuery.Down, ref __result);

	private static bool PrefixUp(int button, ref bool __result) => Answer(button, VirtualInput.ButtonQuery.Up, ref __result);

	private static bool PrefixKeyHeld(KeyCode key, ref bool __result) => AnswerKey(key, VirtualInput.ButtonQuery.Held, ref __result);

	private static bool PrefixKeyDown(KeyCode key, ref bool __result) => AnswerKey(key, VirtualInput.ButtonQuery.Down, ref __result);

	private static bool PrefixKeyUp(KeyCode key, ref bool __result) => AnswerKey(key, VirtualInput.ButtonQuery.Up, ref __result);

	/// <summary>The same three questions, called from the REPLACED call sites inside the game's own methods; every query is counted.</summary>
	private static bool PrefixWrapperHeld(KeyCode key, ref bool __result) => AnswerWrapperKey(key, VirtualInput.ButtonQuery.Held, ref __result);

	private static bool PrefixWrapperDown(KeyCode key, ref bool __result) => AnswerWrapperKey(key, VirtualInput.ButtonQuery.Down, ref __result);

	private static bool PrefixWrapperUp(KeyCode key, ref bool __result) => AnswerWrapperKey(key, VirtualInput.ButtonQuery.Up, ref __result);

	private static bool AnswerWrapperKey(KeyCode key, VirtualInput.ButtonQuery query, ref bool __result)
	{
		WrapperKeyQueries++;
		return AnswerKey(key, query, ref __result);
	}

	/// <summary>
	/// Answers a KEY question for a staged KEY first, then for a staged MOUSE button (Mouse0 = 323, the value the game's `attack` bind
	/// answers): any other key is the real keyboard's, so a batch can never type for the game by accident.
	/// </summary>
	private static bool AnswerKey(KeyCode key, VirtualInput.ButtonQuery query, ref bool __result)
	{
		if (VirtualInput.TryAnswerKey(key, query, out var value))
		{
			KeyQueries++;
			if (query == VirtualInput.ButtonQuery.Down && value)
			{
				KeyDownAnswers++;
			}

			__result = value;
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

	private static bool Answer(int button, VirtualInput.ButtonQuery query, ref bool __result)
	{
		ButtonQueries++;
		if (!VirtualInput.TryAnswer(button, query, out var value))
		{
			return true; // not staged: the real method answers
		}

		if (query == VirtualInput.ButtonQuery.Down && value)
		{
			ButtonDownAnswers++;
		}

		__result = value;
		return false; // the real method is skipped
	}

	private static void PrefixHandleInput() => HandleInputEntries++;

	/// <summary>The first method declared on <paramref name="type"/> with this name, whatever its signature (the fallback when the exact one is not found).</summary>
	private static MethodInfo? FirstDeclaredMethod(Type type, string name)
	{
		foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
		{
			if (method.Name == name)
			{
				return method;
			}
		}

		return null;
	}

	/// <summary>The names the type declares, for a report that says what the install DID see instead of only what it missed.</summary>
	private static string DescribeDeclaredMethods(Type type)
	{
		var names = new List<string>();
		foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
		{
			names.Add(method.Name);
			if (names.Count >= 24)
			{
				names.Add("…");
				break;
			}
		}

		return string.Join(", ", names);
	}

	/// <summary>
	/// Replaces the game's own <c>Input.GetKey/GetKeyDown/GetKeyUp(KeyCode)</c> calls inside the method being patched with the probes
	/// below. The instruction keeps its labels and exception-block markers (only the operand changes), so the game's control flow —
	/// <c>KeyBinds.GetBind</c>, the branch, <c>HandleStartDragging</c>/<c>HandleReleaseDragging</c> — is the game's own; only the answer
	/// to the query is staged.
	/// </summary>
	internal static IEnumerable<CodeInstruction> TranspileKeyReads(IEnumerable<CodeInstruction> instructions)
	{
		var get = AccessTools.Method(typeof(Input), nameof(Input.GetKey), [typeof(KeyCode)]);
		var down = AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), [typeof(KeyCode)]);
		var up = AccessTools.Method(typeof(Input), nameof(Input.GetKeyUp), [typeof(KeyCode)]);
		var probeGet = AccessTools.Method(typeof(InputQueryPatches), nameof(ProbeGetKey), [typeof(KeyCode)]);
		var probeDown = AccessTools.Method(typeof(InputQueryPatches), nameof(ProbeGetKeyDown), [typeof(KeyCode)]);
		var probeUp = AccessTools.Method(typeof(InputQueryPatches), nameof(ProbeGetKeyUp), [typeof(KeyCode)]);

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
	private static bool ProbeGetKey(KeyCode key) => Probe(key, VirtualInput.ButtonQuery.Held);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool ProbeGetKeyDown(KeyCode key) => Probe(key, VirtualInput.ButtonQuery.Down);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool ProbeGetKeyUp(KeyCode key) => Probe(key, VirtualInput.ButtonQuery.Up);

	/// <summary>
	/// One query from the replaced call site: counted ALWAYS, answered from the staged state when that key is staged, and handed to the
	/// real <c>UnityEngine.Input</c> otherwise. The count is what makes a zero honest.
	/// </summary>
	private static bool Probe(KeyCode key, VirtualInput.ButtonQuery query)
	{
		KeyQueries++;
		if (VirtualInput.TryAnswerKey(key, query, out var value))
		{
			if (query == VirtualInput.ButtonQuery.Down && value)
			{
				KeyDownAnswers++;
			}

			return value;
		}

		return query switch
		{
			VirtualInput.ButtonQuery.Held => Input.GetKey(key),
			VirtualInput.ButtonQuery.Down => Input.GetKeyDown(key),
			_ => Input.GetKeyUp(key),
		};
	}

	private static string Describe(MethodInfo method) =>
		method.GetMethodBody() == null ? "no IL body — native backend" : "managed body";

	private static string DescribeFailure(Exception exception) =>
		exception.GetType().Name + ": " + exception.Message;
}
