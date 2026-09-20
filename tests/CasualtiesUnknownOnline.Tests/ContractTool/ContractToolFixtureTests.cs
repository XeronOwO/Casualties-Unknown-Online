using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasualtiesUnknownOnline.ContractTool.Diff;
using CasualtiesUnknownOnline.ContractTool.Report;
using CasualtiesUnknownOnline.ContractTool.Snapshot;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.ContractTool;

/// <summary>
/// The acceptance pair, end to end: two emitted "builds" of one game assembly and
/// the adapter that declares the contracts, snapshotted as metadata and diffed.
/// A deliberately moved member must come out CLASSIFIED — the classification is
/// the deliverable, and every row below names the verdict a reader acts on.
/// </summary>
[Trait("Category", "Integration")]
public class ContractToolFixtureTests
{
	private static readonly ContractFixtureAssemblies.FixtureSet Fixtures = ContractFixtureAssemblies.Build("fixture");

	private static readonly SnapshotDocument Previous = GameAssemblyReader.Read(Fixtures.PreviousGame, Fixtures.Adapter);

	private static readonly SnapshotDocument Current = GameAssemblyReader.Read(Fixtures.CurrentGame, Fixtures.Adapter);

	private static readonly DiffResult Result = SnapshotDiffer.Diff(Previous, Current);

	[Fact]
	public void RenamedMethod_IsClassifiedWithItsRenameCandidate()
	{
		var difference = Assert.Single(Result.Differences, row => row.Kind == DifferenceKind.RemovedOrRenamed && row.Subject == "ContractFixture.FixtureTarget.Renamed(System.Int32)");

		Assert.Contains("RenamedNow", difference.Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void UnconstrainedTargetGainingAnOverload_IsClassifiedAsAmbiguous()
	{
		var difference = Assert.Single(Result.Differences, row => row.Kind == DifferenceKind.HarmonyTargetAmbiguous);

		Assert.Equal(DifferenceScope.Contract, difference.Scope);
		Assert.Equal("ContractFixture.HookedPatch → ContractFixture.FixtureTarget.Hooked", difference.Subject);
		Assert.True(Result.HasBrokenContracts);
	}

	[Fact]
	public void RenamedTargetParameter_IsClassifiedAsAByNameBreak()
	{
		var difference = Assert.Single(Result.Differences, row => row.Kind == DifferenceKind.SignatureChanged && row.Subject.StartsWith("ContractFixture.CarriesPatch", StringComparison.Ordinal));

		Assert.Contains("patch parameter 'amount' is missing", difference.Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void UntouchedContract_IsClassifiedAsStillNeedingASemanticLook()
	{
		var difference = Assert.Single(Result.Differences, row => row.Kind == DifferenceKind.UnchangedNeedsReview && row.Subject.StartsWith("ContractFixture.PlainPatch", StringComparison.Ordinal));

		Assert.Equal("ContractFixture.PlainPatch → ContractFixture.FixtureTarget.Plain", difference.Subject);
	}

	[Fact]
	public void FieldTypeAndVisibilityMoves_AreClassifiedAsFieldShapeChanged()
	{
		var typeMove = Assert.Single(Result.Differences, row => row.Kind == DifferenceKind.FieldShapeChanged && row.Subject == "ContractFixture.FixtureTarget.Counter");
		var visibilityMove = Assert.Single(Result.Differences, row => row.Kind == DifferenceKind.FieldShapeChanged && row.Subject == "ContractFixture.FixtureTarget.Visible");

		Assert.Contains("type: System.Int32 → System.Single", typeMove.Detail, StringComparison.Ordinal);
		Assert.Contains("visibility: public → private", visibilityMove.Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void EnumValueMove_AndTheRemainingKinds_AreClassified()
	{
		Assert.Single(Result.Differences, row => row.Kind == DifferenceKind.EnumValueChanged && row.Subject == "ContractFixture.FixtureMode.Second");
		Assert.Single(Result.Differences, row => row.Kind == DifferenceKind.RemovedOrRenamed && row.Subject == "ContractFixture.FixtureRenamedType");
		Assert.Single(Result.Differences, row => row.Kind == DifferenceKind.Added && row.Subject == "ContractFixture.FixtureAdded");
		Assert.Single(Result.Differences, row => row.Kind == DifferenceKind.SignatureChanged && row.Subject.StartsWith("ContractFixture.FixtureOther.Changed(", StringComparison.Ordinal) && row.Scope == DifferenceScope.OutsideContract);
	}

	[Fact]
	public void Snapshot_CarriesTheCanonicalTypeNamesAndTheSerializedFields()
	{
		var target = Assert.Single(Previous.Types, type => type.Name == "ContractFixture.FixtureTarget");
		var shapes = Assert.Single(target.Methods, method => method.Name == "Shapes");

		Assert.Equal(["System.Collections.Generic.List`1<System.String>", "System.Int32[]"], shapes.Parameters.Select(parameter => parameter.Type));
		Assert.Contains(Previous.Types, type => type.Name == "ContractFixture.FixtureTarget+Inner");
		Assert.Equal(3, target.Fields.Count(field => field.IsSerialized));
		Assert.True(Previous.Counts.Types >= 5, "the fixture snapshot must carry a real census");
	}

	[Fact]
	public void ContractRows_ComeFromTheAdapterAssemblyMetadata()
	{
		Assert.Equal(3, Previous.Contracts.Count);
		var constrained = Assert.Single(Previous.Contracts, contract => contract.PatchClass == "CarriesPatch");

		Assert.Equal("ContractFixture.FixtureTarget", constrained.TargetType);
		Assert.Equal("Carries", constrained.Method);
		Assert.Equal(["System.Int32"], constrained.ArgumentTypes);
		Assert.Equal(["amount"], constrained.PatchParameters);
	}

	[Fact]
	public void SnapshotWithoutAnAdapter_CarriesNoContracts()
	{
		var document = GameAssemblyReader.Read(Fixtures.PreviousGame);

		Assert.Empty(document.Contracts);
		Assert.Equal(0, document.Counts.Contracts);
	}

	[Fact]
	public void SnapshotBytes_AreReproducible() => Assert.Equal(SnapshotWriter.ToJson(Previous), SnapshotWriter.ToJson(GameAssemblyReader.Read(Fixtures.PreviousGame, Fixtures.Adapter)));

	[Fact]
	public void Report_StatesEveryTierAndItsOwnLimits()
	{
		var report = CompatibilityReport.Render(Result);

		Assert.Contains("## Contract verdicts", report, StringComparison.Ordinal);
		Assert.Contains("## Contract-adjacent changes", report, StringComparison.Ordinal);
		Assert.Contains("## Outside the contract lens", report, StringComparison.Ordinal);
		Assert.Contains("contract-breaking difference", report, StringComparison.Ordinal);
		Assert.Contains("## Limits", report, StringComparison.Ordinal);
		Assert.Contains("dynamic contracts are not in this lens", report, StringComparison.Ordinal);
		Assert.Contains("decision 199", report, StringComparison.Ordinal);
	}

	[Fact]
	public void DiffJson_CarriesTheMachineKeysAndTheSummary()
	{
		var path = Path.Combine(Fixtures.Directory, "diff.json");
		DiffWriter.Write(Result, path);

		using var document = JsonDocument.Parse(File.ReadAllText(path));
		var root = document.RootElement;
		Assert.Equal(SnapshotSchema.Diff, root.GetProperty("schema").GetString());
		Assert.True(root.GetProperty("summary").GetProperty("brokenContracts").GetBoolean());
		Assert.Equal(1, root.GetProperty("summary").GetProperty("harmony-target-ambiguous").GetInt32());
		var rows = root.GetProperty("differences").EnumerateArray().ToList();
		Assert.Contains(rows, row => row.GetProperty("kind").GetString() == "harmony-target-ambiguous" && row.GetProperty("scope").GetString() == "contract");
		Assert.Equal(Result.Differences.Count, rows.Count);
	}
}
