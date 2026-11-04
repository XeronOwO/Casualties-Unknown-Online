using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Acceptance;

/// <summary>
/// The game's key-bind capture, answered from staged keys.
///
/// <para>
/// `SettingsMenu.WaitForKeyPress` is a coroutine that enumerates every <c>KeyCode</c> and asks <c>Input.GetKeyDown</c> — a path
/// none of the other replaced call sites covers, so without this the acceptance row that needs a key bound can only be judged
/// with OS-level input. Two methods need the replacement, not one: the outer method only BUILDS the iterator, and the loop that
/// asks lives in the compiler-generated state machine's <c>MoveNext</c> (measured 2026-11-02 — patching the builder alone left
/// <c>captureWaiting</c> true under a staged Return and a staged Space while the builder reported its replacements).
/// </para>
///
/// <para>
/// The coroutine still decides what the key MEANS: it reads the answer, binds that key and applies it. Only the answer to its
/// query is staged.
/// </para>
/// </summary>
internal static class KeybindCapturePatch
{
	/// <summary>Installs the replacement and answers the one report line the install's log carries.</summary>
	internal static string Install(Harmony harmony)
	{
		var waitForKeyPress = AccessTools.Method(typeof(SettingsMenu), "WaitForKeyPress") ?? FirstDeclared(typeof(SettingsMenu), "WaitForKeyPress");
		if (waitForKeyPress == null)
		{
			return "SettingsMenu.WaitForKeyPress: no method on this build";
		}

		try
		{
			var builderReplacements = Patch(harmony, waitForKeyPress);
			var stateMachineReplacements = 0;
			foreach (var nested in typeof(SettingsMenu).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (nested.Name.IndexOf("WaitForKeyPress", StringComparison.Ordinal) < 0)
				{
					continue;
				}

				var moveNext = AccessTools.Method(nested, "MoveNext");
				if (moveNext == null)
				{
					continue;
				}

				stateMachineReplacements += Patch(harmony, moveNext);
			}

			return "SettingsMenu.WaitForKeyPress: patched, " + builderReplacements.ToString(CultureInfo.InvariantCulture)
				+ " call(s) replaced in the builder, " + stateMachineReplacements.ToString(CultureInfo.InvariantCulture)
				+ " in the state machine";
		}
		catch (Exception exception)
		{
			return "SettingsMenu.WaitForKeyPress: FAILED " + exception.GetType().Name + ": " + exception.Message;
		}
	}

	private static int Patch(Harmony harmony, MethodBase target)
	{
		InputQueryPatches.ResetReplacementCount();
		harmony.Patch(target, transpiler: new HarmonyMethod(typeof(InputQueryPatches), nameof(InputQueryPatches.TranspileKeyReads)));
		return InputQueryPatches.TakeReplacementCount();
	}

	/// <summary>The method the game declares under this name, when Harmony's lookup does not find it (a private or renamed member).</summary>
	private static MethodInfo? FirstDeclared(Type type, string name)
	{
		foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
		{
			if (method.Name.IndexOf(name, StringComparison.Ordinal) >= 0)
			{
				return method;
			}
		}

		return null;
	}
}
