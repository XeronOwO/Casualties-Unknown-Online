using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Gate + contract tests for the Mod API public-surface baseline
/// (<c>docs/contracts/abstractions-api-baseline.txt</c>). The first test re-derives the
/// Abstractions public surface from the project's source and compares it with the
/// recorded one; the rest pin the comparison's own contract with synthetic
/// sources, so the gate cannot pass by finding nothing.
/// </summary>
public class ApiSurfaceGateTests
{
	[Fact]
	public void AbstractionsPublicSurface_MatchesTheReviewedBaseline()
	{
		var scan = ApiSurfaceGate.ScanTree();
		if (!File.Exists(RepositoryPaths.File(ApiSurfaceGate.BaselinePath)))
		{
			ApiSurfaceGate.EmitForReview(scan, []);
			Assert.Fail(
				$"{ApiSurfaceGate.BaselinePath} is missing; review the generated candidate at "
				+ $"{ApiSurfaceGate.EmittedBaselinePath} and commit it as the reviewed baseline.");
		}

		var comparison = ApiSurfaceGate.Compare(scan, RepositoryPaths.ReadText(ApiSurfaceGate.BaselinePath));

		if (comparison.IsClean)
		{
			return;
		}

		ApiSurfaceGate.EmitForReview(scan, comparison.Tombstones);
		Assert.Fail(
			$"The Abstractions public surface no longer matches its reviewed baseline. Review the candidate at "
			+ $"{ApiSurfaceGate.EmittedBaselinePath}, update {ApiSurfaceGate.BaselinePath} (a removal also needs a "
			+ $"'{ApiSurfaceGate.RemovalMarker} <key> — <reason>' tombstone), and see docs/api/advanced-modification-policy.md."
			+ Environment.NewLine + comparison.Describe());
	}

	[Fact]
	public void TheBaselineAndTheScan_MeetTheCensusFloor()
	{
		var scan = ApiSurfaceGate.ScanTree();
		var baselineText = RepositoryPaths.ReadText(ApiSurfaceGate.BaselinePath);
		var comparison = ApiSurfaceGate.Compare(scan, baselineText);

		var failures = new List<string>();
		if (scan.FileCount < ApiSurfaceGate.SourceFileFloor)
		{
			failures.Add($"the scan read {scan.FileCount} source files (floor {ApiSurfaceGate.SourceFileFloor})");
		}

		if (scan.Entries.Count < ApiSurfaceGate.EntryCensusFloor)
		{
			failures.Add($"the tree yielded {scan.Entries.Count} surface entries (floor {ApiSurfaceGate.EntryCensusFloor})");
		}

		if (comparison.SurfaceTypeCount < ApiSurfaceGate.TypeCensusFloor)
		{
			failures.Add($"the tree yielded {comparison.SurfaceTypeCount} public types (floor {ApiSurfaceGate.TypeCensusFloor})");
		}

		if (comparison.Baseline.Count < ApiSurfaceGate.EntryCensusFloor)
		{
			failures.Add($"the baseline records {comparison.Baseline.Count} entries (floor {ApiSurfaceGate.EntryCensusFloor})");
		}

		if (comparison.BaselineTypeCount < ApiSurfaceGate.TypeCensusFloor)
		{
			failures.Add($"the baseline records {comparison.BaselineTypeCount} public types (floor {ApiSurfaceGate.TypeCensusFloor})");
		}

		Assert.True(failures.Count == 0, "public-surface census floor failed: " + string.Join("; ", failures));
	}

	[Fact]
	public void TheMatcher_FlagsAnAddedMemberAsAnApiChange()
	{
		var comparison = CompareWithBaselineOf(SourceWithoutExtraMember, SourceWithExtraMember);
		var added = comparison.Findings.Where(finding => finding.Kind == "ADDED").ToList();

		Assert.True(added.Count == 1, comparison.Describe());
		Assert.True(
			added[0].Detail.Contains("member|Stable|Sample.IContract.Extra(int attempts)|method", StringComparison.Ordinal),
			comparison.Describe());
	}

	[Fact]
	public void TheMatcher_FlagsARemovalWithoutATombstoneAndAcceptsOneWith()
	{
		var withoutTombstone = CompareWithBaselineOf(SourceWithExtraMember, SourceWithoutExtraMember);
		Assert.Contains(withoutTombstone.Findings, finding => finding.Kind == "REMOVED");

		var scan = Scan(SourceWithoutExtraMember);
		var withTombstone = ApiSurfaceGate.Compare(scan, BaselineText(SourceWithoutExtraMember, "member|Sample.IContract.Extra(int)", "retired with the sample change"));
		Assert.True(withTombstone.IsClean, withTombstone.Describe());
	}

	[Fact]
	public void TheMatcher_FlagsALevelChangeAndAMalformedBaselineLine()
	{
		var levelChange = CompareWithBaselineOf(SourceWithStableMarker, SourceWithAdvancedMarker);
		Assert.Contains(levelChange.Findings, finding => finding.Kind == "CHANGED");

		var malformed = ApiSurfaceGate.Compare(Scan(SourceWithoutExtraMember), "type|Sample.IContract|interface|-" + Environment.NewLine);
		Assert.Contains(malformed.Findings, finding => finding.Kind == "MALFORMED");

		var bareTombstone = ApiSurfaceGate.Compare(Scan(SourceWithoutExtraMember), ApiSurfaceGate.RemovalMarker + " member|Sample.IContract.Extra(int)" + Environment.NewLine);
		Assert.Contains(bareTombstone.Findings, finding => finding.Kind == "MALFORMED");
	}

	[Fact]
	public void TheMatcher_IgnoresHowAReferenceIsSpelled()
	{
		var comparison = CompareWithBaselineOf(SourceWithQualifiedReferences, SourceWithUsingDirective);

		Assert.True(comparison.IsClean, comparison.Describe());
	}

	[Fact]
	public void TheMatcher_SeesACSharp14ExtensionMember()
	{
		var comparison = CompareWithBaselineOf(SourceWithoutExtensionMember, SourceWithExtensionMember);

		Assert.Contains(
			comparison.Findings,
			finding => finding.Kind == "ADDED" && finding.Detail.Contains("IsBlank(string text)", StringComparison.Ordinal));
	}

	[Fact]
	public void TheMatcher_FlagsAModifierOnlyChange()
	{
		var staticToInstance = CompareWithBaselineOf(SourceWithStaticFactory, SourceWithInstanceFactory);
		Assert.Contains(staticToInstance.Findings, finding => finding.Kind == "CHANGED");

		var readonlyStruct = CompareWithBaselineOf(SourceWithPlainStruct, SourceWithReadonlyStruct);
		Assert.Contains(readonlyStruct.Findings, finding => finding.Kind == "CHANGED");
	}

	[Fact]
	public void TheMatcher_AcceptsAPartialTypeSplitAcrossFiles()
	{
		var scan = ApiSurfaceGate.ExtractFromSources(
		[
			("a.cs", "namespace Sample;\n\npublic sealed partial class Split\n{\n\tpublic void A() { }\n}\n"),
			("b.cs", "namespace Sample;\n\npublic sealed partial class Split\n{\n\tpublic void B() { }\n}\n")
		]);
		var comparison = ApiSurfaceGate.Compare(scan, ApiSurfaceGate.BaselineTextFor(scan.Entries, []));

		Assert.True(comparison.IsClean, comparison.Describe());
	}

	[Fact]
	public void TheMatcher_SeesANestedTypeDeclaredInAnInterfaceWithoutAModifier()
	{
		var comparison = CompareWithBaselineOf(SourceWithBareInterface, SourceWithNestedInterfaceType);

		Assert.Contains(
			comparison.Findings,
			finding => finding.Kind == "ADDED" && finding.Detail.Contains("Sample.IContract+Nested", StringComparison.Ordinal));
	}

	[Fact]
	public void TheMatcher_FlagsAStabilityArgumentItCannotResolve()
	{
		var scan = Scan(SourceWithUnresolvableMarker);
		var comparison = ApiSurfaceGate.Compare(scan, ApiSurfaceGate.BaselineTextFor(scan.Entries, []));

		Assert.Contains(comparison.Findings, finding => finding.Kind == "MALFORMED");
	}

	private const string SourceWithoutExtraMember = """
		namespace Sample;

		public interface IContract
		{
			bool TryTake(string id, int count);
		}
		""";

	private const string SourceWithExtraMember = """
		namespace Sample;

		public interface IContract
		{
			bool TryTake(string id, int count);

			void Extra(int attempts);
		}
		""";

	private const string SourceWithStableMarker = """
		using CasualtiesUnknownOnline.Abstractions;

		namespace Sample;

		[ApiStability(ApiStabilityLevel.Stable)]
		public interface IContract
		{
			bool TryTake(string id, int count);
		}
		""";

	private const string SourceWithAdvancedMarker = """
		using CasualtiesUnknownOnline.Abstractions;

		namespace Sample;

		[ApiStability(ApiStabilityLevel.Advanced)]
		public interface IContract
		{
			bool TryTake(string id, int count);
		}
		""";

	private const string SourceWithUnresolvableMarker = """
		using CasualtiesUnknownOnline.Abstractions;

		namespace Sample;

		[ApiStability((ApiStabilityLevel)7)]
		public interface IContract
		{
			void M();
		}
		""";

	private const string SourceWithQualifiedReferences = """
		namespace Sample;

		public interface IContract
		{
			System.Collections.Generic.IReadOnlyList<Sample.Entry> TryTake(System.String id, System.Int32 count);
		}

		public sealed class Entry
		{
		}
		""";

	private const string SourceWithUsingDirective = """
		using System.Collections.Generic;

		namespace Sample;

		public interface IContract
		{
			IReadOnlyList<Entry> TryTake(string id, int count);
		}

		public sealed class Entry
		{
		}
		""";

	private const string SourceWithoutExtensionMember = """
		namespace Sample;

		public static class Ext
		{
		}
		""";

	private const string SourceWithExtensionMember = """
		namespace Sample;

		public static class Ext
		{
			extension(string text)
			{
				public bool IsBlank() => text.Length == 0;
			}
		}
		""";

	private const string SourceWithStaticFactory = """
		namespace Sample;

		public sealed class Factory
		{
			public static Factory Create() => new Factory();
		}
		""";

	private const string SourceWithInstanceFactory = """
		namespace Sample;

		public sealed class Factory
		{
			public Factory Create() => new Factory();
		}
		""";

	private const string SourceWithPlainStruct = """
		namespace Sample;

		public struct Box
		{
			public int Value { get; }
		}
		""";

	private const string SourceWithReadonlyStruct = """
		namespace Sample;

		public readonly struct Box
		{
			public int Value { get; }
		}
		""";

	private const string SourceWithBareInterface = """
		namespace Sample;

		public interface IContract
		{
			void M();
		}
		""";

	private const string SourceWithNestedInterfaceType = """
		namespace Sample;

		public interface IContract
		{
			void M();

			sealed class Nested
			{
			}
		}
		""";

	private static ApiSurfaceScan Scan(string source) =>
		ApiSurfaceGate.ExtractFromSources([("sample.cs", source)]);

	private static string BaselineText(string source, string? tombstoneKey = null, string? tombstoneReason = null)
	{
		IEnumerable<ApiSurfaceTombstone> tombstones = tombstoneKey is null
			? []
			: [new ApiSurfaceTombstone(tombstoneKey, tombstoneReason ?? "reason")];
		return ApiSurfaceGate.BaselineTextFor(Scan(source).Entries, tombstones);
	}

	private static ApiSurfaceComparison CompareWithBaselineOf(string baselineSource, string surfaceSource) =>
		ApiSurfaceGate.Compare(Scan(surfaceSource), BaselineText(baselineSource));
}
