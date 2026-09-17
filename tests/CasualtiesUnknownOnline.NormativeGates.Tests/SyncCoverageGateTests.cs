using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Sync-coverage rot guard. Every wire/stream discriminator (NetMsg,
/// WireCommandKind, WireEventKind, AdaptiveStreamId) must be declared in
/// <c>docs/evidence/sync-coverage-matrix.md</c> with an owning matrix row whose
/// semantic text mentions it and whose verdict is one of the audit vocabulary;
/// every evidence entry in <c>docs/evidence/sync-coverage-evidence.json</c> must
/// still be present in the source it quotes; every inline matrix reference must be
/// anchored in that evidence file; the verdict summary must match the rows; and
/// every gap row must own an existing ticket. A new sync feature therefore
/// cannot ship without declaring its event + fallback policy, and evidence
/// cannot silently drift. A reference carries the path and the quoted text,
/// never a line number: a line number drifts with every edit above it and then
/// has to be re-pointed by hand, while the quoted text is what the evidence
/// actually asserts.
/// </summary>
public class SyncCoverageGateTests
{
	private const string MatrixPath = "docs/evidence/sync-coverage-matrix.md";
	private const string EvidencePath = "docs/evidence/sync-coverage-evidence.json";
	private const int MinimumMatrixRows = 64;
	private const int MinimumEvidenceEntries = 700;

	private static readonly Regex MatrixRowId = new(@"^[A-Z]{1,2}\d+[a-z]?$");
	private static readonly Regex InlineReference = new(@"(?:src|tests|docs)/[A-Za-z0-9_\-./]+\.(?:cs|md)");
	private static readonly Regex InlineQuote = new(@"((?:src|tests|docs)/[A-Za-z0-9_\-./]+\.(?:cs|md))\s+'(?<quote>[^']*)'");
	private static readonly Regex TicketReference = new(@"todo/[A-Za-z0-9_\-]+\.md");
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

	private static readonly string[] AllowedVerdicts =
	[
		"OK",
		"Event-only gap",
		"Fallback-only gap",
		"Transient-by-design"
	];

	private static readonly string[] GapVerdicts = ["Event-only gap", "Fallback-only gap"];

	private static readonly (string Kind, string Path)[] Discriminators =
	[
		("NetMsg", "src/CasualtiesUnknownOnline.Runtime/Protocol/NetMsg.cs"),
		("WireCommandKind", "src/CasualtiesUnknownOnline.Protocol/Wire/WireCommandKind.cs"),
		("WireEventKind", "src/CasualtiesUnknownOnline.Protocol/Wire/WireEventKind.cs"),
		("AdaptiveStreamId", "src/CasualtiesUnknownOnline.Runtime/Session/AdaptiveSync/AdaptiveStreamId.cs")
	];

	private static readonly string[] IndexKinds = [.. Discriminators.Select(d => d.Kind)];

	/// <summary>Semantic matrix cells used for the member-mention check: feature, direction, event, fallback, backfill, loss. The id, verdict and ticket cells are excluded so a member named "OK" or "W1" cannot be parked on them.</summary>
	private static readonly int[] SemanticCellIndexes = [1, 2, 3, 4, 5, 6];

	[Fact]
	public void SyncCoverageMatrix_EveryRowHasAVerdictAndEvidence()
	{
		var matrix = ParseMatrix();
		var failures = ValidateRows(matrix.Rows, MinimumMatrixRows);
		Assert.True(failures.Count == 0, "Sync-coverage matrix gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void SyncCoverageMatrix_VerdictSummaryMatchesTheRows()
	{
		var matrix = ParseMatrix();
		var summary = ParseVerdictSummary();
		var failures = ValidateVerdictSummary(summary, matrix.Rows);
		Assert.True(failures.Count == 0, "Sync-coverage verdict summary gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void SyncCoverageMatrix_EveryWireDiscriminatorIsIndexed()
	{
		var matrix = ParseMatrix();
		var membersByKind = ReadDiscriminatorMembers();
		var failures = ValidateVocabulary(matrix.Index, membersByKind, matrix.Rows);
		Assert.True(failures.Count == 0, "Sync-coverage vocabulary gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void SyncCoverageMatrix_InlineRefsAreEvidenceAnchoredAndQuoted()
	{
		var matrix = ParseMatrix();
		var evidenceFiles = LoadEvidence().Select(e => e.File).ToHashSet(StringComparer.Ordinal);
		var failures = ValidateInlineReferences(matrix.Rows, evidenceFiles);
		Assert.True(failures.Count == 0, "Sync-coverage inline-reference gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures.Take(20)));
	}

	[Fact]
	public void SyncCoverageEvidence_EveryQuoteMatchesItsSourceLine()
	{
		var failures = ValidateEvidence(LoadEvidence());
		Assert.True(failures.Count == 0, "Sync-coverage evidence gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures.Take(20)));
	}

	[Fact]
	public void InlineReferenceValidation_FlagsUngroundedAnchorAndQuoteMismatch()
	{
		const string netMsg = "src/CasualtiesUnknownOnline.Runtime/Protocol/NetMsg.cs";
		var rows = new List<MatrixRow>
		{
			new("W1", ["W1", "feature", "direction", $"event {netMsg} 'public enum NetMsg : byte'", "fallback", "backfill", "loss", "OK", "ticket"]),
			new("W2", ["W2", "feature", "direction", $"event {netMsg} 'this text does not exist'", "fallback", "backfill", "loss", "OK", "ticket"]),
			new("W3", ["W3", "feature", "direction", "event src/CasualtiesUnknownOnline.Runtime/Protocol/NetMsgUnknown.cs 'public enum NetMsg : byte'", "fallback", "backfill", "loss", "OK", "ticket"])
		};
		var evidence = new HashSet<string>(StringComparer.Ordinal) { netMsg };

		var failures = ValidateInlineReferences(rows, evidence);

		Assert.Contains(failures, f => f.Contains($"W2: inline quote is not in {netMsg}", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("W3: inline reference src/CasualtiesUnknownOnline.Runtime/Protocol/NetMsgUnknown.cs has no evidence anchor", StringComparison.Ordinal));
		Assert.DoesNotContain(failures, f => f.StartsWith("W1:", StringComparison.Ordinal));
	}

	[Fact]
	public void RowValidation_FlagsInvalidVerdictMissingEvidenceDuplicatesAndMissingGapTicket()
	{
		var rows = new List<MatrixRow>
		{
			new("W1", Row("W1", "OK", "src/one.cs:1")),
			new("W1", Row("W1", "Unverified", "src/two.cs:2")),
			new("W2", Row("W2", "OK", "no evidence here")),
			new("W3", Row("W3", "Made-up verdict", "src/three.cs:3")),
			new("W4", Row("W4", "Event-only gap", "src/four.cs:4"))
		};

		var failures = ValidateRows(rows, minimumRows: 1);

		Assert.Contains(failures, f => f.Contains("duplicate matrix row id: W1", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("invalid verdict 'Unverified'", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("W2: no source evidence reference", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("invalid verdict 'Made-up verdict'", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("W4: gap row has no existing todo ticket", StringComparison.Ordinal));
	}

	[Fact]
	public void RowValidation_FlagsATruncatedMatrix()
	{
		var failures = ValidateRows([new MatrixRow("K1", Row("K1", "OK", "src/one.cs:1"))], minimumRows: 64);
		Assert.Contains(failures, f => f.Contains("expected at least 64 rows", StringComparison.Ordinal));
	}

	[Fact]
	public void VerdictSummaryValidation_FlagsMismatchedCounts()
	{
		var rows = new List<MatrixRow>
		{
			new("W1", Row("W1", "OK", "src/one.cs:1")),
			new("W2", Row("W2", "Event-only gap", "src/two.cs:2"))
		};
		var summary = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["OK"] = 2,
			["Event-only gap"] = 0,
			["Fallback-only gap"] = 0,
			["Transient-by-design"] = 0
		};

		var failures = ValidateVerdictSummary(summary, rows);

		Assert.Contains(failures, f => f.Contains("verdict summary says OK=2 but the matrix has 1", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("verdict summary says Event-only gap=0 but the matrix has 1", StringComparison.Ordinal));
	}

	[Fact]
	public void VocabularyValidation_FlagsUnindexedStaleUnknownRowAndUnrelatedRow()
	{
		var membersByKind = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
		{
			["NetMsg"] = ["Handshake", "Ping"],
			["WireCommandKind"] = ["ItemSpawn"],
			["WireEventKind"] = [],
			["AdaptiveStreamId"] = []
		};
		var index = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["NetMsg.Handshake"] = "R4",
			["NetMsg.Ping"] = "NOPE",
			["NetMsg.RemovedMember"] = "R4",
			["NetMsg.OK"] = "R4"
		};
		var rows = new List<MatrixRow>
		{
			new("R4", Row("R4", "OK", "src/one.cs:1"))
		};

		var failures = ValidateVocabulary(index, membersByKind, rows);

		Assert.Contains(failures, f => f.Contains("NetMsg.Ping points at unknown matrix row 'NOPE'", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("WireCommandKind.ItemSpawn is not declared", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("NetMsg.RemovedMember is indexed but no longer exists", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("NetMsg.Handshake is indexed to row R4 but that row does not mention it", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("NetMsg.OK is indexed to row R4 but that row does not mention it", StringComparison.Ordinal));
	}

	[Fact]
	public void EvidenceValidation_FlagsMismatchedQuoteMissingFileTruncationAndEmptyQuote()
	{
		const string netMsg = "src/CasualtiesUnknownOnline.Runtime/Protocol/NetMsg.cs";
		var entries = new List<EvidenceEntry>
		{
			new("c", "W1", netMsg, "public enum NetMsg : byte"),
			new("c", "W1", netMsg, "this line text does not exist"),
			new("c", "W1", "src/does/not/exist.cs", "anything"),
			new("c", "W1", netMsg, "   ")
		};

		var failures = ValidateEvidence(entries);

		Assert.Contains(failures, f => f.Contains("evidence file has 4 entries (expected at least 700)", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains($"quote is not in {netMsg}", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("missing file src/does/not/exist.cs", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains($"empty quote at {netMsg}", StringComparison.Ordinal));
	}

	[Fact]
	public void EnumBodyParsing_HandlesAttributesEscapedIdentifiersAndComments()
	{
		const string source = """
			public enum Sample : byte
			{
				// comment only
				[Obsolete]
				OldMember = 1,
				@class, // trailing comment
				Third = 3
			}
			""";

		var members = ParseEnumMembers(source);

		Assert.Equal(["OldMember", "Third", "class"], members.OrderBy(m => m, StringComparer.Ordinal));
	}

	private static string[] Row(string id, string verdict, string evidence) =>
		[id, "feature", "direction", "event", "fallback", "backfill", evidence, verdict, "ticket"];

	private static string Normalize(string? value) =>
		value is null ? string.Empty : Regex.Replace(value.Trim(), @"\s+", " ");

	private static List<string> ValidateRows(IReadOnlyList<MatrixRow> rows, int minimumRows)
	{
		var failures = new List<string>();
		if (rows.Count < minimumRows)
		{
			failures.Add($"matrix has {rows.Count} rows (expected at least {minimumRows} rows); the matrix was truncated or the table format changed");
		}

		var seenIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var row in rows)
		{
			if (!seenIds.Add(row.Id))
			{
				failures.Add($"duplicate matrix row id: {row.Id}");
			}

			var verdict = row.Cells[7];
			if (!AllowedVerdicts.Contains(verdict, StringComparer.Ordinal))
			{
				failures.Add($"{row.Id}: invalid verdict '{verdict}' (expected one of {string.Join(" | ", AllowedVerdicts)})");
			}

			if (!row.Cells.Any(cell => InlineReference.IsMatch(cell)))
			{
				failures.Add($"{row.Id}: no source evidence reference in any cell");
			}

			if (GapVerdicts.Contains(verdict, StringComparer.Ordinal))
			{
				var ticket = TicketReference.Match(row.Cells[8]);
				if (!ticket.Success || !File.Exists(RepositoryPaths.File("docs/backlog/" + ticket.Value)))
				{
					failures.Add($"{row.Id}: gap row has no existing todo ticket (cell: '{row.Cells[8]}')");
				}
			}
		}

		return failures;
	}

	private static List<string> ValidateVerdictSummary(IReadOnlyDictionary<string, int> summary, IReadOnlyList<MatrixRow> rows)
	{
		var failures = new List<string>();
		foreach (var verdict in AllowedVerdicts)
		{
			var actual = rows.Count(r => r.Cells[7] == verdict);
			var declared = summary.TryGetValue(verdict, out var value) ? value : -1;
			if (declared != actual)
			{
				failures.Add($"verdict summary says {verdict}={declared} but the matrix has {actual}");
			}
		}

		if (summary.TryGetValue("Unverified", out var unverified) && unverified > 0)
		{
			failures.Add("verdict summary declares Unverified rows; the audit forbids them");
		}

		return failures;
	}

	/// <summary>
	/// A QUOTED reference is the evidence claim: its path must be anchored in the
	/// evidence file AND the quoted text must still be in that source. A bare path
	/// mention (no quote) is prose, not a claim, so it is not anchored here — which
	/// is also why dropping the line number costs nothing: the claim was always the
	/// quoted text, never the position.
	/// </summary>
	private static List<string> ValidateInlineReferences(
		IReadOnlyList<MatrixRow> rows,
		IReadOnlySet<string> evidenceFiles)
	{
		var failures = new List<string>();
		var textByFile = new Dictionary<string, string>(StringComparer.Ordinal);

		foreach (var row in rows)
		{
			foreach (var cell in row.Cells)
			{
				foreach (Match match in InlineQuote.Matches(cell))
				{
					var relative = match.Groups[1].Value;
					if (!evidenceFiles.Contains(relative))
					{
						failures.Add($"{row.Id}: inline reference {relative} has no evidence anchor in {EvidencePath}");
					}

					var quote = Normalize(match.Groups["quote"].Value.Replace("&#124;", "|", StringComparison.Ordinal));
					if (quote.Length == 0)
					{
						continue;
					}

					var fullPath = RepositoryPaths.File(relative);
					if (!File.Exists(fullPath))
					{
						failures.Add($"{row.Id}: inline quote file missing {relative}");
						continue;
					}

					if (!NormalizedSourceText(fullPath, textByFile).Contains(quote, StringComparison.Ordinal))
					{
						failures.Add($"{row.Id}: inline quote is not in {relative}: '{quote}'");
					}
				}
			}
		}

		return failures;
	}

	/// <summary>
	/// The whole file with runs of whitespace folded to one space, so a quote is
	/// matched against the text it was copied from rather than against one line
	/// number. A line number is deliberately NOT part of the contract: it drifts
	/// with every edit above it and then has to be re-pointed by hand, while the
	/// quoted text is what the evidence actually asserts.
	/// </summary>
	private static string NormalizedSourceText(string fullPath, Dictionary<string, string> cache)
	{
		if (!cache.TryGetValue(fullPath, out var text))
		{
			text = Normalize(File.ReadAllText(fullPath));
			cache[fullPath] = text;
		}

		return text;
	}

	private static List<string> ValidateVocabulary(
		IReadOnlyDictionary<string, string> index,
		IReadOnlyDictionary<string, IReadOnlyCollection<string>> membersByKind,
		IReadOnlyList<MatrixRow> rows)
	{
		var failures = new List<string>();
		var rowIds = rows.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
		var rowTextById = rows.ToDictionary(
			r => r.Id,
			r => string.Join(" ", SemanticCellIndexes.Select(i => r.Cells[i])),
			StringComparer.Ordinal);

		foreach (var entry in index)
		{
			if (!rowIds.Contains(entry.Value))
			{
				failures.Add($"index entry {entry.Key} points at unknown matrix row '{entry.Value}'");
				continue;
			}

			var member = entry.Key[(entry.Key.IndexOf('.') + 1)..];
			if (!Regex.IsMatch(rowTextById[entry.Value], $@"(?<![\w]){Regex.Escape(member)}(?![\w])"))
			{
				failures.Add($"index entry {entry.Key} is indexed to row {entry.Value} but that row does not mention it");
			}
		}

		foreach (var (kind, members) in membersByKind)
		{
			foreach (var member in members)
			{
				if (!index.ContainsKey(kind + "." + member))
				{
					failures.Add($"{kind}.{member} is not declared in the wire vocabulary index - add it with its owning matrix row and an event+fallback policy");
				}
			}

			foreach (var entry in index.Where(e => e.Key.StartsWith(kind + ".", StringComparison.Ordinal)))
			{
				var member = entry.Key[(kind.Length + 1)..];
				if (!members.Contains(member))
				{
					failures.Add($"{entry.Key} is indexed but no longer exists in the {kind} enum (stale entry)");
				}
			}
		}

		return failures;
	}

	private static List<string> ValidateEvidence(IReadOnlyList<EvidenceEntry> entries)
	{
		var failures = new List<string>();
		if (entries.Count < MinimumEvidenceEntries)
		{
			failures.Add($"evidence file has {entries.Count} entries (expected at least {MinimumEvidenceEntries})");
		}

		var textByFile = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var entry in entries)
		{
			var quote = Normalize(entry.Quote);
			if (quote.Length == 0)
			{
				failures.Add($"{entry.Row}: empty quote at {entry.File}");
				continue;
			}

			var fullPath = RepositoryPaths.File(entry.File);
			if (!File.Exists(fullPath))
			{
				failures.Add($"{entry.Row}: missing file {entry.File}");
				continue;
			}

			if (!NormalizedSourceText(fullPath, textByFile).Contains(quote, StringComparison.Ordinal))
			{
				failures.Add($"{entry.Row}: quote is not in {entry.File}: '{quote}'");
			}
		}

		return failures;
	}

	private static Dictionary<string, IReadOnlyCollection<string>> ReadDiscriminatorMembers()
	{
		var membersByKind = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
		foreach (var (kind, path) in Discriminators)
		{
			var members = ParseEnumMembers(File.ReadAllText(RepositoryPaths.File(path)));
			Assert.True(members.Count > 0, $"no enum members parsed from {path}");
			membersByKind[kind] = members;
		}

		return membersByKind;
	}

	/// <summary>
	/// Parses enum members with Roslyn, so attributes, escaped identifiers and
	/// comments cannot hide a member from the "new wire member must be declared"
	/// check.
	/// </summary>
	private static IReadOnlyCollection<string> ParseEnumMembers(string source)
	{
		var root = CSharpSyntaxTree.ParseText(source).GetRoot();
		return root.DescendantNodes()
			.OfType<EnumDeclarationSyntax>()
			.SelectMany(declaration => declaration.Members)
			.Select(member => member.Identifier.ValueText)
			.Where(name => name.Length > 0)
			.ToHashSet(StringComparer.Ordinal);
	}

	private static IReadOnlyList<EvidenceEntry> LoadEvidence()
	{
		var path = RepositoryPaths.File(EvidencePath);
		Assert.True(File.Exists(path), $"{EvidencePath} missing");
		var document = JsonSerializer.Deserialize<EvidenceDocument>(File.ReadAllText(path), JsonOptions)
			?? throw new InvalidOperationException($"{EvidencePath} could not be deserialized");
		Assert.NotNull(document.Entries);
		return document.Entries;
	}

	private static IReadOnlyDictionary<string, int> ParseVerdictSummary()
	{
		var path = RepositoryPaths.File(MatrixPath);
		Assert.True(File.Exists(path), $"{MatrixPath} missing");

		var summary = new Dictionary<string, int>(StringComparer.Ordinal);
		var inSummary = false;
		foreach (var line in File.ReadAllLines(path))
		{
			if (line.StartsWith("## ", StringComparison.Ordinal))
			{
				inSummary = line.Contains("Verdict summary", StringComparison.Ordinal);
				continue;
			}

			if (!inSummary || !line.StartsWith("|", StringComparison.Ordinal))
			{
				continue;
			}

			var raw = line.Split('|');
			var cells = raw.Skip(1).Take(raw.Length - 2).Select(c => c.Trim().Trim('`')).ToArray();
			if (cells.Length == 3 && int.TryParse(cells[1], out var count))
			{
				if (!summary.TryAdd(cells[0], count))
				{
					Assert.Fail($"duplicate verdict summary entry: {cells[0]}");
				}
			}
		}

		Assert.True(summary.Count > 0, "verdict summary table not found in " + MatrixPath);
		return summary;
	}

	private static Matrix ParseMatrix()
	{
		var path = RepositoryPaths.File(MatrixPath);
		Assert.True(File.Exists(path), $"{MatrixPath} missing");

		var rows = new List<MatrixRow>();
		var index = new Dictionary<string, string>(StringComparer.Ordinal);

		foreach (var line in File.ReadAllLines(path))
		{
			if (!line.StartsWith("|", StringComparison.Ordinal))
			{
				continue;
			}

			var raw = line.Split('|');
			var cells = raw.Skip(1).Take(raw.Length - 2).Select(c => c.Trim()).ToArray();
			if (cells.Length == 9 && MatrixRowId.IsMatch(cells[0]))
			{
				rows.Add(new MatrixRow(cells[0], cells));
				continue;
			}

			if (cells.Length == 3 && IndexKinds.Contains(cells[1], StringComparer.Ordinal))
			{
				var key = cells[1] + "." + cells[0];
				if (!index.TryAdd(key, cells[2]))
				{
					Assert.Fail($"duplicate wire vocabulary index entry: {key}");
				}
			}
		}

		Assert.True(rows.Count > 0, "no matrix rows parsed from " + MatrixPath);
		return new Matrix(rows, index);
	}

	private sealed record MatrixRow(string Id, string[] Cells);

	private sealed record Matrix(IReadOnlyList<MatrixRow> Rows, IReadOnlyDictionary<string, string> Index);

	private sealed record EvidenceDocument(int Count, IReadOnlyList<EvidenceEntry>? Entries);

	private sealed record EvidenceEntry(string Cluster, string Row, string File, string Quote);
}

