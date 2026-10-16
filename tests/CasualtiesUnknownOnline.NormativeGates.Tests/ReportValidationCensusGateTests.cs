using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Member-report validation census guard. <c>docs/architecture/member-report-validation.md</c>
/// carries one row per wire member a member's own client can author, and the row
/// answers the question the standard makes binding: what does the HOST bound that
/// report against, or which anchor is it missing.
///
/// <para>
/// The reach is DERIVED, not declared here: a wire member is member-authored when a
/// packet handler serves it for <c>GuestToHost</c> or <c>Bidirectional</c>, or when
/// the kernel's authority policy does not declare its <c>WireCommandKind</c>
/// <c>HostOnly</c>. A new member-authored member therefore fails this gate until it
/// has a census row, which is the point: the question has to be answered where the
/// surface is added, not remembered later.
/// </para>
///
/// <para>
/// The two cell contracts are what keep the table honest rather than decorative: a
/// row is classified as Request, Fact or Presentation and states <c>bound:</c>,
/// <c>unbounded:</c> or <c>n/a:</c>; an <c>unbounded:</c> row must name an existing
/// ticket under <c>docs/backlog/</c>, so a gap cannot be parked silently.
/// </para>
/// </summary>
public class ReportValidationCensusGateTests
{
	private const string CensusPath = "docs/architecture/member-report-validation.md";
	private const string NetMsgPath = "src/CasualtiesUnknownOnline.Runtime/Protocol/NetMsg.cs";
	private const string WireCommandKindPath = "src/CasualtiesUnknownOnline.Protocol/Wire/WireCommandKind.cs";
	private const string AuthorityPolicyPath = "src/CasualtiesUnknownOnline.Application/Kernel/WireCommandAuthorityPolicy.cs";
	private const string SourceRoot = "src";

	/// <summary>The census floor: the table's own size at the change that landed it, so a truncated page fails instead of passing vacuously.</summary>
	private const int MinimumRows = 45;

	private static readonly string[] Classes = ["Request", "Fact", "Presentation"];

	private static readonly string[] VerdictPrefixes = ["bound:", "unbounded:", "n/a:"];

	private static readonly Regex MemberCell = new(@"^`(NetMsg|WireCommandKind)\.([A-Za-z_][A-Za-z0-9_]*)`$");

	private static readonly Regex AuthorityEntry = new(@"WireCommandKind\.([A-Za-z_][A-Za-z0-9_]*)\s*=>\s*AuthorityKind\.([A-Za-z_][A-Za-z0-9_]*)");

	private static readonly Regex HandlerAttribute = new(@"\[PacketHandler\(\s*NetMsg\.([A-Za-z_][A-Za-z0-9_]*)\s*,\s*NetMessageDirection\.([A-Za-z_][A-Za-z0-9_]*)\s*\)\]");

	private static readonly Regex TicketReference = new(@"docs/backlog/(?:todo|review|done)/[A-Za-z0-9_\-]+\.md");

	private static readonly string[] MemberAuthoredDirections = ["GuestToHost", "Bidirectional"];

	[Fact]
	public void TheCensusCoversEveryMemberAuthoredWireMember()
	{
		var rows = ParseCensus();
		var failures = ValidateReach(rows, WireKindsAMemberMayAuthor(), HandledMemberMessages());

		Assert.True(failures.Count == 0, "Report-validation census gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void EveryRowNamesALiveMemberAndAnswersTheQuestion()
	{
		var rows = ParseCensus();
		var failures = ValidateRows(rows, MinimumRows);

		Assert.True(failures.Count == 0, "Report-validation census row gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void RowValidation_FlagsAStaleMemberABadClassAMissingVerdictAndATicketlessGap()
	{
		var live = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
		{
			["NetMsg"] = ["BlockPlaced"],
			["WireCommandKind"] = ["ItemSpawn"],
		};
		var rows = new List<CensusRow>
		{
			new("NetMsg.BlockPlaced", "Fact", "bound: the host's own cell", "fine"),
			new("NetMsg.GoneMember", "Fact", "bound: something", "stale"),
			new("WireCommandKind.ItemSpawn", "Spawn", "bound: something", "bad class"),
			new("NetMsg.BlockPlaced", "Fact", "maybe", "bad verdict"),
			new("WireCommandKind.ItemSpawn", "Fact", "unbounded: an anchor", "no ticket named"),
			new("WireCommandKind.ItemSpawn", "Fact", "unbounded: an anchor", "`docs/backlog/todo/does-not-exist.md`"),
		};

		var failures = ValidateRows(rows, minimumRows: 1, live);

		Assert.Contains(failures, f => f.Contains("NetMsg.GoneMember", StringComparison.Ordinal) && f.Contains("no such member", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("invalid class 'Spawn'", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("cell 3 is 'maybe'", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("names no ticket", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("does-not-exist.md", StringComparison.Ordinal));
		Assert.DoesNotContain(failures, f => f.StartsWith("NetMsg.BlockPlaced: fine", StringComparison.Ordinal));
	}

	[Fact]
	public void ReachValidation_FlagsAnUnansweredSurface()
	{
		var rows = new List<CensusRow> { new("NetMsg.BlockPlaced", "Fact", "bound: x", "") };

		var failures = ValidateReach(
			rows,
			wireKinds: ["ItemSpawn", "RunStart"],
			messages: ["BlockPlaced", "Chat"]);

		Assert.Contains(failures, f => f.Contains("WireCommandKind.ItemSpawn is member-authored", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("NetMsg.Chat is member-authored", StringComparison.Ordinal));
		Assert.DoesNotContain(failures, f => f.Contains("BlockPlaced", StringComparison.Ordinal));
	}

	[Fact]
	public void TheDerivation_ReadsTheRealAuthorityTableAndTheRealHandlers()
	{
		// The reach is only as good as these two readers, and they are the half a
		// synthetic sample cannot pin: the authority table must yield the kinds a
		// member's own sender emits (and NOT the host-only ones), and the handler
		// scan must see member-authored directions in the real tree.
		var kinds = WireKindsAMemberMayAuthor();

		Assert.Contains("ItemSpawn", kinds);
		Assert.DoesNotContain("RunStart", kinds);
		Assert.DoesNotContain("UpdateFluidRegion", kinds);

		var messages = HandledMemberMessages();
		Assert.Contains("KernelEnvelope", messages);
		Assert.Contains("BlockDamaged", messages);
	}

	[Fact]
	public void RowValidation_FlagsATruncatedCensus()
	{
		var failures = ValidateRows([new CensusRow("NetMsg.BlockPlaced", "Fact", "bound: x", "")], MinimumRows);

		Assert.Contains(failures, f => f.Contains($"census rows (expected at least {MinimumRows})", StringComparison.Ordinal));
	}

	private static List<string> ValidateReach(
		IReadOnlyList<CensusRow> rows,
		IReadOnlyCollection<string> wireKinds,
		IReadOnlyCollection<string> messages)
	{
		var covered = rows.Select(row => row.Member).ToHashSet(StringComparer.Ordinal);
		var failures = new List<string>();
		foreach (var kind in wireKinds.Where(kind => !covered.Contains("WireCommandKind." + kind)))
		{
			failures.Add($"WireCommandKind.{kind} is member-authored (the authority policy does not declare it host-only) but has no row in {CensusPath} — answer what bounds it, or name the anchor it lacks and the ticket that carries it");
		}

		foreach (var message in messages.Where(message => !covered.Contains("NetMsg." + message)))
		{
			failures.Add($"NetMsg.{message} is member-authored (a packet handler serves it for a member's own direction) but has no row in {CensusPath} — answer what bounds it, or name the anchor it lacks and the ticket that carries it");
		}

		return failures;
	}

	private static List<string> ValidateRows(IReadOnlyList<CensusRow> rows, int minimumRows) =>
		ValidateRows(rows, minimumRows, LiveMembers());

	private static List<string> ValidateRows(
		IReadOnlyList<CensusRow> rows,
		int minimumRows,
		IReadOnlyDictionary<string, IReadOnlyCollection<string>> live)
	{
		var failures = new List<string>();
		if (rows.Count < minimumRows)
		{
			failures.Add($"{CensusPath} has {rows.Count} census rows (expected at least {minimumRows}); the table was truncated or its shape changed");
		}

		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var row in rows)
		{
			if (!seen.Add(row.Member))
			{
				failures.Add($"{row.Member}: duplicate census row");
			}

			var parts = row.Member.Split('.');
			if (!live.TryGetValue(parts[0], out var members) || !members.Contains(parts[1]))
			{
				failures.Add($"{row.Member}: no such member in the {parts[0]} enum (stale row)");
			}

			if (!Classes.Contains(row.Class, StringComparer.Ordinal))
			{
				failures.Add($"{row.Member}: invalid class '{row.Class}' (expected one of {string.Join(" | ", Classes)})");
			}

			var prefix = VerdictPrefixes.FirstOrDefault(candidate => row.Verdict.StartsWith(candidate, StringComparison.Ordinal));
			if (prefix is null)
			{
				failures.Add($"{row.Member}: cell 3 is '{row.Verdict}' — it must start with one of {string.Join(" | ", VerdictPrefixes)}");
				continue;
			}

			if (prefix != "unbounded:")
			{
				continue;
			}

			var ticket = TicketReference.Match(row.Detail);
			if (!ticket.Success)
			{
				failures.Add($"{row.Member}: an unbounded row names no ticket — the detail must carry an existing docs/backlog/ ticket path, or the gap is parked in silence");
			}
			else if (!File.Exists(RepositoryPaths.File(ticket.Value)))
			{
				failures.Add($"{row.Member}: the unbounded row names {ticket.Value}, which does not exist");
			}
		}

		return failures;
	}

	/// <summary>The census table's rows. A row is four cells whose first cell is a backquoted wire member; every other table on the page (the class legend, the header, the separator) is skipped by shape rather than by position.</summary>
	private static List<CensusRow> ParseCensus()
	{
		var path = RepositoryPaths.File(CensusPath);
		Assert.True(File.Exists(path), $"{CensusPath} missing");

		var rows = new List<CensusRow>();
		foreach (var line in File.ReadAllLines(path))
		{
			if (!line.StartsWith("|", StringComparison.Ordinal))
			{
				continue;
			}

			var raw = line.Split('|');
			var cells = raw.Skip(1).Take(raw.Length - 2).Select(cell => cell.Trim().Trim('`').Trim()).ToArray();
			if (cells.Length != 4)
			{
				continue;
			}

			var member = MemberCell.Match("`" + cells[0] + "`");
			if (!member.Success)
			{
				continue;
			}

			rows.Add(new CensusRow(cells[0], cells[1], cells[2], cells[3]));
		}

		return rows;
	}

	private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> LiveMembers() => new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
	{
		["NetMsg"] = EnumMembers(NetMsgPath),
		["WireCommandKind"] = EnumMembers(WireCommandKindPath),
	};

	private static IReadOnlyCollection<string> EnumMembers(string relativePath) =>
		[.. CSharpSyntaxTree.ParseText(File.ReadAllText(RepositoryPaths.File(relativePath)))
			.GetRoot()
			.DescendantNodes()
			.OfType<EnumDeclarationSyntax>()
			.SelectMany(declaration => declaration.Members)
			.Select(member => member.Identifier.ValueText)
			.Where(name => name.Length > 0)];

	/// <summary>Every wire command kind the authority policy does NOT declare host-only: the kinds a member's own sender actually emits, read from the table the seam itself reads.</summary>
	private static IReadOnlyCollection<string> WireKindsAMemberMayAuthor()
	{
		var text = SourceScan.Flatten(File.ReadAllText(RepositoryPaths.File(AuthorityPolicyPath)));
		var declared = AuthorityEntry.Matches(text)
			.Select(match => (Kind: match.Groups[1].Value, Authority: match.Groups[2].Value))
			.ToList();

		Assert.True(declared.Count > 0, $"no wire-command authority entries parsed from {AuthorityPolicyPath}");
		return [.. declared.Where(entry => entry.Authority != "HostOnly").Select(entry => entry.Kind)];
	}

	/// <summary>Every message a packet handler serves for a member's own direction, read off the handler attributes rather than from a list kept here.</summary>
	private static IReadOnlyCollection<string> HandledMemberMessages()
	{
		var messages = new HashSet<string>(StringComparer.Ordinal);
		foreach (var path in SourceScan.SourceFiles(RepositoryPaths.File(SourceRoot)))
		{
			foreach (Match match in HandlerAttribute.Matches(SourceScan.Flatten(File.ReadAllText(path))))
			{
				if (MemberAuthoredDirections.Contains(match.Groups[2].Value, StringComparer.Ordinal))
				{
					messages.Add(match.Groups[1].Value);
				}
			}
		}

		Assert.True(messages.Count > 0, $"no member-authored packet handlers found under {SourceRoot}");
		return messages;
	}

	private sealed record CensusRow(string Member, string Class, string Verdict, string Detail);
}
