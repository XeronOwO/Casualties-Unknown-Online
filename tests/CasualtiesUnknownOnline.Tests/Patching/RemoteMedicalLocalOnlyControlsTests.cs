using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CasualtiesUnknownOnline.Runtime.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// A remote medical focus is a READ-ONLY view of another player's body, so it must
/// not present the local-only action controls (the nap control, the workout list and
/// the HUD main/off-hand switch) and neither hand-switch entry point may run through
/// it (user ruling 2026-09-21; the control mapping was confirmed 2026-09-25 — the HUD
/// switch, NOT the armor/health view toggle, which reads the displayed body).
///
/// What this suite reaches, and what it does not: the declared surface set is resolved
/// against the game assembly, the remember/restore memory is exercised as a unit, and
/// the reporting decision is read from the COMPILED postfix (its true decision order —
/// text cannot show a statement that moved). The Unity writes themselves
/// (`SetActive` / `Image.enabled`) and <c>RemoteMedicalView.IsOpen</c> need a real
/// scene, so the hide/restore wiring is pinned by reading our own sources against its
/// order requirements; the visible result is the user's dual-client run.
/// </summary>
[Trait("Category", "Integration")]
public class RemoteMedicalLocalOnlyControlsTests
{
	private const string ControlsType = "CasualtiesUnknownOnline.GameAdapter.Character.RemoteMedicalLocalControls";
	private const string SwitchHandsPatchType = "CasualtiesUnknownOnline.GameAdapter.Patches.BodyItemPatches+SwitchHandsPatch";

	[Fact]
	public void DeclaredSurfaces_AreTheRulingControls_AndExistOnTheNativeTypes()
	{
		string[] expected = ["napbutton", "sleepImage", "workoutList", "handSwapImage"];
		Assert.Equal(expected, DeclaredSurfaces());

		foreach (var field in DeclaredSurfaces())
		{
			Assert.True(
				HasNativeField("WoundView", field) || HasNativeField("PlayerCamera", field),
				$"the hidden surface '{field}' is no longer a public field of WoundView or PlayerCamera");
		}
	}

	[Fact]
	public void SurfaceMemory_RemembersTheOriginalOnce_AndRestoresItExactly()
	{
		var memory = NewSurfaceMemory(out var hide, out var restore);

		// The first hide pass remembers what the surface looked like; every later
		// pass re-asserts the hidden state without overwriting that memory.
		Assert.False((bool)hide.Invoke(memory, [true])!);
		Assert.False((bool)hide.Invoke(memory, [false])!);

		// The original value comes back exactly once.
		Assert.True((bool)restore.Invoke(memory, null)!);
		Assert.Null(restore.Invoke(memory, null));
	}

	[Fact]
	public void SurfaceMemory_KeepsAnAlreadyHiddenSurfaceHidden()
	{
		var memory = NewSurfaceMemory(out var hide, out var restore);

		Assert.False((bool)hide.Invoke(memory, [false])!);
		Assert.False((bool)restore.Invoke(memory, null)!);
	}

	[Fact]
	public void BothHandSwitchPaths_AreClaimedInThePatchInventory()
	{
		var contracts = BuildContracts();

		// The HUD control's own method and the keyboard path's method: blocking one
		// of them alone leaves the other entry point reachable.
		Assert.Contains(contracts, contract => contract.TargetType == "PlayerCamera" && contract.MethodName == "SwitchHands");
		Assert.Contains(contracts, contract => contract.TargetType == "Body" && contract.MethodName == "SwitchHands");
	}

	[Fact]
	public void ThePostfix_ReturnsOnTheBlockedVerdict_BeforeItCanReport()
	{
		// Repository convention 6: a prefix that swallows a write must not let its postfix
		// report it. A source pin cannot see the report statements moving ABOVE the verdict
		// check — the defect would be back with every text assertion still green — so the
		// decision order is read from the COMPILED method: the postfix must reach its early
		// `ret` (the null-verdict return) before its first call into the reporting contract,
		// and it must still report when the verdict says the swap ran.
		var patch = GameAssemblyHost.Adapter.GetType(SwitchHandsPatchType, throwOnError: true)!;
		var postfix = patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!;
		var contract = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.IPatchBridge", throwOnError: true)!;

		var instructions = Disassemble(postfix.GetMethodBody()!.GetILAsByteArray()!);
		var reports = instructions
			.Where(instruction => instruction.Token != 0
				&& instruction.Opcode.FlowControl == FlowControl.Call
				&& postfix.Module.ResolveMethod(instruction.Token)?.DeclaringType == contract)
			.ToList();
		Assert.True(
			reports.Count >= 3,
			"the postfix no longer reports the inventory change and both slot moves"
				+ Environment.NewLine + Trace(postfix.Module, instructions));

		// The blocked verdict must be able to LEAVE before the first report: either an
		// inline return, or a branch that jumps past every report — the Debug build shares
		// one epilogue, so the guard compiles to a branch rather than a `ret`.
		var firstReport = reports[0].Offset;
		var lastReport = reports[reports.Count - 1].Offset;
		var blockedPathLeaves = instructions
			.TakeWhile(instruction => instruction.Offset < firstReport)
			.Any(instruction => instruction.Opcode == OpCodes.Ret || instruction.Target > lastReport);
		Assert.True(
			blockedPathLeaves,
			"no blocked-verdict path leaves the postfix before it reports: a blocked swap would report an inventory change and two slot moves"
				+ Environment.NewLine + Trace(postfix.Module, instructions));

		// The reported facts themselves are unchanged production code; a source pin keeps
		// them from drifting while the compiled-order pin above guards the decision.
		var swap = Between(
			AdapterSource("Patches", "BodyItemPatches.cs"),
			"[HarmonyPatch(typeof(Body), \"SwitchHands\")]",
			"[HarmonyPatch(typeof(Body), \"PickUpItem\")]");
		Assert.Contains("PatchBridge.Impl?.OnInventoryChanged();", swap);
		Assert.Contains("OnSlotMoved(__instance, 0, \"Hands\")", swap);
		Assert.Contains("OnSlotMoved(__instance, 1, \"Hands\")", swap);
	}

	[Fact]
	public void TheRemoteFocusHidesTheSurfaces_AndTheClosePathCannotSkipTheRestore()
	{
		var postfix = Between(
			AdapterSource("Patches", "RemoteMedicalPatches.cs"),
			"[HarmonyPatch(typeof(WoundView), \"Update\")]",
			"[HarmonyPatch(typeof(ECGVisualizer), \"get_body\")]");
		Assert.Contains("RemoteMedicalLocalControls.Hide(__instance);", postfix);
		Assert.Contains("RemoteMedicalLocalControls.Restore();", postfix);

		// Order, not just presence: the hide sits behind the open-focus guard.
		Assert.True(
			postfix.IndexOf("if (!RemoteMedicalView.IsOpen)", StringComparison.Ordinal)
				< postfix.IndexOf("RemoteMedicalLocalControls.Hide(__instance);", StringComparison.Ordinal),
			"the hide must sit behind the open-focus guard");

		// Hiding, not merely disabling: the ruling rejects the disabled-but-visible
		// control the previous cycle shipped.
		Assert.DoesNotContain("napbutton.interactable = false", postfix);

		// The close path restores BEFORE its early return: a display body destroyed under
		// the focus leaves IsOpen false, and that path must not skip the restore.
		var close = AdapterSource("Character", "RemoteMedicalView.cs");
		var restore = close.IndexOf("RemoteMedicalLocalControls.Restore();", StringComparison.Ordinal);
		var earlyReturn = close.IndexOf("if (!wasOpen)", StringComparison.Ordinal);
		Assert.True(restore >= 0, "RemoteMedicalView.Close must restore the local-only surfaces");
		Assert.True(earlyReturn >= 0, "RemoteMedicalView.Close's early return moved — re-check the restore order");
		Assert.True(restore < earlyReturn, "the restore must run before the early return can skip it");
	}

	[Fact]
	public void TheHidingApplier_WritesEveryDeclaredSurface()
	{
		var applier = AdapterSource("Character", "RemoteMedicalLocalControls.cs");
		foreach (var field in DeclaredSurfaces())
		{
			Assert.Contains("." + field, applier);
		}
	}

	/// <summary>The compiled instructions with the metadata token each one carries — the
	/// only place a method's real statement order is visible.</summary>
	private static List<Instruction> Disassemble(byte[] il)
	{
		var opcodes = typeof(OpCodes)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Select(field => (OpCode)field.GetValue(null)!)
			.ToDictionary(opcode => unchecked((ushort)opcode.Value));
		var instructions = new List<Instruction>();
		var offset = 0;
		while (offset < il.Length)
		{
			var start = offset;
			var value = (ushort)il[offset++];
			if (value == 0xFE)
			{
				value = (ushort)(0xFE00 | il[offset++]);
			}

			var opcode = opcodes[value];
			var token = 0;
			int? target = null;
			if (CarriesMetadataToken(opcode.OperandType))
			{
				token = BitConverter.ToInt32(il, offset);
			}
			else if (opcode.OperandType == OperandType.ShortInlineBrTarget)
			{
				target = offset + 1 + (sbyte)il[offset];
			}
			else if (opcode.OperandType == OperandType.InlineBrTarget)
			{
				target = offset + 4 + BitConverter.ToInt32(il, offset);
			}

			offset += OperandSize(opcode.OperandType);
			instructions.Add(new Instruction(start, opcode, token, target));
		}

		return instructions;
	}

	/// <summary>One decoded IL instruction: the metadata token it carries (0 when none)
	/// and the offset it branches to (null when it is not a branch).</summary>
	private sealed record Instruction(int Offset, OpCode Opcode, int Token, int? Target);

	private static bool CarriesMetadataToken(OperandType operandType) =>
		operandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok;

	private static int OperandSize(OperandType operandType) => operandType switch
	{
		OperandType.InlineNone => 0,
		OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
		OperandType.InlineVar => 2,
		OperandType.InlineBrTarget or OperandType.InlineI or OperandType.InlineMethod or OperandType.InlineField
			or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType
			or OperandType.ShortInlineR => 4,
		OperandType.InlineI8 or OperandType.InlineR => 8,
		_ => throw new InvalidOperationException($"unhandled IL operand type: {operandType}"),
	};

	/// <summary>The disassembly a failed order assertion prints: the decision order IS the
	/// assertion, so the reader sees the instructions that decided it.</summary>
	private static string Trace(Module module, List<Instruction> instructions) =>
		string.Join(
			Environment.NewLine,
			instructions.Select(instruction =>
				$"{instruction.Offset:X4} {instruction.Opcode.Name} {Member(module, instruction.Token)}"
					+ (instruction.Target is { } target ? $" -> {target:X4}" : string.Empty)));

	private static string Member(Module module, int token)
	{
		if (token == 0)
		{
			return string.Empty;
		}

		try
		{
			var resolved = module.ResolveMember(token);
			return $"{resolved.DeclaringType?.Name}::{resolved.Name}";
		}
		catch (ArgumentException)
		{
			return $"token 0x{token:X8}";
		}
	}

	private static string[] DeclaredSurfaces()
	{
		var type = GameAssemblyHost.Adapter.GetType(ControlsType, throwOnError: true)!;
		var field = type.GetField("NativeSurfaceFields", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		Assert.NotNull(field);
		return (string[])field!.GetValue(null)!;
	}

	private static bool HasNativeField(string typeName, string field) =>
		GameAssemblyHost.ResolveType(typeName)?.GetField(field, BindingFlags.Public | BindingFlags.Instance) != null;

	private static object NewSurfaceMemory(out MethodInfo hide, out MethodInfo restore)
	{
		var type = GameAssemblyHost.Adapter.GetType(ControlsType + "+SurfaceMemory", throwOnError: true)!;
		hide = type.GetMethod("Hide", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
		restore = type.GetMethod("Restore", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
		Assert.NotNull(hide);
		Assert.NotNull(restore);
		return Activator.CreateInstance(type)!;
	}

	private static List<PatchContract> BuildContracts()
	{
		var inventory = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory",
			throwOnError: true)!;
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
		return (List<PatchContract>)build.Invoke(null, null)!;
	}

	private static string AdapterSource(string folder, string fileName) =>
		File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"src",
			"CasualtiesUnknownOnline.GameAdapter",
			folder,
			fileName));

	private static string Between(string text, string startAnchor, string endAnchor)
	{
		var start = text.IndexOf(startAnchor, StringComparison.Ordinal);
		Assert.True(start >= 0, $"anchor not found in the adapter source: {startAnchor}");
		var end = text.IndexOf(endAnchor, start + startAnchor.Length, StringComparison.Ordinal);
		return end < 0 ? text.Substring(start) : text.Substring(start, end - start);
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		if (directory is null)
		{
			throw new InvalidOperationException("could not locate repository root (CasualtiesUnknownOnline.slnx)");
		}

		return directory.FullName;
	}
}
