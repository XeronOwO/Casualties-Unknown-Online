using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Gate for the wire surface. The protocol version is one declared integer; nothing else checks that it
/// still describes the wire. This gate derives the surface the wire actually declares — message ids, the
/// <c>[ProtoContract]</c> types, their <c>[ProtoMember]</c> members, and the enums that ride them — and
/// compares it with the reviewed record in <c>docs/contracts/wire-surface-baseline.txt</c>, so a moved tag,
/// a changed type or a renumbered enum is a diff in the failure message instead of a reviewer's memory.
///
/// The comparison is INERT while <c>ProtocolVersion.Current</c> still names <c>UnreleasedBaseline</c>: the
/// pre-release freeze (decision 241) makes a surface move legitimate, and a gate that fired on every
/// pre-release wire change would be turned off instead of listened to. It arms the moment the constant stops
/// naming the baseline — the release day's deliberate act — and a drift then fails with the changed lines.
/// </summary>
public class WireSurfaceGateTests
{
	[Fact]
	public void TheWireSurface_MatchesTheRecordedBaseline()
	{
		var scan = WireSurfaceGate.ScanTree();
		var derived = WireSurfaceGate.Render(scan);

		if (!File.Exists(RepositoryPaths.File(WireSurfaceGate.BaselinePath)))
		{
			WireSurfaceGate.EmitForReview(scan);
			Assert.Fail(
				$"{WireSurfaceGate.BaselinePath} is missing; review the derived candidate at "
				+ $"{WireSurfaceGate.CandidatePath} and commit it as the recorded surface.");
		}

		var recorded = RepositoryPaths.ReadText(WireSurfaceGate.BaselinePath);

		if (WireSurfaceGate.IsInert())
		{
			// The version is frozen before the release, so a surface move is the normal case: keep the
			// release-day candidate fresh without failing the tree.
			if (!string.Equals(derived, recorded.TrimEnd(), StringComparison.Ordinal))
			{
				WireSurfaceGate.EmitForReview(scan);
			}

			return;
		}

		var comparison = WireSurfaceGate.Compare(derived, recorded);
		if (comparison.IsClean)
		{
			return;
		}

		WireSurfaceGate.EmitForReview(scan);
		Assert.Fail(
			$"The wire surface no longer matches its recorded baseline. Review the candidate at "
			+ $"{WireSurfaceGate.CandidatePath}, update {WireSurfaceGate.BaselinePath}, and bump "
			+ $"ProtocolVersion.Current in the same change (decision 241); a removal also needs a "
			+ $"'*REMOVED* <key> — <reason>' tombstone."
			+ Environment.NewLine + comparison.Describe());
	}

	[Fact]
	public void TheBaselineAndTheScan_MeetTheCensusFloor()
	{
		var scan = WireSurfaceGate.ScanTree();
		var lines = WireSurfaceGate.Render(scan).Split('\n', StringSplitOptions.RemoveEmptyEntries);
		var recorded = RepositoryPaths.ReadText(WireSurfaceGate.BaselinePath)
			.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Where(line => !line.StartsWith('#') && !line.StartsWith("*REMOVED*", StringComparison.Ordinal))
			.ToArray();

		var messages = lines.Count(line => line.StartsWith("msg|", StringComparison.Ordinal));
		var members = lines.Count(line => line.StartsWith("member|", StringComparison.Ordinal));
		var ids = lines.Count(line => line.StartsWith("id|", StringComparison.Ordinal));
		var enums = lines.Count(line => line.StartsWith("enum|", StringComparison.Ordinal));

		var failures = new List<string>();
		if (scan.FileCount < WireSurfaceGate.MinimumFiles)
		{
			failures.Add($"the scan read {scan.FileCount} declared files (floor {WireSurfaceGate.MinimumFiles})");
		}

		if (messages < WireSurfaceGate.MinimumMessageTypes)
		{
			failures.Add($"the scan derived {messages} message types (floor {WireSurfaceGate.MinimumMessageTypes})");
		}

		if (members < WireSurfaceGate.MinimumMemberLines)
		{
			failures.Add($"the scan derived {members} member lines (floor {WireSurfaceGate.MinimumMemberLines})");
		}

		if (ids < WireSurfaceGate.MinimumIdLines)
		{
			failures.Add($"the scan derived {ids} message ids (floor {WireSurfaceGate.MinimumIdLines})");
		}

		if (enums < WireSurfaceGate.MinimumEnums)
		{
			failures.Add($"the scan derived {enums} enum value lines (floor {WireSurfaceGate.MinimumEnums})");
		}

		if (recorded.Length < WireSurfaceGate.MinimumMemberLines)
		{
			failures.Add($"the baseline records {recorded.Length} entries (floor {WireSurfaceGate.MinimumMemberLines})");
		}

		Assert.True(failures.Count == 0, "wire-surface census floor failed: " + string.Join("; ", failures));
	}

	[Fact]
	public void TheArmingSwitch_IsInertWhileCurrentNamesTheBaselineAndArmedOnceItDoesNot()
	{
		const string inert = """
			public static class ProtocolVersion
			{
				public const int UnreleasedBaseline = 1;
				public const int Current = UnreleasedBaseline;
			}
			""";

		const string armed = """
			public static class ProtocolVersion
			{
				public const int UnreleasedBaseline = 1;
				public const int Current = 2;
			}
			""";

		Assert.False(WireSurfaceGate.IsArmedFor(inert), "Current naming UnreleasedBaseline is the pre-release freeze");
		Assert.True(WireSurfaceGate.IsArmedFor(armed), "a literal Current is the release day's act and arms the gate");
	}

	[Fact]
	public void TheComparison_NamesWhatChangedByKey()
	{
		const string recorded = """
			id|Ping=1
			msg|PingMsg
			member|PingMsg|1|int64|single
			enum|PingKind|Tick=0
			""";

		const string derived = """
			id|Ping=1
			msg|PingMsg
			member|PingMsg|1|int32|single
			member|PingMsg|2|string|single
			enum|PingKind|Tick=1
			""";

		var comparison = WireSurfaceGate.Compare(derived, recorded);
		Assert.False(comparison.IsClean);
		var description = comparison.Describe();
		Assert.Contains("member|PingMsg|1", description, StringComparison.Ordinal);
		Assert.Contains("member|PingMsg|2", description, StringComparison.Ordinal);
		Assert.Contains("enum|PingKind|Tick", description, StringComparison.Ordinal);
	}

	[Fact]
	public void TheDerivation_ReadsTheWireAttributesAndNothingElse()
	{
		const string source = """
			using ProtoBuf;

			namespace X;

			[ProtoContract]
			public sealed class WireMsg
			{
				[ProtoMember(2)]
				public int Count { get; set; }

				[ProtoMember(1, DataFormat = DataFormat.FixedSize)]
				public float Ratio { get; set; }

				public string Note { get; set; } = "";
			}

			public sealed class NotOnTheWire
			{
				[ProtoMember(9)]
				public int Ignored { get; set; }
			}

			public enum WireKind
			{
				First = 4,
				Second,
			}
			""";

		var lines = WireSurfaceGate.DeriveFromSource(source);

		Assert.Contains("msg|WireMsg", lines);
		Assert.Contains("member|WireMsg|1|float|single|fixed", lines);
		Assert.Contains("member|WireMsg|2|int32|single", lines);
		Assert.Contains("enum|WireKind|First=4", lines);
		Assert.Contains("enum|WireKind|Second=5", lines);
		Assert.DoesNotContain(lines, line => line.Contains("Ignored", StringComparison.Ordinal));
		Assert.DoesNotContain(lines, line => line.Contains("Note", StringComparison.Ordinal));
		Assert.DoesNotContain(lines, line => line.Contains("NotOnTheWire", StringComparison.Ordinal));
	}
}
