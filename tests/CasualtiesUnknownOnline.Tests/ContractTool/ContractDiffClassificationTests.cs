using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.ContractTool.Diff;
using CasualtiesUnknownOnline.ContractTool.Snapshot;
using Xunit;
using static CasualtiesUnknownOnline.Tests.ContractTool.ContractSnapshotBuilder;

namespace CasualtiesUnknownOnline.Tests.ContractTool;

/// <summary>
/// The classification contract: every kind of game-update difference the ticket
/// names is not merely listed but CLASSIFIED, and the verdict rules match the
/// runtime guard's (exact argument types, no name-only fallback, unconstrained
/// targets ambiguous, patch parameter names bound by name). These cases drive
/// the differ with explicit snapshot pairs so each rule is pinned on its own.
/// </summary>
public class ContractDiffClassificationTests
{
	private const string Target = "FixtureTarget";

	[Fact]
	public void NoChange_ProducesNoDifferences()
	{
		var types = new[] { Type(Target, methods: [Method("Hooked")], fields: [Field("Counter", "System.Int32")]) };
		var result = SnapshotDiffer.Diff(Document(types), Document(types));

		Assert.Empty(result.Differences);
		Assert.False(result.HasBrokenContracts);
	}

	[Fact]
	public void RenamedMethod_IsClassifiedAsRemovedOrRenamedWithARenameCandidate()
	{
		var result = SnapshotDiffer.Diff(
			Document([Type(Target, methods: [Method("Renamed", "System.Void", "System.Int32")])], Contracts()),
			Document([Type(Target, methods: [Method("RenamedNow", "System.Void", "System.Int32")])], Contracts()));

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.RemovedOrRenamed && row.Subject.StartsWith(Target, StringComparison.Ordinal));
		Assert.Contains("RenamedNow", difference.Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void ReturnTypeChange_IsClassifiedAsSignatureChanged()
	{
		var result = SnapshotDiffer.Diff(
			Baseline(),
			Document([Type(Target, methods: [Method("Hooked", "System.Single")])], Contracts()));

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.SignatureChanged && row.Scope == DifferenceScope.Contract);
		Assert.Contains("returns: System.Void", difference.Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void UnconstrainedTargetGainingAnOverload_IsClassifiedAsHarmonyTargetAmbiguous()
	{
		var result = SnapshotDiffer.Diff(
			Baseline(),
			Document([Type(Target, methods: [Method("Hooked"), Method("Hooked", "System.Void", "System.Int32")])], Contracts()));

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.HarmonyTargetAmbiguous);
		Assert.Equal(DifferenceScope.Contract, difference.Scope);
		Assert.Contains("HookedPatch", difference.Subject, StringComparison.Ordinal);
		Assert.True(result.HasBrokenContracts);
	}

	[Fact]
	public void ConstrainedTargetGainingAnUnrelatedOverload_KeepsItsVerdict()
	{
		var result = SnapshotDiffer.Diff(
			Document([Type(Target, methods: [MethodWithParameters("Carries", "System.Void", Parameter("amount", "System.Int32"))])], ConstrainedContracts()),
			Document([Type(Target, methods: [MethodWithParameters("Carries", "System.Void", Parameter("amount", "System.Int32")), Method("Carries", "System.Void", "System.Int32", "System.Int32")])], ConstrainedContracts()));

		Assert.DoesNotContain(result.Differences, row => row.Kind == DifferenceKind.HarmonyTargetAmbiguous);
		Assert.Single(result.Differences, row => row.Kind == DifferenceKind.UnchangedNeedsReview);
	}

	[Fact]
	public void ConstrainedTargetLosingItsExactSignature_IsClassifiedAsSignatureChanged()
	{
		var result = SnapshotDiffer.Diff(
			Document([Type(Target, methods: [MethodWithParameters("Carries", "System.Void", Parameter("amount", "System.Int32"))])], ConstrainedContracts()),
			Document([Type(Target, methods: [MethodWithParameters("Carries", "System.Void", Parameter("amount", "System.Int64"))])], ConstrainedContracts()));

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.SignatureChanged && row.Scope == DifferenceScope.Contract);
		Assert.Contains("no overload matches the declared argument types", difference.Detail, StringComparison.Ordinal);
		Assert.True(result.HasBrokenContracts);
	}

	[Fact]
	public void ContractTargetRemoved_IsClassifiedAsRemovedOrRenamed()
	{
		var result = SnapshotDiffer.Diff(
			Baseline(),
			Document([Type(Target, methods: [])], Contracts()));

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.RemovedOrRenamed && row.Scope == DifferenceScope.Contract);
		Assert.Contains("would not install", difference.Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void RemovedContractTarget_IsReportedOnce()
	{
		var result = SnapshotDiffer.Diff(Baseline(), Document([Type(Target, methods: [])], Contracts()));

		// The contract verdict carries the removal; the member-level pass must not repeat
		// the same hook as a second row under the same subject.
		Assert.Single(result.Differences, row => row.Subject.Contains("FixtureTarget.Hooked", StringComparison.Ordinal));
	}

	[Fact]
	public void ContractThatOnlyBecomesResolvableNow_DoesNotHideTheMemberChange()
	{
		// Before: the constrained contract matches nothing (the target still takes a
		// string), so no verdict is owed for it. After: the parameter moved to int and the
		// contract resolves. The change underneath MUST stay visible — this is the shape of
		// a hook whose target moved between the two snapshots.
		var result = SnapshotDiffer.Diff(
			Document([Type(Target, methods: [MethodWithParameters("Carries", "System.Void", Parameter("amount", "System.String"))])], ConstrainedContracts()),
			Document([Type(Target, methods: [MethodWithParameters("Carries", "System.Void", Parameter("amount", "System.Int32"))])], ConstrainedContracts()));

		var difference = Assert.Single(result.Differences, row => row.Subject.StartsWith(Target, StringComparison.Ordinal));
		Assert.Equal(DifferenceKind.SignatureChanged, difference.Kind);
		Assert.Equal(DifferenceScope.ContractAdjacent, difference.Scope);
		Assert.Contains("parameters: (System.String) → (System.Int32)", difference.Detail, StringComparison.Ordinal);
		Assert.False(result.HasBrokenContracts);
	}

	[Fact]
	public void UnconstrainedTargetWithARetypedParameter_IsClassifiedAsSignatureChanged()
	{
		// An unconstrained [HarmonyPatch] target resolves by NAME, so a parameter whose
		// TYPE moved is the one shape move the runtime guard accepts silently — and the
		// tool must not: reporting "structurally identical" here would be a false
		// negative on the tool's own question ("what did the game change?").
		var result = SnapshotDiffer.Diff(
			Document([Type(Target, methods: [MethodWithParameters("Hooked", "System.Void", Parameter("atk", "AttackInfo"))])], Contracts()),
			Document([Type(Target, methods: [MethodWithParameters("Hooked", "System.Void", Parameter("atk", "System.String"))])], Contracts()));

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.SignatureChanged && row.Scope == DifferenceScope.Contract);
		Assert.Contains("parameters: (AttackInfo) → (System.String)", difference.Detail, StringComparison.Ordinal);
		Assert.True(result.HasBrokenContracts);
	}

	[Fact]
	public void PatchParameterNameMissingFromTheTarget_IsClassifiedAsSignatureChanged()
	{
		var result = SnapshotDiffer.Diff(
			Document([Type(Target, methods: [MethodWithParameters("Carries", "System.Void", Parameter("amount", "System.Int32"))])], ConstrainedContracts()),
			Document([Type(Target, methods: [MethodWithParameters("Carries", "System.Void", Parameter("count", "System.Int32"))])], ConstrainedContracts()));

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.SignatureChanged && row.Scope == DifferenceScope.Contract);
		Assert.Contains("Harmony binds patch arguments by name", difference.Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void FieldTypeChange_IsClassifiedAsFieldShapeChanged()
	{
		var result = SnapshotDiffer.Diff(
			Baseline(),
			Document([Type(Target, methods: [Method("Hooked")], fields: [Field("Counter", "System.Single")])], Contracts()));

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.FieldShapeChanged);
		Assert.Equal(DifferenceScope.ContractAdjacent, difference.Scope);
		Assert.Contains("type: System.Int32 → System.Single", difference.Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void FieldVisibilityChange_IsClassifiedAsFieldShapeChanged()
	{
		var result = SnapshotDiffer.Diff(
			Baseline(),
			Document([Type(Target, methods: [Method("Hooked")], fields: [Field("Counter", "System.Int32", "private")])], Contracts()));

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.FieldShapeChanged);
		Assert.Contains("visibility: public → private", difference.Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void EnumValueChange_IsClassifiedAsEnumValueChanged()
	{
		var result = SnapshotDiffer.Diff(
			Document([Type("FixtureMode", "enum", enumMembers: [EnumMember("Second", "1")])]),
			Document([Type("FixtureMode", "enum", enumMembers: [EnumMember("Second", "2")])]));

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.EnumValueChanged);
		Assert.Equal(DifferenceScope.OutsideContract, difference.Scope);
		Assert.Equal(("1", "2"), (difference.Previous, difference.Current));
	}

	[Fact]
	public void UnchangedContract_IsClassifiedAsUnchangedNeedsReviewAndIsNotBroken()
	{
		var result = SnapshotDiffer.Diff(Baseline(), Baseline());

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.UnchangedNeedsReview);
		Assert.Equal(DifferenceScope.Contract, difference.Scope);
		Assert.False(result.HasBrokenContracts);
	}

	[Fact]
	public void ChangeOutsideTheLens_IsClassifiedOutsideTheContractScope()
	{
		var result = SnapshotDiffer.Diff(
			Document([Type(Target, methods: [Method("Hooked")]), Type("FixtureOther", methods: [Method("Changed")])], Contracts()),
			Document([Type(Target, methods: [Method("Hooked")]), Type("FixtureOther", methods: [Method("Changed", "System.Void", "System.Int32")])], Contracts()));

		var difference = Assert.Single(result.Differences, row => row.Subject.StartsWith("FixtureOther", StringComparison.Ordinal));
		Assert.Equal(DifferenceScope.OutsideContract, difference.Scope);
		Assert.Equal(DifferenceKind.SignatureChanged, difference.Kind);
	}

	[Fact]
	public void RemovedTypeWithTheSameMemberSet_NamesARenameCandidate()
	{
		var result = SnapshotDiffer.Diff(
			Document([Type("OldName", methods: [Method("Kept")])]),
			Document([Type("NewName", methods: [Method("Kept")])]));

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.RemovedOrRenamed);
		Assert.Contains("NewName", difference.Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void AddedMembers_AreRecordedAsAdded()
	{
		var result = SnapshotDiffer.Diff(
			Document([Type(Target, methods: [Method("Hooked")])], Contracts()),
			Document([Type(Target, methods: [Method("Hooked"), Method("Fresh")])], Contracts()));

		var difference = Assert.Single(result.Differences, row => row.Kind == DifferenceKind.Added);
		Assert.Contains("Fresh", difference.Subject, StringComparison.Ordinal);
	}

	[Fact]
	public void SnapshotsWithDifferentContractSets_AreRefused()
	{
		var exception = Assert.Throws<InvalidOperationException>(() => SnapshotDiffer.Diff(Baseline(), Document([Type(Target, methods: [Method("Hooked")])])));

		Assert.Contains("different contract sets", exception.Message, StringComparison.Ordinal);
	}

	private static SnapshotDocument Baseline() =>
		Document([Type(Target, methods: [Method("Hooked")], fields: [Field("Counter", "System.Int32")])], Contracts());

	private static IReadOnlyList<SnapshotContract> Contracts() =>
		[Contract("HookedPatch", Target, "Hooked")];

	private static IReadOnlyList<SnapshotContract> ConstrainedContracts() =>
		[Contract("CarriesPatch", Target, "Carries", ["System.Int32"], ["amount"])];
}
