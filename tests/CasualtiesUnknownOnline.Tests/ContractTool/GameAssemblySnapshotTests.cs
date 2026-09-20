using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.ContractTool.Diff;
using CasualtiesUnknownOnline.ContractTool.Snapshot;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.ContractTool;

/// <summary>
/// The tool against the real game assembly: the census floor proves the snapshot
/// is not silently empty, byte-reproducibility is measured on the real input (not
/// on a hand-built document), and a self-diff proves the contract lens actually
/// resolves every declared target against the shipped build.
/// </summary>
[Trait("Category", "Integration")]
public class GameAssemblySnapshotTests
{
	private static readonly string GameAssembly = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assembly-CSharp.dll");

	private static readonly string AdapterAssembly = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CasualtiesUnknownOnline.GameAdapter.dll");

	[Fact]
	public void Snapshot_OfTheRealGameAssembly_IsByteReproducible()
	{
		var first = SnapshotWriter.ToJson(GameAssemblyReader.Read(GameAssembly));
		var second = SnapshotWriter.ToJson(GameAssemblyReader.Read(GameAssembly));

		Assert.Equal(first, second);
	}

	[Fact]
	public void Snapshot_OfTheRealGameAssembly_MeetsTheCensusFloor()
	{
		var counts = GameAssemblyReader.Read(GameAssembly).Counts;

		Assert.True(counts.Types > 300, $"only {counts.Types} types snapshotted");
		Assert.True(counts.Methods > 3000, $"only {counts.Methods} methods snapshotted");
		Assert.True(counts.Fields > 2000, $"only {counts.Fields} fields snapshotted");
		Assert.True(counts.EnumMembers > 100, $"only {counts.EnumMembers} enum members snapshotted");
		Assert.True(counts.SerializedFields > 500, $"only {counts.SerializedFields} serialized fields snapshotted");
	}

	[Fact]
	public void SnapshotWithTheAdapter_CarriesTheRealContractRows()
	{
		var document = GameAssemblyReader.Read(GameAssembly, AdapterAssembly);

		Assert.True(document.Counts.Contracts >= 100, $"only {document.Counts.Contracts} contract rows embedded");
		Assert.Equal(document.Counts.Contracts, document.Contracts.Count);
	}

	[Fact]
	public void SelfDiff_ResolvesEveryContractInsideThisAssemblyAndBreaksNothing()
	{
		var document = GameAssemblyReader.Read(GameAssembly, AdapterAssembly);
		var result = SnapshotDiffer.Diff(document, document);

		// A contract may target a type outside the snapshotted assembly (the adapter
		// hooks SceneManager.LoadScene, and SceneManager lives in a Unity module): the
		// snapshot covers one assembly, so that row is reported unresolved here and is
		// judged by the contract tests, which resolve against every referenced assembly.
		var names = new HashSet<string>(document.Types.Select(type => type.Name), StringComparer.Ordinal);
		var outside = document.Contracts.Count(contract => !names.Contains(contract.TargetType));
		Assert.True(outside > 0, "expected at least one contract to target a type outside this assembly");

		Assert.Equal(outside, result.CurrentLens.Unresolved);
		Assert.Equal(document.Contracts.Count - outside, result.CurrentLens.Resolved);
		Assert.Equal(0, result.CurrentLens.Ambiguous);
		Assert.False(result.HasBrokenContracts);
		Assert.Equal(result.CurrentLens.Resolved, result.Differences.Count);
		Assert.All(result.Differences, difference => Assert.Equal(DifferenceKind.UnchangedNeedsReview, difference.Kind));
	}
}
