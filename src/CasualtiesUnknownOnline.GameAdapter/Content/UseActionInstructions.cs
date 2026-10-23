using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The one reader of an item's own <c>useAction</c> COMPILED BODY, shared by the content classifiers
/// (<see cref="GameSolidFoodFacts"/>, <see cref="GameDrinkActionFacts"/>). It exists as its own type because
/// two classifiers asking the same question must read it the same way: the instructions of the delegate plus,
/// one level down, the instructions of every GAME method it calls directly (a method merely loaded as a
/// delegate or type token is not part of what the action does). A mod's method one level down is not followed:
/// that would be a decompiler, not a classification.
///
/// <para>
/// Reading is not running: a classifier can ask what a use action WOULD do without applying it to a body. A
/// delegate whose body cannot be read (a dynamic method) answers null, and each caller decides its own
/// fail-closed verdict rather than guessing.
/// </para>
/// </summary>
internal static class UseActionInstructions
{
	/// <summary>
	/// The instruction set the shape questions are asked over, or null when the use action's own body cannot be
	/// read. See the type summary for the one-level rule.
	/// </summary>
	internal static List<CodeInstruction>? Read(Delegate useAction)
	{
		var own = OriginalInstructions(useAction.Method);
		if (own is null)
		{
			return null;
		}

		var instructions = new List<CodeInstruction>(own);
		foreach (var instruction in own)
		{
			if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
			{
				continue;
			}

			if (instruction.operand is not MethodBase called || called.DeclaringType is null)
			{
				continue;
			}

			// One level, and the game's own assembly only: a method a mod ships is not the game's content, and
			// following further would turn this into a decompiler rather than a classification.
			if (called.DeclaringType.Assembly != typeof(Item).Assembly)
			{
				continue;
			}

			var callee = OriginalInstructions(called);
			if (callee is not null)
			{
				instructions.AddRange(callee);
			}
		}

		return instructions;
	}

	/// <summary>Whether the instruction set calls one of the named methods on exactly <paramref name="declaringType"/>.</summary>
	internal static bool Calls(List<CodeInstruction> instructions, Type declaringType, params string[] methodNames)
	{
		foreach (var instruction in instructions)
		{
			if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
			{
				continue;
			}

			if (instruction.operand is not MethodInfo called || called.DeclaringType != declaringType)
			{
				continue;
			}

			foreach (var name in methodNames)
			{
				if (called.Name == name)
				{
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>Harmony's own reader — the same instruction source every transpiler in this adapter is handed its input by.</summary>
	private static List<CodeInstruction>? OriginalInstructions(MethodBase method)
	{
		try
		{
			return PatchProcessor.GetOriginalInstructions(method, null);
		}
		catch (Exception)
		{
			// A dynamic or otherwise body-less method: reported by the caller's line.
			return null;
		}
	}
}
