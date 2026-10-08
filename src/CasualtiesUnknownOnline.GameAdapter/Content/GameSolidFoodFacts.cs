using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The Game Adapter's answer to <see cref="ISolidFoodSemantics"/>: the shape of
/// an item's own use action, read from the game's own content. The registry
/// (<c>Item.GlobalItems</c>) gives the <c>ItemInfo</c> — vanilla AND
/// mod-registered, because the item content provider writes into the same
/// dictionary — and its <c>useAction</c> delegate is the code
/// <c>Body.UseItem</c> runs (<c>Body.cs:2475-2481</c>).
/// <para>
/// WHY THE COMPILED BODY AND NOT A FIELD. The game carries no field for this
/// question. <c>ItemInfo</c> has no nutrition member, and its <c>category</c>
/// string is a display classifier — the unidentified-item label reads it
/// (<c>PlayerCamera.ItemHoverDescription</c>) — which files 18 vanilla edibles
/// under <c>"custom"</c> (<c>geofruit</c>, <c>browncap</c>, <c>popfruit</c>,
/// <c>mushpear</c>, …) while calling <c>ketchup</c> food (it is a drink). The
/// shape flags do not separate it either: measured over the 139 vanilla items
/// with a use action, "usable and not left-click-triggered and not a liquid
/// container and not wearable" still admits 24 items that act on the USER's own
/// world — a watch that talks, a geiger counter that clicks, dynamite that arms,
/// a drain that empties fluid where the eater stands, a present that spawns
/// items — so it cannot be the family's gate. The delegate's own instructions
/// answer exactly the question, at no cost and with no side effect: 41 of those
/// 139 call <c>Body.Eat</c>/<c>Body.Drink</c> directly and one more
/// (<c>nondescriptcan</c>, whose action calls <c>NonDescriptCan.Eat</c>) does so
/// one call away.
/// </para>
/// <para>
/// THE TRAVERSAL is the use action's own instructions plus the instructions of
/// the game's own methods it calls directly — one level, because the game's one
/// component-driven eater sits exactly one call away. A mod whose feeding goes
/// through a helper of its own two levels down is not recognised: that is the
/// mechanism's named limit, and the item keeps the behaviour it has today (the
/// gesture is a drop, not a feed) rather than becoming a wrong one.
/// </para>
/// <para>
/// RUNNING the delegate is not an option, which is why this reads it instead:
/// the sibling drink chain measures its dose by running the item's action with
/// the ONE call it makes diverted, but a food action writes the body directly,
/// changes the item's condition, swaps sprites, plays sounds and — for the two
/// container-swap foods — instantiates and picks up a replacement object. So the
/// verdict is read once per id and cached; a delegate the reader cannot open
/// (a dynamic method) answers <see cref="SolidFoodVerdict.NotSolidFood"/>, which
/// refuses the cross-player gesture instead of guessing at it.
/// </para>
/// </summary>
internal sealed class GameSolidFoodFacts(ILogger<GameSolidFoodFacts> log) : ISolidFoodSemantics
{
	private static readonly string[] BodyFeedingMethods = ["Eat", "Drink"];
	private static readonly string[] HandOverMethods = ["PickUpItem", "AutoPickUpItem"];

	/// <summary>
	/// One verdict per id, read once: the use action's compiled body never changes
	/// while the assembly is loaded, and the classification is asked on every gesture
	/// and every request. Ordinal by nature — item ids are exact game-data keys.
	/// </summary>
	private readonly Dictionary<string, SolidFoodVerdict> _verdicts = [];

	public SolidFoodVerdict Classify(string itemId)
	{
		if (string.IsNullOrEmpty(itemId))
		{
			return SolidFoodVerdict.NotSolidFood;
		}

		if (_verdicts.TryGetValue(itemId, out var cached))
		{
			return cached;
		}

		var verdict = Read(itemId);
		_verdicts[itemId] = verdict;
		return verdict;
	}

	private SolidFoodVerdict Read(string itemId)
	{
		// The registry is populated by the game's own item setup; a query before it has
		// run (or on a build that never loads items) answers "not a solid food" instead
		// of throwing inside a gesture path.
		if (Item.GlobalItems is not { } items
			|| !items.TryGetValue(itemId, out var info)
			|| !info.usable
			|| info.useAction is null)
		{
			return SolidFoodVerdict.NotSolidFood;
		}

		var instructions = ReadUseAction(info.useAction);
		if (instructions is null)
		{
			// Fail closed AND loud: an unreadable delegate means this side cannot say
			// what the use action does, and a wrong answer would run a stranger's code
			// against another player's body.
			log.LogWarning("[SolidFood] {ItemId} has a use action whose compiled body could not be read — the cross-player eat refuses it.", itemId);
			return SolidFoodVerdict.NotSolidFood;
		}

		if (!Calls(instructions, typeof(Body), BodyFeedingMethods))
		{
			return SolidFoodVerdict.NotSolidFood;
		}

		if (Calls(instructions, typeof(Body), HandOverMethods)
			|| Calls(instructions, typeof(Object), "Instantiate")
			|| Calls(instructions, typeof(Utils), "Create"))
		{
			return SolidFoodVerdict.EatsAndReplaces;
		}

		return Calls(instructions, typeof(Object), "Destroy") ? SolidFoodVerdict.EatsAndDestroys : SolidFoodVerdict.Eats;
	}

	/// <summary>
	/// The instruction set the shape questions are asked over: the use action's own
	/// body, plus the body of every game method it CALLS directly (the call/callvirt
	/// operands only — a method merely loaded as a delegate or type token is not part
	/// of what the action does). Null when the use action's own body cannot be read —
	/// the answer is then "not a solid food" rather than a guess.
	/// </summary>
	private static List<CodeInstruction>? ReadUseAction(Delegate useAction)
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

			// One level, and the game's own assembly only: a method a mod ships is not
			// the game's content, and following further would turn this into a
			// decompiler rather than a classification.
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

	/// <summary>Harmony's own reader, the same one every transpiler in this adapter is handed instructions by.</summary>
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

	private static bool Calls(List<CodeInstruction> instructions, Type declaringType, params string[] methodNames)
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
}
