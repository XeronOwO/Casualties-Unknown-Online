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
/// still be present in the source it quotes AND the declared anchor count of the
/// row it belongs to must match the entries that file holds for that row; the
/// verdict summary must match the rows; and every gap row must own an existing
/// ticket. A new sync feature therefore cannot ship without declaring its event +
/// fallback policy, and evidence cannot silently drift. Evidence lives in ONE
/// place (the JSON): a matrix row carries the decision and the NUMBER of anchors
/// it owns, never a copy of the quoted text, so the same claim is not
/// hand-maintained twice. A reference carries the path and the quoted text,
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

	/// <summary>A matrix data row has ten cells; the anchor-count cell and the gap-ticket cell are named so the layout is asserted in one place.</summary>
	private const int MatrixRowCellCount = 10;
	private const int AnchorCellIndex = 8;
	private const int TicketCellIndex = 9;

	/// <summary>The row value of an evidence entry that belongs to the document rather than to a coverage row (for example the "doc corrections" table); such an entry is verified like any other quote but is owned by no row. The count is capped so the marker cannot become a way to park coverage evidence out of every row's anchors.</summary>
	private const string UnassignedRow = "(none)";
	private const int MaximumUnassignedEntries = 5;

	/// <summary>A matrix row must NOT carry evidence text: the quote lives in the evidence file only, so a row that repeats a <c>path 'quote'</c> pair is exactly the double maintenance this contract removed.</summary>
	private static readonly Regex ForbiddenInlineEvidence = new(@"(?:src|tests|docs)/[A-Za-z0-9_\-./]+\.(?:cs|md)\s+'[^']*'");

	private static readonly Regex MatrixRowId = new(@"^[A-Z]{1,2}\d+[a-z]?$");
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
	public void SyncCoverageMatrix_DeclaredAnchorCountsMatchTheEvidence()
	{
		var matrix = ParseMatrix();
		var document = LoadEvidence();
		var failures = ValidateAnchorDeclarations(matrix.Rows, document.Entries ?? []);
		Assert.True(failures.Count == 0, "Sync-coverage anchor-declaration gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures.Take(20)));
	}

	[Fact]
	public void SyncCoverageEvidence_EveryQuoteMatchesItsSourceLine()
	{
		var failures = ValidateEvidence(LoadEvidence());
		Assert.True(failures.Count == 0, "Sync-coverage evidence gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures.Take(20)));
	}

	[Fact]
	public void AnchorDeclarationValidation_FlagsCountMismatchZeroAnchorsAndUnclaimedRow()
	{
		var rows = new List<MatrixRow>
		{
			new("W1", Row("W1", "OK", "2")),
			new("W2", Row("W2", "OK", "3")),
			new("W3", Row("W3", "OK", "1"))
		};
		var entries = new List<EvidenceEntry>
		{
			new("c", "W1", "src/one.cs", "quote one"),
			new("c", "W1", "src/two.cs", "quote two"),
			new("c", "W2", "src/three.cs", "quote three"),
			new("c", "W9", "src/four.cs", "quote four"),
			new("c", "(none)", "src/five.cs", "quote five")
		};

		var failures = ValidateAnchorDeclarations(rows, entries);

		Assert.Contains(failures, f => f.Contains("W2: declares 3 evidence anchors but", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("W3: no evidence anchors in", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("W9", StringComparison.Ordinal) && f.Contains("is neither a matrix row id", StringComparison.Ordinal));
		Assert.DoesNotContain(failures, f => f.Contains("row '(none)'", StringComparison.Ordinal));
		Assert.DoesNotContain(failures, f => f.StartsWith("W1:", StringComparison.Ordinal));
	}

	[Fact]
	public void RowValidation_FlagsInvalidVerdictMissingAnchorDeclarationDuplicatesAndMissingGapTicket()
	{
		var rows = new List<MatrixRow>
		{
			new("W1", Row("W1", "OK", "2")),
			new("W1", Row("W1", "Unverified", "2")),
			new("W2", Row("W2", "OK", "no anchors here")),
			new("W3", Row("W3", "Made-up verdict", "0")),
			new("W4", Row("W4", "Event-only gap", "1")),
			new("W5", ["W5", "feature", "direction", "event src/one.cs 'a quote'", "fallback", "backfill", "loss", "OK", "1", "ticket"])
		};

		var failures = ValidateRows(rows, minimumRows: 1);

		Assert.Contains(failures, f => f.Contains("W5: row repeats evidence text", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("duplicate matrix row id: W1", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("invalid verdict 'Unverified'", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("W2: evidence anchors cell 'no anchors here'", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("W3: evidence anchors cell '0'", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("invalid verdict 'Made-up verdict'", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("W4: gap row has no existing todo ticket", StringComparison.Ordinal));
	}

	[Fact]
	public void RowValidation_FlagsATruncatedMatrix()
	{
		var failures = ValidateRows([new MatrixRow("K1", Row("K1", "OK", "1"))], minimumRows: 64);
		Assert.Contains(failures, f => f.Contains("expected at least 64 rows", StringComparison.Ordinal));
	}

	[Fact]
	public void VerdictSummaryValidation_FlagsMismatchedCounts()
	{
		var rows = new List<MatrixRow>
		{
			new("W1", Row("W1", "OK", "1")),
			new("W2", Row("W2", "Event-only gap", "2"))
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
			new("R4", Row("R4", "OK", "1"))
		};

		var failures = ValidateVocabulary(index, membersByKind, rows);

		Assert.Contains(failures, f => f.Contains("NetMsg.Ping points at unknown matrix row 'NOPE'", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("WireCommandKind.ItemSpawn is not declared", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("NetMsg.RemovedMember is indexed but no longer exists", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("NetMsg.Handshake is indexed to row R4 but that row does not mention it", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("NetMsg.OK is indexed to row R4 but that row does not mention it", StringComparison.Ordinal));
	}

	[Fact]
	public void EvidenceValidation_FlagsCountMismatchMismatchedQuoteMissingFileTruncationAndEmptyQuote()
	{
		const string netMsg = "src/CasualtiesUnknownOnline.Runtime/Protocol/NetMsg.cs";
		var entries = new List<EvidenceEntry>
		{
			new("c", "W1", netMsg, "public enum NetMsg : byte"),
			new("c", "W1", netMsg, "this line text does not exist"),
			new("c", "W1", "src/does/not/exist.cs", "anything"),
			new("c", "W1", netMsg, "   "),
			new("c", "(none)", netMsg, "n1"),
			new("c", "(none)", netMsg, "n2"),
			new("c", "(none)", netMsg, "n3"),
			new("c", "(none)", netMsg, "n4"),
			new("c", "(none)", netMsg, "n5"),
			new("c", "(none)", netMsg, "n6")
		};

		var failures = ValidateEvidence(new EvidenceDocument(3, entries));

		Assert.Contains(failures, f => f.Contains("evidence file declares count=3 but carries 10 entries", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("evidence file has 10 entries (expected at least 700)", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("evidence file has 6 '(none)' entries", StringComparison.Ordinal));
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

	private static string[] Row(string id, string verdict, string anchors) =>
		[id, "feature", "direction", "event", "fallback", "backfill", "loss", verdict, anchors, "ticket"];

	private static string Normalize(string? value) =>
		value is null ? string.Empty : Regex.Replace(value.Trim(), @"\s+", " ");

	/// <summary>The anchor-count cell is a count, not an anchor list: the row id is the anchor key, so a row owns exactly the entries the evidence file records for it.</summary>
	private static bool TryParseAnchorCount(string cell, out int count) =>
		int.TryParse(cell.Trim().Trim('`'), out count);

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

			if (!TryParseAnchorCount(row.Cells[AnchorCellIndex], out var anchors) || anchors <= 0)
			{
				failures.Add($"{row.Id}: evidence anchors cell '{row.Cells[AnchorCellIndex]}' must declare at least one anchor from {EvidencePath}");
			}

			foreach (var cell in row.Cells)
			{
				if (ForbiddenInlineEvidence.IsMatch(cell))
				{
					failures.Add($"{row.Id}: row repeats evidence text ({ForbiddenInlineEvidence.Match(cell).Value}) - the quote belongs only in {EvidencePath}");
					break;
				}
			}

			if (GapVerdicts.Contains(verdict, StringComparer.Ordinal))
			{
				var ticket = TicketReference.Match(row.Cells[TicketCellIndex]);
				if (!ticket.Success || !File.Exists(RepositoryPaths.File("docs/backlog/" + ticket.Value)))
				{
					failures.Add($"{row.Id}: gap row has no existing todo ticket (cell: '{row.Cells[TicketCellIndex]}')");
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
	/// The row's anchor-count cell is the row's whole evidence claim: the evidence
	/// file must hold exactly that many entries for the row id, a covered row must
	/// own at least one entry, and no entry may name a row id the matrix does not
	/// have (an entry that belongs to the document rather than to a row is marked
	/// <see cref="UnassignedRow"/>). The quoted text itself lives only in the
	/// evidence file, so the same claim is never hand-maintained twice.
	/// </summary>
	private static List<string> ValidateAnchorDeclarations(
		IReadOnlyList<MatrixRow> rows,
		IReadOnlyList<EvidenceEntry> entries)
	{
		var failures = new List<string>();
		var rowIds = rows.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
		var countByRow = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var entry in entries)
		{
			countByRow[entry.Row] = countByRow.TryGetValue(entry.Row, out var existing) ? existing + 1 : 1;
		}

		foreach (var row in rows)
		{
			if (!TryParseAnchorCount(row.Cells[AnchorCellIndex], out var declared))
			{
				failures.Add($"{row.Id}: evidence anchors cell '{row.Cells[AnchorCellIndex]}' is not a count");
				continue;
			}

			var actual = countByRow.TryGetValue(row.Id, out var value) ? value : 0;
			if (actual == 0)
			{
				failures.Add($"{row.Id}: no evidence anchors in {EvidencePath} - a covered row must own at least one entry");
				continue;
			}

			if (declared != actual)
			{
				failures.Add($"{row.Id}: declares {declared} evidence anchors but {EvidencePath} holds {actual} for that row");
			}
		}

		foreach (var entry in entries)
		{
			if (entry.Row != UnassignedRow && !rowIds.Contains(entry.Row))
			{
				failures.Add($"evidence entry with row '{entry.Row}' ({entry.File}) is neither a matrix row id in {MatrixPath} nor '{UnassignedRow}'");
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

	private static List<string> ValidateEvidence(EvidenceDocument document)
	{
		var failures = new List<string>();
		var entries = document.Entries ?? [];
		if (document.Count != entries.Count)
		{
			failures.Add($"evidence file declares count={document.Count} but carries {entries.Count} entries");
		}

		if (entries.Count < MinimumEvidenceEntries)
		{
			failures.Add($"evidence file has {entries.Count} entries (expected at least {MinimumEvidenceEntries})");
		}

		var unassigned = entries.Count(e => e.Row == UnassignedRow);
		if (unassigned > MaximumUnassignedEntries)
		{
			failures.Add($"evidence file has {unassigned} '{UnassignedRow}' entries; at most {MaximumUnassignedEntries} document-level anchors are allowed");
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

	private static EvidenceDocument LoadEvidence()
	{
		var path = RepositoryPaths.File(EvidencePath);
		Assert.True(File.Exists(path), $"{EvidencePath} missing");
		var document = JsonSerializer.Deserialize<EvidenceDocument>(File.ReadAllText(path), JsonOptions)
			?? throw new InvalidOperationException($"{EvidencePath} could not be deserialized");
		Assert.NotNull(document.Entries);
		return document;
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
			if (cells.Length == MatrixRowCellCount && MatrixRowId.IsMatch(cells[0]))
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

