using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.ContractTool.Snapshot;
using CasualtiesUnknownOnline.Runtime.Patching;
using CasualtiesUnknownOnline.Tests.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.ContractTool;

/// <summary>
/// The one-source-of-facts gate. The update-day snapshot reads the patch-target
/// contract rows out of the adapter's METADATA (so a snapshot needs neither the
/// game to run nor the adapter to be loaded), while the runtime guard and the
/// contract tests take the same facts from <c>PatchInventory.BuildContracts</c>.
/// Nothing links the two implementations, so this test does: every row the tool
/// recovers must equal the adapter's own row, and the ONLY rows the lens cannot
/// see must be the hand-declared dynamic contracts (which carry no attribute and
/// are covered by the contract tests). A silent divergence fails the build.
/// </summary>
[Trait("Category", "Integration")]
public class PatchContractRowParityTests
{
	private static readonly string AdapterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CasualtiesUnknownOnline.GameAdapter.dll");

	[Fact]
	public void ToolRows_EqualTheAdaptersOwnContractRows()
	{
		var toolRows = PatchContractReader.Read(AdapterPath);
		var adapterRows = BuildContracts();

		Assert.True(toolRows.Count >= 100, $"the tool recovered only {toolRows.Count} contract rows — the metadata lens is not seeing the adapter");
		Assert.All(toolRows, row => Assert.EndsWith(row.PatchClass, row.PatchClassType, StringComparison.Ordinal));
		var unmatched = toolRows.Where(row => !adapterRows.Any(candidate => Matches(row, candidate))).ToList();
		Assert.True(unmatched.Count == 0, "tool contract rows the adapter does not declare: " + string.Join("; ", unmatched.Select(row => $"{row.PatchClass} → {row.TargetType}.{row.Method}")));
	}

	[Fact]
	public void ToolRows_CoverEveryAttributedPatchClass()
	{
		var toolRows = PatchContractReader.Read(AdapterPath);

		Assert.Equal(CountTargets(), toolRows.Count);
	}

	[Fact]
	public void RowsTheToolCannotSee_AreExactlyTheHandDeclaredDynamicOnes()
	{
		var toolRows = PatchContractReader.Read(AdapterPath);
		var adapterRows = BuildContracts();

		var unseen = adapterRows.Where(row => !toolRows.Any(candidate => Matches(candidate, row))).ToList();
		Assert.NotEmpty(unseen);
		Assert.All(unseen, row => Assert.EndsWith("(dynamic)", row.PatchClass, StringComparison.Ordinal));

		// The boundary is pinned by name AND by count: the tool's report, the ticket and
		// the cycle's self-check all say "nine hand-declared contracts", so a tenth must
		// fail here rather than quietly make a committed sentence false.
		Assert.Equal(9, unseen.Count);
	}

	[Fact]
	public void AdapterAndToolAgreeOnTheContractCount()
	{
		var toolRows = PatchContractReader.Read(AdapterPath);
		var adapterRows = BuildContracts();
		var dynamicRows = adapterRows.Count(row => row.PatchClass.EndsWith("(dynamic)", StringComparison.Ordinal));

		Assert.Equal(adapterRows.Count - dynamicRows, toolRows.Count);
	}

	[Theory]
	[InlineData("System.Void", "System.Void")]
	[InlineData("Body", "Body")]
	[InlineData("System.Int32[]", "System.Int32[]")]
	[InlineData("System.String&", "System.String&")]
	[InlineData("Outer+Inner", "Outer+Inner")]
	[InlineData("System.Collections.Generic.List`1[[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]", "System.Collections.Generic.List`1<System.String>")]
	[InlineData("System.Collections.Generic.Dictionary`2[[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.Single, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]", "System.Collections.Generic.Dictionary`2<System.Int32,System.Single>")]
	public void ReflectionNames_AreCanonicalisedToTheSnapshotSpelling(string reflectionName, string expected) => Assert.Equal(expected, ReflectionTypeName.Canonical(reflectionName));

	private static bool Matches(SnapshotContract tool, PatchContract adapter) =>
		tool.PatchClass == adapter.PatchClass
		&& tool.TargetType == ReflectionTypeName.Canonical(adapter.TargetType)
		&& tool.Method == adapter.MethodName
		&& tool.ArgumentTypes.SequenceEqual(adapter.ParameterTypes.Select(ReflectionTypeName.Canonical), StringComparer.Ordinal)
		&& tool.PatchParameters.SequenceEqual(adapter.PatchParameters, StringComparer.Ordinal);

	private static List<PatchContract> BuildContracts()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found in the adapter assembly.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		return (List<PatchContract>)build.Invoke(null, null)!;
	}

	private static int CountTargets()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found in the adapter assembly.");
		var count = inventory.GetMethod("CountTargets", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.CountTargets not found.");
		return (int)count.Invoke(null, null)!;
	}
}
